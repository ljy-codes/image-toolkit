using System.Reflection;
using ImageMagick;
using ImageToolkit.Infrastructure.AI;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class OnnxBackgroundRemovalEngineTests
{
    [Fact]
    public void Mask_postprocessing_removes_isolated_foreground_speckle()
    {
        using var image = new MagickImage(MagickColors.White, 80, 80);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 8, 8 });
        for (var y = 2; y <= 5; y++)
        {
            for (var x = 2; x <= 5; x++)
            {
                tensor[0, 0, y, x] = 1f;
            }
        }

        tensor[0, 0, 0, 0] = 1f;

        ApplyMask(image, tensor);

        var alpha = ReadAlpha(image);
        Assert.InRange(alpha[5 * 80 + 5], 0, 16);
        Assert.InRange(alpha[40 * 80 + 40], 239, 255);
    }

    [Fact]
    public void Mask_postprocessing_removes_annotation_sized_component()
    {
        using var image = new MagickImage(MagickColors.White, 200, 200);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 200, 200 });
        for (var y = 70; y < 130; y++)
        {
            for (var x = 70; x < 130; x++)
            {
                tensor[0, 0, y, x] = 1f;
            }
        }

        tensor[0, 0, 10, 10] = 1f;
        tensor[0, 0, 10, 11] = 1f;
        tensor[0, 0, 11, 10] = 1f;

        ApplyMask(image, tensor);

        var alpha = ReadAlpha(image);
        Assert.InRange(alpha[10 * 200 + 10], 0, 16);
        Assert.InRange(alpha[100 * 200 + 100], 239, 255);
    }

    [Fact]
    public void Mask_postprocessing_fills_small_transparent_hole()
    {
        using var image = new MagickImage(MagickColors.White, 80, 80);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 8, 8 });
        tensor.Fill(1f);
        for (var y = 0; y < 8; y++)
        {
            tensor[0, 0, y, 0] = 0f;
        }

        tensor[0, 0, 4, 4] = 0f;

        ApplyMask(image, tensor);

        var alpha = ReadAlpha(image);
        Assert.InRange(alpha[45 * 80 + 45], 239, 255);
    }

    [Fact]
    public void Mask_is_stretched_to_non_square_image_bounds()
    {
        using var image = new MagickImage(MagickColors.White, 200, 100);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 320, 320 });
        tensor.Fill(1f);
        for (var y = 0; y < 320; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                tensor[0, 0, y, x] = 0f;
            }
        }

        ApplyMask(image, tensor);

        var alpha = ReadAlpha(image);
        var rightHalfOpaqueRatio = alpha
            .Where((_, index) => index % 200 >= 100)
            .Count(value => value >= 239) / 10_000d;

        Assert.True(
            rightHalfOpaqueRatio >= 0.99,
            $"遮罩未覆盖原图右半区，右半区不透明比例仅为 {rightHalfOpaqueRatio:P2}。");
    }

    [Fact]
    public void Soft_edge_color_is_pulled_toward_nearby_opaque_foreground()
    {
        var rgba = new byte[]
        {
            255, 255, 255, 0,
            255, 255, 255, 128,
            20, 20, 20, 255,
            20, 20, 20, 255,
            20, 20, 20, 255
        };
        using var image = new MagickImage(
            rgba,
            new PixelReadSettings(
                5,
                1,
                StorageType.Char,
                PixelMapping.RGBA));
        var method = typeof(OnnxBackgroundRemovalEngine).GetMethod(
            "DecontaminateSoftEdges",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method.Invoke(null, [image]);

        var result = image.GetPixels().ToByteArray(PixelMapping.RGBA)!;
        Assert.InRange(result[4], 20, 180);
        Assert.Equal(128, result[7]);
        Assert.Equal(20, result[8]);
    }

    [Fact]
    public void Degenerate_mask_without_confident_foreground_is_rejected()
    {
        using var image = new MagickImage(MagickColors.White, 80, 80);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 20, 20 });
        for (var index = 0; index < 400; index++)
        {
            tensor.Buffer.Span[index] = index == 200 ? 1f : 0.1f;
        }

        var exception = Assert.Throws<TargetInvocationException>(
            () => ApplyMask(image, tensor));
        var inner = Assert.IsType<InvalidDataException>(
            exception.InnerException);
        Assert.Contains("未识别到明确主体", inner.Message);
    }

    private static void ApplyMask(MagickImage image, DenseTensor<float> tensor)
    {
        var applyMask = typeof(OnnxBackgroundRemovalEngine).GetMethod(
            "ApplyMask",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(applyMask);

        applyMask.Invoke(null, [image, tensor]);
    }

    private static byte[] ReadAlpha(MagickImage image) =>
        image.GetPixels().ToByteArray(PixelMapping.RGBA)!
            .Where((_, index) => index % 4 == 3)
            .ToArray();
}
