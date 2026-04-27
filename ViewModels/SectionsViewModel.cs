using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.ApplicationModel;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class SectionsViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly SectionCloudService _sectionCloudService;

    public ObservableCollection<Section> JoinedSections { get; } = new();

    private string _newSectionName = string.Empty;
    public string NewSectionName
    {
        get => _newSectionName;
        set => SetProperty(ref _newSectionName, value);
    }

    private string _joinCode = string.Empty;
    public string JoinCode
    {
        get => _joinCode;
        set => SetProperty(ref _joinCode, value);
    }

    private int _sectionCount;
    public int SectionCount
    {
        get => _sectionCount;
        set => SetProperty(ref _sectionCount, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public SectionsViewModel()
    {
        _databaseService = new DatabaseService();
        _sectionCloudService = new SectionCloudService();
    }

    [RelayCommand]
    public async Task LoadSectionsAsync()
    {
        try
        {
            IsBusy = true;
            var sections = await _sectionCloudService.GetJoinedSectionsAsync();

            JoinedSections.Clear();
            foreach (var section in sections.OrderBy(section => section.Name))
            {
                await _databaseService.SaveSectionAsync(section);
                JoinedSections.Add(section);
            }

            SectionCount = JoinedSections.Count;
        }
        catch (Exception ex)
        {
            await LoadCachedSectionsAsync();
            await Shell.Current.DisplayAlert(
                GetFriendlyTitle(ex),
                GetFriendlyMessage("loading sections", ex),
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateSectionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSectionName))
        {
            await Shell.Current.DisplayAlert("Missing name", "Enter a section name first.", "OK");
            return;
        }

        try
        {
            IsBusy = true;

            var section = await _sectionCloudService.CreateSectionAsync(
                NewSectionName.Trim(),
                await GenerateUniqueInviteCodeAsync());

            await _databaseService.SaveSectionAsync(section);
            NewSectionName = string.Empty;
            await LoadSectionsAsync();

            await Shell.Current.DisplayAlert(
                "Section created",
                $"Invite code: {section.InviteCode}",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                GetFriendlyTitle(ex),
                GetFriendlyMessage("creating the section", ex),
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task JoinSectionAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCode))
        {
            await Shell.Current.DisplayAlert("Missing code", "Enter a section code first.", "OK");
            return;
        }

        var normalizedCode = JoinCode.Trim().ToUpperInvariant();
        try
        {
            var section = await _sectionCloudService.JoinSectionByCodeAsync(normalizedCode);

            if (section != null)
            {
                await _databaseService.SaveSectionAsync(section);
                await LoadSectionsAsync();
                await Shell.Current.DisplayAlert(
                    "Section joined",
                    $"You joined {section.Name}.",
                    "OK");
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    "Code not found",
                    "No section was found for that invite code.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                GetFriendlyTitle(ex),
                GetFriendlyMessage("joining the section", ex),
                "OK");
        }

        JoinCode = string.Empty;
    }

    [RelayCommand]
    private async Task CopyInviteCodeAsync(Section section)
    {
        if (section == null) return;

        await Clipboard.Default.SetTextAsync(section.InviteCode);
        await Shell.Current.DisplayAlert("Copied", $"{section.InviteCode} copied to clipboard.", "OK");
    }

    [RelayCommand]
    private async Task ShareInviteAsync(Section section)
    {
        if (section == null) return;

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = section.Name,
            Subject = $"{section.Name} invite",
            Text = $"Join my StudySync section \"{section.Name}\" with code: {section.InviteCode}"
        });
    }

    [RelayCommand]
    private async Task OpenSectionAsync(Section section)
    {
        if (section == null)
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(Views.SectionDetailPage)}?sectionName={Uri.EscapeDataString(section.Name)}&inviteCode={Uri.EscapeDataString(section.InviteCode)}");
    }

    [RelayCommand]
    private async Task ShowSectionActionsAsync(Section section)
    {
        if (section == null)
            return;

        var action = await Shell.Current.DisplayActionSheet(
            section.Name,
            "Cancel",
            null,
            "Open Section",
            "Copy Code",
            "Share Invite",
            "Leave Section");

        switch (action)
        {
            case "Open Section":
                await OpenSectionAsync(section);
                break;
            case "Copy Code":
                await CopyInviteCodeAsync(section);
                break;
            case "Share Invite":
                await ShareInviteAsync(section);
                break;
            case "Leave Section":
                await LeaveSectionAsync(section);
                break;
        }
    }

    [RelayCommand]
    private async Task LeaveSectionAsync(Section section)
    {
        if (section == null)
            return;

        bool shouldLeave = await Shell.Current.DisplayAlert(
            "Leave section",
            $"Leave {section.Name}? You can always rejoin later with the invite code.",
            "Leave",
            "Cancel");

        if (!shouldLeave)
            return;

        try
        {
            IsBusy = true;
            await _sectionCloudService.LeaveSectionAsync(section.InviteCode);
            await _databaseService.DeleteSectionAsync(section);
            await LoadSectionsAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                GetFriendlyTitle(ex),
                GetFriendlyMessage("leaving the section", ex),
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        while (true)
        {
            var code = $"SS-{Guid.NewGuid():N}"[..9].ToUpperInvariant();
            var exists = await _sectionCloudService.InviteCodeExistsAsync(code);
            if (!exists)
                return code;
        }
    }

    private async Task LoadCachedSectionsAsync()
    {
        JoinedSections.Clear();
        var sections = await _databaseService.GetSectionsAsync();
        foreach (var section in sections.OrderBy(section => section.Name))
            JoinedSections.Add(section);

        SectionCount = JoinedSections.Count;
    }

    private static string GetFriendlyTitle(Exception ex) =>
        ex.Message.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase)
            ? "Firestore blocked"
            : "Sections error";

    private static string GetFriendlyMessage(string action, Exception ex) =>
        ex.Message.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase)
            ? "Firebase denied access to Firestore. Update your Firestore rules before testing sections."
            : $"Something went wrong while {action}: {ex.Message}";
}
