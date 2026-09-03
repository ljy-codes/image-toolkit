using System.Reflection;
using ImageMagick;
using ImageToolkit.Infrastructure.AI;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class OnnxBackgroundRemovalEngineTests
{
    [Fact]
    public void Mask_is_stretched_to_non_square_image_bounds()
    {
        using var image = new MagickImage(MagickColors.White, 200, 100);
        var tensor = new DenseTensor<float>(new[] { 1, 1, 320, 320 });
        tensor.Fill(1f);
        tensor[0, 0, 0, 0] = 0f;

        var applyMask = typeof(OnnxBackgroundRemovalEngine).GetMethod(
            "ApplyMask",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(applyMask);

        applyMask.Invoke(null, [image, tensor]);

        var alpha = image.GetPixels().ToByteArray(PixelMapping.RGBA)!
            .Where((_, index) => index % 4 == 3)
            .ToArray();
        var rightHalfOpaqueRatio = alpha
            .Where((_, index) => index % 200 >= 100)
            .Count(value => value >= 239) / 10_000d;

        Assert.True(
            rightHalfOpaqueRatio >= 0.99,
            $"遮罩未覆盖原图右半区，右半区不透明比例仅为 {rightHalfOpaqueRatio:P2}。");
    }
}
