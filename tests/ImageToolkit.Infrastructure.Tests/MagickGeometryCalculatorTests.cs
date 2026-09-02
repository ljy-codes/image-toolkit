using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class MagickGeometryCalculatorTests
{
    [Fact]
    public void Center_crop_converts_4_by_3_to_square()
    {
        var crop = MagickGeometryCalculator.CalculateCrop(
            new PixelSize(4000, 3000),
            1,
            1,
            CropAnchor.Center);

        Assert.Equal(new PixelRectangle(500, 0, 3000, 3000), crop);
    }

    [Fact]
    public void Crop_uses_selected_anchor()
    {
        var crop = MagickGeometryCalculator.CalculateCrop(
            new PixelSize(4000, 3000),
            1,
            1,
            CropAnchor.Right);

        Assert.Equal(new PixelRectangle(1000, 0, 3000, 3000), crop);
    }

    [Fact]
    public void Exact_ratio_crop_is_a_no_op()
    {
        var crop = MagickGeometryCalculator.CalculateCrop(
            new PixelSize(1600, 900),
            16,
            9,
            CropAnchor.Bottom);

        Assert.Equal(new PixelRectangle(0, 0, 1600, 900), crop);
    }

    [Fact]
    public void Canvas_expands_square_to_4_by_3()
    {
        var canvas = MagickGeometryCalculator.CalculateCanvas(
            new PixelSize(1000, 1000),
            4,
            3);

        Assert.Equal(new PixelSize(1334, 1000), canvas);
    }

    [Fact]
    public void Width_only_resize_preserves_ratio()
    {
        var result = MagickGeometryCalculator.CalculateResize(
            new PixelSize(4000, 3000),
            new ResizeOptions(true, 1200, null, true, false));

        Assert.Equal(new PixelSize(1200, 900), result);
    }

    [Fact]
    public void Unlocked_resize_allows_stretch()
    {
        var result = MagickGeometryCalculator.CalculateResize(
            new PixelSize(4000, 3000),
            new ResizeOptions(true, 1000, 1000, false, false));

        Assert.Equal(new PixelSize(1000, 1000), result);
    }

    [Fact]
    public void No_upscale_keeps_original_size()
    {
        var result = MagickGeometryCalculator.CalculateResize(
            new PixelSize(800, 600),
            new ResizeOptions(true, 1600, null, true, false));

        Assert.Equal(new PixelSize(800, 600), result);
    }

    [Fact]
    public void Locked_incompatible_dimensions_are_rejected()
    {
        Action action = () =>
        {
            _ = MagickGeometryCalculator.CalculateResize(
                new PixelSize(4000, 3000),
                new ResizeOptions(true, 1000, 1000, true, false));
        };

        Assert.Throws<ArgumentException>(action);
    }
}
