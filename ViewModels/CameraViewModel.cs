using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using StudySync.Models;
using StudySync.Services;
using StudySync.Views;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace StudySync.ViewModels;

public partial class CameraViewModel : ObservableObject
{
    private readonly OCRService _ocrService;
    private readonly SectionCloudService _sectionCloudService;
    private readonly DatabaseService _databaseService;
    private readonly ObservableCollection<CapturedPageItem> _capturedPages = new();

    private ImageSource _previewSource;
    public ImageSource PreviewSource
    {
        get => _previewSource;
        set => SetProperty(ref _previewSource, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ObservableCollection<CapturedPageItem> CapturedPages => _capturedPages;

    public bool HasCapturedPages => CapturedPages.Count > 0;

    public string PageCountText =>
        CapturedPages.Count switch
        {
            0 => "No pages added yet",
            1 => "1 page ready for OCR",
            _ => $"{CapturedPages.Count} pages ready for OCR"
        };

    public string ProcessButtonText =>
        CapturedPages.Count <= 1 ? "Process Page" : $"Process {CapturedPages.Count} Pages";

    public IAsyncRelayCommand TakePhotoCommand { get; }
    public IAsyncRelayCommand PickPhotoCommand { get; }
    public IAsyncRelayCommand ProcessPagesCommand { get; }
    public IAsyncRelayCommand ClearPagesCommand { get; }
    public IRelayCommand<CapturedPageItem> PreviewPageCommand { get; }
    public IRelayCommand<CapturedPageItem> MovePageLeftCommand { get; }
    public IRelayCommand<CapturedPageItem> MovePageRightCommand { get; }
    public IRelayCommand<CapturedPageItem> RemovePageCommand { get; }

    public CameraViewModel()
    {
        _ocrService = new OCRService();
        _sectionCloudService = new SectionCloudService();
        _databaseService = new DatabaseService();
        _previewSource = "camera_icon.png";

        TakePhotoCommand = new AsyncRelayCommand(TakePhotoAsync);
        PickPhotoCommand = new AsyncRelayCommand(PickPhotoAsync);
        ProcessPagesCommand = new AsyncRelayCommand(ProcessPagesAsync);
        ClearPagesCommand = new AsyncRelayCommand(ClearPagesAsync);
        PreviewPageCommand = new RelayCommand<CapturedPageItem>(PreviewPage);
        MovePageLeftCommand = new RelayCommand<CapturedPageItem>(MovePageLeft);
        MovePageRightCommand = new RelayCommand<CapturedPageItem>(MovePageRight);
        RemovePageCommand = new RelayCommand<CapturedPageItem>(RemovePage);
    }

    private async Task TakePhotoAsync()
    {
        try
        {
            IsBusy = true;

            var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlert(
                    "Permission Denied",
                    "Camera permission is required to take photos.",
                    "OK");
                return;
            }

            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                if (storageStatus != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlert(
                        "Permission Denied",
                        "Storage permission is required to save photos.",
                        "OK");
                    return;
                }
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Capture your notes"
            });

            if (photo == null)
                return;

            var localPath = await SavePhotoAsync(photo);
            if (await ShouldContinueWithImageAsync(localPath, "Retake"))
                AddCapturedPage(localPath);
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlert(
                "Not Supported",
                "Camera is not supported on this device.",
                "OK");
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert(
                "Permission Error",
                "Unable to get camera permission.",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Error",
                $"Failed to capture photo: {ex.Message}",
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PickPhotoAsync()
    {
        try
        {
            IsBusy = true;

            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Choose a photo of your notes"
            });

            if (photo == null)
                return;

            var localPath = await SavePhotoAsync(photo);
            if (await ShouldContinueWithImageAsync(localPath, "Choose Another"))
                AddCapturedPage(localPath);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Error",
                $"Failed to pick photo: {ex.Message}",
                "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ProcessPagesAsync()
    {
        if (CapturedPages.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                "No pages yet",
                "Take a photo or choose one from the gallery before starting OCR.",
                "OK");
            return;
        }

        var selectedSection = await PromptForSectionAsync();
        if (selectedSection == null)
            return;

        var imagePathsJson = JsonSerializer.Serialize(CapturedPages.Select(page => page.ImagePath).ToList());
        ResetCaptureSession();
        await Shell.Current.GoToAsync(
            $"{nameof(ResultPage)}?imagePaths={Uri.EscapeDataString(imagePathsJson)}&sectionInviteCode={Uri.EscapeDataString(selectedSection.InviteCode)}");
    }

    private async Task<Section?> PromptForSectionAsync()
    {
        var sections = await GetJoinedSectionsAsync();
        if (sections.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                "No sections yet",
                "Join a section first before sending a processed note to classmates.",
                "OK");
            return null;
        }

        var orderedSections = sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Name))
            .OrderBy(section => section.Name)
            .ToList();

        var chosenName = await Shell.Current.DisplayActionSheet(
            "Send this note to which section?",
            "Cancel",
            null,
            orderedSections.Select(section => section.Name).ToArray());

        if (string.IsNullOrWhiteSpace(chosenName) || chosenName == "Cancel")
            return null;

        return orderedSections.FirstOrDefault(section =>
            string.Equals(section.Name, chosenName, StringComparison.Ordinal));
    }

    private async Task<List<Section>> GetJoinedSectionsAsync()
    {
        try
        {
            var sections = await _sectionCloudService.GetJoinedSectionsAsync();
            foreach (var section in sections)
                await _databaseService.SaveSectionAsync(section);

            return sections;
        }
        catch
        {
            return await _databaseService.GetSectionsAsync();
        }
    }

    private async Task ClearPagesAsync()
    {
        if (CapturedPages.Count == 0)
            return;

        bool shouldClear = await Shell.Current.DisplayAlert(
            "Clear pages",
            "Remove the pages you have already added to this note?",
            "Clear",
            "Cancel");

        if (shouldClear)
            ResetCaptureSession();
    }

    private async Task<string> SavePhotoAsync(FileResult photo)
    {
        var fileName = $"note_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
        var localPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        await using var sourceStream = await photo.OpenReadAsync();
        await using var localStream = File.OpenWrite(localPath);
        await sourceStream.CopyToAsync(localStream);

        return localPath;
    }

    private async Task<bool> ShouldContinueWithImageAsync(string localPath, string cancelButtonText)
    {
        var warning = await _ocrService.AnalyzeImageQualityAsync(localPath);
        if (string.IsNullOrWhiteSpace(warning))
            return true;

        return await Shell.Current.DisplayAlert(
            "Image Quality Warning",
            $"{warning}\n\nYou can continue, but the OCR result may need more cleanup.",
            "Continue",
            cancelButtonText);
    }

    private void AddCapturedPage(string localPath)
    {
        CapturedPages.Add(new CapturedPageItem
        {
            ImagePath = localPath
        });
        PreviewSource = ImageSource.FromFile(localPath);
        NotifyCaptureStateChanged();
    }

    private void ResetCaptureSession()
    {
        CapturedPages.Clear();
        PreviewSource = "camera_icon.png";
        NotifyCaptureStateChanged();
    }

    private void NotifyCaptureStateChanged()
    {
        for (int index = 0; index < CapturedPages.Count; index++)
            CapturedPages[index].PageLabel = $"Page {index + 1}";

        OnPropertyChanged(nameof(HasCapturedPages));
        OnPropertyChanged(nameof(PageCountText));
        OnPropertyChanged(nameof(ProcessButtonText));
        OnPropertyChanged(nameof(CapturedPages));
    }

    private void PreviewPage(CapturedPageItem? page)
    {
        if (page == null || string.IsNullOrWhiteSpace(page.ImagePath))
            return;

        PreviewSource = ImageSource.FromFile(page.ImagePath);
    }

    private void MovePageLeft(CapturedPageItem? page)
    {
        MovePage(page, -1);
    }

    private void MovePageRight(CapturedPageItem? page)
    {
        MovePage(page, 1);
    }

    private void MovePage(CapturedPageItem? page, int direction)
    {
        if (page == null)
            return;

        int currentIndex = CapturedPages.IndexOf(page);
        if (currentIndex < 0)
            return;

        int targetIndex = currentIndex + direction;
        if (targetIndex < 0 || targetIndex >= CapturedPages.Count)
            return;

        CapturedPages.Move(currentIndex, targetIndex);
        PreviewSource = ImageSource.FromFile(page.ImagePath);
        NotifyCaptureStateChanged();
    }

    private void RemovePage(CapturedPageItem? page)
    {
        if (page == null)
            return;

        if (!CapturedPages.Remove(page))
            return;

        PreviewSource = CapturedPages.Count > 0
            ? ImageSource.FromFile(CapturedPages.Last().ImagePath)
            : "camera_icon.png";
        NotifyCaptureStateChanged();
    }
}
