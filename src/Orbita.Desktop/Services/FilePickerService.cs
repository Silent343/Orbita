using Microsoft.UI.Xaml;
using Orbita.Files.Domain;
using Windows.Storage.Pickers;

namespace Orbita.Desktop.Services;

public sealed class FilePickerService(Window window)
{
    public async Task<string?> PickFileAsync()
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        picker.FileTypeFilter.Add("*");
        return (await picker.PickSingleFileAsync())?.Path;
    }

    public async Task<IReadOnlyList<string>> PickImagesAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        foreach (var extension in FileAsset.ImageExtensions) picker.FileTypeFilter.Add(extension);
        return (await picker.PickMultipleFilesAsync()).Select(file => file.Path).ToArray();
    }

    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        picker.FileTypeFilter.Add("*");
        return (await picker.PickSingleFolderAsync())?.Path;
    }
}
