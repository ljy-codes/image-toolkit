using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Domain.Tests;

public sealed class ProcessingRequestTests
{
    [Fact]
    public void Defaults_match_product_safety_boundaries()
    {
        var request = ProcessingRequest.Default;

        Assert.True(request.Compression.Enabled);
        Assert.Equal(1_048_576, request.Compression.TargetBytes);
        Assert.Equal(45, request.Compression.MinimumJpegQuality);
        Assert.Equal(45, request.Compression.MinimumWebpQuality);
        Assert.Equal(0.25, request.Compression.MinimumScaleRatio);
        Assert.Equal(320, request.Compression.MinimumShortEdge);
        Assert.False(request.Compression.AllowPngQuantization);
        Assert.True(request.Metadata.PreserveIccProfile);
        Assert.False(request.Metadata.PreserveGps);
        Assert.Equal(OutputImageFormat.Original, request.Output.Format);
    }

    [Fact]
    public void With_expression_creates_a_new_request()
    {
        var original = ProcessingRequest.Default;
        var changed = original with
        {
            Compression = original.Compression with { TargetBytes = 500_000 }
        };

        Assert.Equal(1_048_576, original.Compression.TargetBytes);
        Assert.Equal(500_000, changed.Compression.TargetBytes);
    }

    [Fact]
    public void Processing_preset_displays_its_name()
    {
        var preset = new ProcessingPreset(
            "电商图片",
            ProcessingRequest.Default);

        Assert.Equal("电商图片", preset.ToString());
    }
}
