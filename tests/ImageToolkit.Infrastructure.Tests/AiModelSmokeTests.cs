using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Infrastructure.AI;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class AiModelSmokeTests
{
    [Theory]
    [InlineData(BackgroundRemovalMode.Portrait)]
    [InlineData(BackgroundRemovalMode.GeneralObject)]
    public async Task Installed_model_produces_transparent_png(
        BackgroundRemovalMode mode)
    {
        var modelDirectory = Environment.GetEnvironmentVariable(
            "IMAGETOOLKIT_AI_MODEL_DIR");
        if (string.IsNullOrWhiteSpace(modelDirectory))
        {
            return;
        }

        using var client = new HttpClient();
        var manager = new LocalAiModelManager(client, modelDirectory);
        var engine = new OnnxBackgroundRemovalEngine(manager);
        using var source = new MagickImage(MagickColors.White, 640, 480);
        using (var foreground = new MagickImage(MagickColors.CornflowerBlue, 260, 320))
        {
            source.Composite(foreground, Gravity.Center, CompositeOperator.Over);
        }

        source.Format = MagickFormat.Png;
        using var input = new MemoryStream();
        source.Write(input);
        input.Position = 0;
        using var output = new MemoryStream();

        await engine.RemoveBackgroundAsync(
            input,
            output,
            mode,
            CancellationToken.None);

        output.Position = 0;
        using var result = new MagickImage(output);
        Assert.Equal(MagickFormat.Png, result.Format);
        Assert.True(result.HasAlpha);
        Assert.Equal(640u, result.Width);
        Assert.Equal(480u, result.Height);
    }
}
