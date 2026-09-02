using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageToolkit.App.Models;

public sealed partial class ImageQueueItemViewData : ObservableObject
{
    public ImageQueueItemViewData(
        string sourcePath,
        string fileName,
        string dimensions,
        string sizeText)
    {
        SourcePath = sourcePath;
        FileName = fileName;
        Dimensions = dimensions;
        SizeText = sizeText;
    }

    public string SourcePath { get; }

    public string FileName { get; }

    public string Dimensions { get; }

    public string SizeText { get; }

    [ObservableProperty]
    private string _status = "等待";

    [ObservableProperty]
    private string? _outputPath;
}
