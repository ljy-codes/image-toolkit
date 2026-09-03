using Microsoft.Win32;

namespace ImageToolkit.App.Services;

public interface IDesktopFilePicker
{
    Task<IReadOnlyList<string>> PickFilesAsync();

    Task<string?> PickFolderAsync();

    Task<string?> PickConfigurationImportPathAsync();

    Task<string?> PickConfigurationExportPathAsync(string suggestedFileName);

    Task<string?> PickAiModelPathAsync();
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

    public Task<string?> PickConfigurationImportPathAsync()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = false,
            Title = "导入苏影枢配置",
            Filter = "苏影枢配置包|*.syconfig|所有文件|*.*",
            DefaultExt = ".syconfig"
        };
        return Task.FromResult(dialog.ShowDialog() == true
            ? dialog.FileName
            : null);
    }

    public Task<string?> PickConfigurationExportPathAsync(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出苏影枢配置",
            Filter = "苏影枢配置包|*.syconfig",
            DefaultExt = ".syconfig",
            AddExtension = true,
            FileName = suggestedFileName,
            OverwritePrompt = true
        };
        return Task.FromResult(dialog.ShowDialog() == true
            ? dialog.FileName
            : null);
    }

    public Task<string?> PickAiModelPathAsync()
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = false,
            Title = "选择本地 AI 模型",
            Filter = "ONNX 模型|*.onnx|所有文件|*.*",
            DefaultExt = ".onnx"
        };
        return Task.FromResult(dialog.ShowDialog() == true
            ? dialog.FileName
            : null);
    }
}
