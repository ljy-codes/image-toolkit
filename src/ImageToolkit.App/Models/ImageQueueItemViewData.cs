using CommunityToolkit.Mvvm.ComponentModel;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.App.Models;

public sealed partial class ImageQueueItemViewData : ObservableObject
{
    public ImageQueueItemViewData(
        string sourcePath,
        string fileName,
        string dimensions,
        string sizeText,
        ImageImportEntry? importEntry = null)
    {
        SourcePath = sourcePath;
        FileName = fileName;
        Dimensions = dimensions;
        SizeText = sizeText;
        ImportEntry = importEntry ?? ImageImportEntry.FromFile(sourcePath);
    }

    public string SourcePath { get; }

    public string FileName { get; }

    public string Dimensions { get; }

    public string SizeText { get; }

    public ImageImportEntry ImportEntry { get; }

    [ObservableProperty]
    private string _status = "等待";

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    private string? _resultDetails;

    [ObservableProperty]
    private string? _message;
}
