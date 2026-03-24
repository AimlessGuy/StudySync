using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using StudySync.Views;

namespace StudySync.ViewModels;

public partial class CameraViewModel : ObservableObject  // ← Add ObservableObject
{
    private ImageSource _previewSource;
    private bool _isBusy;

    public ImageSource PreviewSource
    {
        get => _previewSource;
        set => SetProperty(ref _previewSource, value);  // ← Use SetProperty
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);  // ← Use SetProperty
    }

    public ICommand TakePhotoCommand { get; }
    public ICommand PickPhotoCommand { get; }

    public CameraViewModel()
    {
        PreviewSource = "camera_icon.png";
        TakePhotoCommand = new AsyncRelayCommand(TakePhotoAsync);
        PickPhotoCommand = new AsyncRelayCommand(PickPhotoAsync);
    }

    // Rest of your code remains the same...
    private async Task TakePhotoAsync()
    {
        try
        {
            IsBusy = true;
            // ... existing code ...
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
            // ... existing code ...
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> SavePhotoAsync(FileResult photo)
    {
        var fileName = $"note_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        var localPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        using var sourceStream = await photo.OpenReadAsync();
        using var localStream = File.OpenWrite(localPath);
        await sourceStream.CopyToAsync(localStream);

        return localPath;
    }
}