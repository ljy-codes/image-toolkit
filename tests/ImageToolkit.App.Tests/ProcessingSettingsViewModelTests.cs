using ImageToolkit.App.ViewModels;
using ImageToolkit.App.Models;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

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
    public void Build_request_preserves_minimum_lossy_quality()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            MinimumJpegQuality = 38,
            MinimumWebpQuality = 41
        };

        var request = viewModel.BuildRequest();

        Assert.Equal(38, request.Compression.MinimumJpegQuality);
        Assert.Equal(41, request.Compression.MinimumWebpQuality);
    }

    [Fact]
    public void Apply_restores_minimum_lossy_quality()
    {
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                MinimumJpegQuality = 36,
                MinimumWebpQuality = 39
            }
        };
        var viewModel = new ProcessingSettingsViewModel();

        viewModel.Apply(request);

        Assert.Equal(36, viewModel.MinimumJpegQuality);
        Assert.Equal(39, viewModel.MinimumWebpQuality);
    }

    [Fact]
    public void Apply_and_build_preserve_srgb_fallback_preference()
    {
        var request = ProcessingRequest.Default with
        {
            Metadata = ProcessingRequest.Default.Metadata with
            {
                ConvertToSrgbWhenIccCannotBePreserved = false
            }
        };
        var viewModel = new ProcessingSettingsViewModel();

        viewModel.Apply(request);
        var rebuilt = viewModel.BuildRequest();

        Assert.False(viewModel.ConvertToSrgbWhenIccCannotBePreserved);
        Assert.False(rebuilt.Metadata.ConvertToSrgbWhenIccCannotBePreserved);
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

    [Fact]
    public void Ai_cutout_allows_solid_background_after_it_is_enabled()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            BackgroundRemovalMode = BackgroundRemovalMode.GeneralObject
        };

        viewModel.BackgroundMode = BackgroundMode.White;
        viewModel.OutputFormat = OutputImageFormat.Jpeg;

        Assert.Equal(BackgroundMode.White, viewModel.BackgroundMode);
        Assert.Equal(OutputImageFormat.Jpeg, viewModel.OutputFormat);
    }

    [Fact]
    public void Enabling_ai_defaults_to_transparent_png_new_file()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            OutputMode = OutputMode.OverwriteOriginal,
            OutputFormat = OutputImageFormat.Original,
            BackgroundMode = BackgroundMode.Preserve
        };

        viewModel.BackgroundRemovalMode = BackgroundRemovalMode.Portrait;

        Assert.Equal(OutputMode.SourceDirectory, viewModel.OutputMode);
        Assert.Equal(OutputImageFormat.Png, viewModel.OutputFormat);
        Assert.Equal(BackgroundMode.Transparent, viewModel.BackgroundMode);
    }

    [Fact]
    public void Reset_to_defaults_restores_optional_resize_and_default_output()
    {
        var viewModel = new ProcessingSettingsViewModel
        {
            ResizeEnabled = true,
            Width = 1200,
            Height = 900,
            OutputFormat = OutputImageFormat.Webp,
            TargetMegabytes = 3
        };

        viewModel.ResetToDefaults();

        Assert.False(viewModel.ResizeEnabled);
        Assert.Null(viewModel.Width);
        Assert.Null(viewModel.Height);
        Assert.Equal(OutputImageFormat.Original, viewModel.OutputFormat);
        Assert.Equal(1, viewModel.TargetMegabytes);
    }

    [Fact]
    public void Apply_legacy_request_without_ai_options_uses_disabled_mode()
    {
        var legacyRequest = ProcessingRequest.Default with
        {
            AiBackgroundRemoval = null!
        };
        var viewModel = new ProcessingSettingsViewModel
        {
            BackgroundRemovalMode = BackgroundRemovalMode.Portrait
        };

        viewModel.Apply(legacyRequest);

        Assert.Equal(
            BackgroundRemovalMode.Disabled,
            viewModel.BackgroundRemovalMode);
    }

    [Fact]
    public void Choice_option_displays_its_label()
    {
        var option = new ChoiceOption<OutputImageFormat>(
            OutputImageFormat.Png,
            "PNG");

        Assert.Equal("PNG", option.ToString());
    }
}
