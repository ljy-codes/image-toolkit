using Microsoft.Win32;

namespace ImageToolkit.App.Services;

public interface IDesktopFilePicker
{
    Task<IReadOnlyList<string>> PickFilesAsync();

    Task<string?> PickFolderAsync();
}

public sealed class DesktopFilePicker : IDesktopFilePicker
{
    public Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = "选择图片",
            Filter = "支持的图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff|所有文件|*.*"
        };
        IReadOnlyList<string> result = dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
        return Task.FromResult(result);
    }

    public Task<string?> PickFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择图片文件夹",
            Multiselect = false
        };
        return Task.FromResult(dialog.ShowDialog() == true
            ? dialog.FolderName
            : null);
    }
}
