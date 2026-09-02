using ImageToolkit.App.ViewModels;
using ImageToolkit.Domain.Enums;

namespace ImageToolkit.App.Tests;

public sealed class ProcessingSettingsViewModelTests
{
    [Fact]
    public void Transparent_jpeg_draft_switches_to_png_with_notice()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            OutputFormat = OutputImageFormat.Jpeg
        };

        viewModel.BackgroundMode = BackgroundMode.Transparent;

        Assert.Equal(OutputImageFormat.Png, viewModel.OutputFormat);
        Assert.NotNull(viewModel.Notice);
    }

    [Fact]
    public void Build_request_preserves_manual_dimensions()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            ResizeEnabled = true,
            Width = 1200,
            Height = 900,
            LockAspectRatio = false
        };

        var request = viewModel.BuildRequest();

        Assert.True(request.Resize.Enabled);
        Assert.Equal(1200, request.Resize.Width);
        Assert.Equal(900, request.Resize.Height);
        Assert.False(request.Resize.LockAspectRatio);
    }

    [Fact]
    public void Overwrite_mode_forces_original_format()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            OutputFormat = OutputImageFormat.Png
        };

        viewModel.OutputMode = OutputMode.OverwriteOriginal;

        Assert.Equal(OutputImageFormat.Original, viewModel.OutputFormat);
        Assert.NotNull(viewModel.Notice);
    }

    [Fact]
    public void Transparent_background_leaves_overwrite_mode()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            OutputMode = OutputMode.OverwriteOriginal
        };

        viewModel.BackgroundMode = BackgroundMode.Transparent;

        Assert.Equal(OutputMode.SourceDirectory, viewModel.OutputMode);
        Assert.Equal(OutputImageFormat.Png, viewModel.OutputFormat);
        Assert.NotNull(viewModel.Notice);
    }
}
