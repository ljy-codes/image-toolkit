using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class CompressionSearchServiceTests
{
    [Fact]
    public async Task Selects_highest_quality_not_exceeding_target()
    {
        var service = new CompressionSearchService();

        var result = await service.FindQualityAsync(
            45,
            95,
            800,
            quality => Task.FromResult((long)quality * 10),
            CancellationToken.None);

        Assert.True(result.ReachedTarget);
        Assert.Equal(80, result.Quality);
        Assert.Equal(800, result.SizeBytes);
    }

    [Fact]
    public async Task Reports_unmet_when_minimum_quality_is_too_large()
    {
        var service = new CompressionSearchService();

        var result = await service.FindQualityAsync(
            45,
            95,
            400,
            quality => Task.FromResult((long)quality * 10),
            CancellationToken.None);

        Assert.False(result.ReachedTarget);
        Assert.Equal(45, result.Quality);
        Assert.Equal(450, result.SizeBytes);
    }

    [Fact]
    public void Resize_candidates_preserve_ratio_and_boundaries()
    {
        var candidates = CompressionSearchService.BuildResizeCandidates(
            new PixelSize(4000, 3000),
            ProcessingRequest.Default.Compression);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, size =>
        {
            Assert.True(size.Width >= 1000);
            Assert.True(size.Height >= 750);
            Assert.True(size.ShortEdge >= 320);
            Assert.InRange((double)size.Width / size.Height, 1.332, 1.334);
        });
        Assert.Equal(new PixelSize(1000, 750), candidates[^1]);
    }

    [Fact]
    public void Image_below_short_edge_limit_is_not_resized()
    {
        var candidates = CompressionSearchService.BuildResizeCandidates(
            new PixelSize(500, 300),
            ProcessingRequest.Default.Compression);

        Assert.Empty(candidates);
    }

    [Fact]
    public void Manual_resize_disables_automatic_resize()
    {
        var request = ProcessingRequest.Default with
        {
            Resize = ProcessingRequest.Default.Resize with
            {
                Enabled = true,
                Width = 1200,
                Height = 900
            }
        };

        Assert.False(CompressionSearchService.CanAutomaticallyResize(request));
    }
}
