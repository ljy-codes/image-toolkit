using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class MagickPreviewRendererTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Ai_enabled_preview_uses_background_removal_engine()
    {
        var source = TestImages.CreateJpeg(
            _directory,
            width: 640,
            height: 480);
        var engine = new RecordingBackgroundRemovalEngine();
        var renderer = new MagickPreviewRenderer(engine);
        var request = ProcessingRequest.Default with
        {
            AiBackgroundRemoval = ProcessingRequest.Default.AiBackgroundRemoval with
            {
                Mode = BackgroundRemovalMode.Portrait
            }
        };

        var preview = await renderer.RenderAsync(
            source,
            request,
            320,
            240,
            CancellationToken.None);

        Assert.Equal(1, engine.CallCount);
        Assert.Equal(BackgroundRemovalMode.Portrait, engine.LastMode);
        Assert.Equal(new PixelSize(320, 240), preview.Size);
        using var image = new MagickImage(preview.Bytes);
        Assert.Equal(MagickFormat.Png, image.Format);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private sealed class RecordingBackgroundRemovalEngine :
        IBackgroundRemovalEngine
    {
        public int CallCount { get; private set; }

        public BackgroundRemovalMode LastMode { get; private set; }

        public async Task RemoveBackgroundAsync(
            Stream input,
            Stream output,
            BackgroundRemovalMode mode,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastMode = mode;
            await input.CopyToAsync(output, cancellationToken);
        }
    }
}
