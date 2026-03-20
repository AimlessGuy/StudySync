using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using StudySync.Views;

namespace StudySync.ViewModels;

public partial class CameraViewModel // Note: no longer inherits ObservableObject
{
    private ImageSource _previewSource;
    private bool _isBusy;

    public ImageSource PreviewSource
    {
        get => _previewSource;
        set
        {
            if (_previewSource != value)
            {
                _previewSource = value;
                // Notify property changed - we'll add this later if needed
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                // Notify property changed
            }
        }
    }

    public ICommand TakePhotoCommand { get; }
    public ICommand PickPhotoCommand { get; }

    public CameraViewModel()
    {
        PreviewSource = "camera_icon.png";
        TakePhotoCommand = new AsyncRelayCommand(TakePhotoAsync);
        PickPhotoCommand = new AsyncRelayCommand(PickPhotoAsync);
    }

    private async Task TakePhotoAsync()
    {
        try
        {
            IsBusy = true;

            // Check and request camera permission
            var cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlert("Permission Denied",
                    "Camera permission is required to take photos.", "OK");
                return;
            }

            // Check storage permission (for Android)
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                var storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                if (storageStatus != PermissionStatus.Granted)
                {
                    await Shell.Current.DisplayAlert("Permission Denied",
                        "Storage permission is required to save photos.", "OK");
                    return;
                }
            }

            // Take photo using MediaPicker
            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Capture your notes"
            });

            if (photo != null)
            {
                // Save to app's local directory
                var localPath = await SavePhotoAsync(photo);

                // Display preview
                PreviewSource = ImageSource.FromFile(localPath);

                // Navigate to result page with the image path
                await Shell.Current.GoToAsync($"{nameof(ResultPage)}?imagePath={Uri.EscapeDataString(localPath)}");
            }
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlert("Not Supported",
                "Camera is not supported on this device.", "OK");
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlert("Permission Error",
                "Unable to get camera permission.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error",
                $"Failed to capture photo: {ex.Message}", "OK");
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
                Title = "Choose a photo"
            });

            if (photo != null)
            {
                var localPath = await SavePhotoAsync(photo);
                PreviewSource = ImageSource.FromFile(localPath);

                await Shell.Current.GoToAsync($"{nameof(ResultPage)}?imagePath={Uri.EscapeDataString(localPath)}");
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error",
                $"Failed to pick photo: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> SavePhotoAsync(FileResult photo)
    {
        // Create a unique filename
        var fileName = $"note_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        var localPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        // Copy the photo to app's local storage
        using var sourceStream = await photo.OpenReadAsync();
        using var localStream = File.OpenWrite(localPath);
        await sourceStream.CopyToAsync(localStream);

        return localPath;
    }
}