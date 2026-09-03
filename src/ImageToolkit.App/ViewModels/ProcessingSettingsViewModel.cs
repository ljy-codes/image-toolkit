using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToolkit.App.Models;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.App.ViewModels;

public sealed partial class ProcessingSettingsViewModel : ObservableObject
{
    public IReadOnlyList<ChoiceOption<OutputImageFormat>> OutputFormats { get; } =
    [
        new(OutputImageFormat.Original, "保持原格式"),
        new(OutputImageFormat.Jpeg, "JPEG"),
        new(OutputImageFormat.Png, "PNG"),
        new(OutputImageFormat.Webp, "WebP"),
        new(OutputImageFormat.Bmp, "BMP")
    ];

    public IReadOnlyList<ChoiceOption<OutputMode>> OutputModes { get; } =
    [
        new(OutputMode.SourceDirectory, "原目录新文件"),
        new(OutputMode.OverwriteOriginal, "覆盖原文件"),
        new(OutputMode.SpecificDirectory, "指定目录")
    ];

    public IReadOnlyList<ChoiceOption<AspectRatioMode>> AspectRatioModes { get; } =
    [
        new(AspectRatioMode.Original, "保持原比例"),
        new(AspectRatioMode.Crop, "裁剪到比例"),
        new(AspectRatioMode.Canvas, "补边到比例")
    ];

    public IReadOnlyList<ChoiceOption<CropAnchor>> CropAnchors { get; } =
    [
        new(CropAnchor.Center, "居中"),
        new(CropAnchor.Top, "顶部"),
        new(CropAnchor.Bottom, "底部"),
        new(CropAnchor.Left, "左侧"),
        new(CropAnchor.Right, "右侧")
    ];

    public IReadOnlyList<ChoiceOption<BackgroundMode>> BackgroundModes { get; } =
    [
        new(BackgroundMode.Preserve, "保持原背景"),
        new(BackgroundMode.White, "白色"),
        new(BackgroundMode.Black, "黑色"),
        new(BackgroundMode.Transparent, "透明"),
        new(BackgroundMode.Custom, "自定义颜色")
    ];

    public IReadOnlyList<ChoiceOption<BackgroundRemovalMode>> BackgroundRemovalModes { get; } =
    [
        new(BackgroundRemovalMode.Disabled, "关闭"),
        new(BackgroundRemovalMode.Automatic, "自动（高精度通用）"),
        new(BackgroundRemovalMode.Portrait, "高精度人像"),
        new(BackgroundRemovalMode.GeneralObject, "高精度通用主体")
    ];

    [ObservableProperty]
    private bool _compressionEnabled = true;

    [ObservableProperty]
    private double _targetMegabytes = 1;

    [ObservableProperty]
    private int _minimumJpegQuality = 45;

    [ObservableProperty]
    private int _minimumWebpQuality = 45;

    [ObservableProperty]
    private bool _resizeEnabled;

    [ObservableProperty]
    private int? _width;

    [ObservableProperty]
    private int? _height;

    [ObservableProperty]
    private bool _lockAspectRatio = true;

    [ObservableProperty]
    private AspectRatioMode _aspectRatioMode = AspectRatioMode.Original;

    [ObservableProperty]
    private int _ratioWidth = 1;

    [ObservableProperty]
    private int _ratioHeight = 1;

    [ObservableProperty]
    private CropAnchor _cropAnchor = CropAnchor.Center;

    [ObservableProperty]
    private BackgroundRemovalMode _backgroundRemovalMode =
        BackgroundRemovalMode.Disabled;

    [ObservableProperty]
    private BackgroundMode _backgroundMode = BackgroundMode.Preserve;

    [ObservableProperty]
    private string _customBackgroundColor = "#FFFFFF";

    [ObservableProperty]
    private OutputImageFormat _outputFormat = OutputImageFormat.Original;

    [ObservableProperty]
    private OutputMode _outputMode = OutputMode.SourceDirectory;

    [ObservableProperty]
    private string? _outputDirectory;

    [ObservableProperty]
    private bool _preserveExif = true;

    [ObservableProperty]
    private bool _preserveGps;

    [ObservableProperty]
    private bool _preserveIccProfile = true;

    [ObservableProperty]
    private bool _convertToSrgbWhenIccCannotBePreserved = true;

    [ObservableProperty]
    private bool _allowPngQuantization;

    [ObservableProperty]
    private string? _notice;

    public void Apply(ProcessingRequest request)
    {
        CompressionEnabled = request.Compression.Enabled;
        TargetMegabytes = request.Compression.TargetBytes / 1024d / 1024d;
        MinimumJpegQuality = request.Compression.MinimumJpegQuality;
        MinimumWebpQuality = request.Compression.MinimumWebpQuality;
        AllowPngQuantization = request.Compression.AllowPngQuantization;
        ResizeEnabled = request.Resize.Enabled;
        Width = request.Resize.Width;
        Height = request.Resize.Height;
        LockAspectRatio = request.Resize.LockAspectRatio;
        AspectRatioMode = request.AspectRatio.Mode;
        RatioWidth = request.AspectRatio.RatioWidth;
        RatioHeight = request.AspectRatio.RatioHeight;
        CropAnchor = request.AspectRatio.CropAnchor;
        BackgroundRemovalMode =
            request.AiBackgroundRemoval?.Mode ??
            Domain.Enums.BackgroundRemovalMode.Disabled;
        BackgroundMode = request.Background.Mode;
        CustomBackgroundColor = request.Background.CustomColor;
        PreserveExif = request.Metadata.PreserveExif;
        PreserveGps = request.Metadata.PreserveGps;
        PreserveIccProfile = request.Metadata.PreserveIccProfile;
        ConvertToSrgbWhenIccCannotBePreserved =
            request.Metadata.ConvertToSrgbWhenIccCannotBePreserved;
        OutputFormat = request.Output.Format;
        OutputMode = request.Output.Mode;
        OutputDirectory = request.Output.DirectoryPath;
    }

    public ProcessingRequest BuildRequest()
    {
        var defaults = ProcessingRequest.Default;
        return defaults with
        {
            Compression = defaults.Compression with
            {
                Enabled = CompressionEnabled,
                TargetBytes = Math.Max(
                    1,
                    (long)Math.Round(TargetMegabytes * 1024 * 1024)),
                MinimumJpegQuality = MinimumJpegQuality,
                MinimumWebpQuality = MinimumWebpQuality,
                AllowPngQuantization = AllowPngQuantization
            },
            Resize = defaults.Resize with
            {
                Enabled = ResizeEnabled,
                Width = Width,
                Height = Height,
                LockAspectRatio = LockAspectRatio
            },
            AspectRatio = defaults.AspectRatio with
            {
                Mode = AspectRatioMode,
                RatioWidth = RatioWidth,
                RatioHeight = RatioHeight,
                CropAnchor = CropAnchor
            },
            AiBackgroundRemoval = defaults.AiBackgroundRemoval with
            {
                Mode = BackgroundRemovalMode
            },
            Background = defaults.Background with
            {
                Mode = BackgroundMode,
                CustomColor = CustomBackgroundColor
            },
            Metadata = defaults.Metadata with
            {
                PreserveExif = PreserveExif,
                PreserveGps = PreserveGps,
                PreserveIccProfile = PreserveIccProfile,
                ConvertToSrgbWhenIccCannotBePreserved =
                    ConvertToSrgbWhenIccCannotBePreserved
            },
            Output = defaults.Output with
            {
                Format = OutputFormat,
                Mode = OutputMode,
                DirectoryPath = OutputDirectory
            }
        };
    }

    [RelayCommand]
    public void ResetToDefaults() => Apply(ProcessingRequest.Default);

    partial void OnBackgroundModeChanged(BackgroundMode value) =>
        EnsureTransparentOutputCompatibility();

    partial void OnBackgroundRemovalModeChanged(BackgroundRemovalMode value)
    {
        if (value != Domain.Enums.BackgroundRemovalMode.Disabled)
        {
            if (OutputMode == OutputMode.OverwriteOriginal)
            {
                OutputMode = OutputMode.SourceDirectory;
            }

            if (BackgroundMode == Domain.Enums.BackgroundMode.Preserve)
            {
                BackgroundMode = Domain.Enums.BackgroundMode.Transparent;
            }

            if (BackgroundMode == Domain.Enums.BackgroundMode.Transparent &&
                OutputFormat is OutputImageFormat.Original or OutputImageFormat.Jpeg)
            {
                OutputFormat = OutputImageFormat.Png;
            }

            Notice = BackgroundMode == Domain.Enums.BackgroundMode.Transparent
                ? "AI 抠图已默认切换为透明 PNG 新文件；也可以改选白色、黑色或自定义实色背景。"
                : "AI 抠图已启用，将使用所选实色背景生成新文件。";
            return;
        }

        EnsureTransparentOutputCompatibility();
    }

    partial void OnOutputFormatChanged(OutputImageFormat value) =>
        EnsureTransparentOutputCompatibility();

    partial void OnOutputModeChanged(OutputMode value) =>
        EnsureTransparentOutputCompatibility();

    private void EnsureTransparentOutputCompatibility()
    {
        if (OutputMode == OutputMode.OverwriteOriginal &&
            BackgroundMode == Domain.Enums.BackgroundMode.Transparent)
        {
            OutputMode = OutputMode.SourceDirectory;
            OutputFormat = OutputImageFormat.Png;
            Notice = "透明背景不能安全覆盖原格式，已切换为原目录新文件和 PNG。";
            return;
        }

        if (OutputMode == OutputMode.OverwriteOriginal &&
            OutputFormat != OutputImageFormat.Original)
        {
            OutputFormat = OutputImageFormat.Original;
            Notice = "覆盖原文件时必须保持原格式。";
            return;
        }

        if (BackgroundMode == Domain.Enums.BackgroundMode.Transparent &&
            OutputFormat is OutputImageFormat.Original or OutputImageFormat.Jpeg)
        {
            OutputFormat = OutputImageFormat.Png;
            Notice = "透明背景需要明确使用 PNG，已自动切换输出格式。";
        }
        else
        {
            Notice = null;
        }
    }
}
