using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Tests;

public sealed class ProcessingRequestValidatorTests
{
    private readonly ProcessingRequestValidator _validator = new();

    [Fact]
    public void Rejects_quality_outside_supported_range()
    {
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                MinimumJpegQuality = 19
            }
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "compression.jpeg-quality-range");
    }

    [Fact]
    public void Accepts_quality_boundaries()
    {
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                MinimumJpegQuality = 20,
                MinimumWebpQuality = 95
            }
        };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_target_dimensions_without_positive_value()
    {
        var request = ProcessingRequest.Default with
        {
            Resize = ProcessingRequest.Default.Resize with
            {
                Enabled = true,
                Width = 0,
                Height = 900
            }
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "resize.width-positive");
    }

    [Fact]
    public void Accepts_one_positive_resize_dimension()
    {
        var request = ProcessingRequest.Default with
        {
            Resize = ProcessingRequest.Default.Resize with
            {
                Enabled = true,
                Width = 1200,
                Height = null
            }
        };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_missing_resize_dimensions()
    {
        var request = ProcessingRequest.Default with
        {
            Resize = ProcessingRequest.Default.Resize with
            {
                Enabled = true,
                Width = null,
                Height = null
            }
        };

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, error => error.Code == "resize.dimension-required");
    }

    [Fact]
    public void Accepts_default_compression_boundaries()
    {
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                MinimumScaleRatio = 0.25,
                MinimumShortEdge = 320
            }
        };

        Assert.True(_validator.Validate(request).IsValid);
    }

    [Fact]
    public void Rejects_invalid_custom_aspect_ratio()
    {
        var request = ProcessingRequest.Default with
        {
            AspectRatio = ProcessingRequest.Default.AspectRatio with
            {
                Mode = AspectRatioMode.Crop,
                RatioWidth = 0,
                RatioHeight = 3
            }
        };

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, error => error.Code == "aspect-ratio.width-positive");
    }

    [Fact]
    public void Rejects_empty_output_suffix()
    {
        var request = ProcessingRequest.Default with
        {
            Output = ProcessingRequest.Default.Output with { FileNameSuffix = " " }
        };

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, error => error.Code == "output.suffix-required");
    }

    [Fact]
    public void Rejects_missing_specific_output_directory()
    {
        var request = ProcessingRequest.Default with
        {
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = null
            }
        };

        var result = _validator.Validate(request);

        Assert.Contains(result.Errors, error => error.Code == "output.directory-required");
    }
}
