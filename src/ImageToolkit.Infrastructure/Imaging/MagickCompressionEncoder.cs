using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class MagickCompressionEncoder
{
    private readonly CompressionSearchService _search = new();

    public async Task<EncodedImageData> EncodeAsync(
        MagickImage transformed,
        OutputImageFormat format,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transformed);
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Compression.Enabled)
        {
            var bytes = Encode(transformed, format, 92, false);
            return new EncodedImageData(
                bytes,
                CurrentSize(transformed),
                format is OutputImageFormat.Jpeg or OutputImageFormat.Webp ? 92 : null,
                true,
                false,
                false);
        }

        return format switch
        {
            OutputImageFormat.Jpeg => await EncodeLossyAsync(
                transformed,
                format,
                request.Compression.MinimumJpegQuality,
                request,
                cancellationToken).ConfigureAwait(false),
            OutputImageFormat.Webp => await EncodeLossyAsync(
                transformed,
                format,
                request.Compression.MinimumWebpQuality,
                request,
                cancellationToken).ConfigureAwait(false),
            OutputImageFormat.Png => EncodePng(transformed, request, cancellationToken),
            _ => EncodeDirect(transformed, format, request)
        };
    }

    private async Task<EncodedImageData> EncodeLossyAsync(
        MagickImage transformed,
        OutputImageFormat format,
        int minimumQuality,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var originalSize = CurrentSize(transformed);
        var decision = await SearchAsync(transformed, format, minimumQuality)
            .ConfigureAwait(false);
        var bytes = Encode(transformed, format, decision.Quality, false);
        var finalSize = originalSize;
        var usedAutomaticResize = false;

        if (!decision.ReachedTarget &&
            CompressionSearchService.CanAutomaticallyResize(request))
        {
            foreach (var candidate in CompressionSearchService.BuildResizeCandidates(
                         originalSize,
                         request.Compression))
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var resized = new MagickImage(transformed);
                resized.Resize((uint)candidate.Width, (uint)candidate.Height);
                decision = await SearchAsync(resized, format, minimumQuality)
                    .ConfigureAwait(false);
                bytes = Encode(resized, format, decision.Quality, false);
                finalSize = candidate;
                usedAutomaticResize = true;

                if (decision.ReachedTarget)
                {
                    break;
                }
            }
        }

        return new EncodedImageData(
            bytes,
            finalSize,
            decision.Quality,
            decision.ReachedTarget,
            usedAutomaticResize,
            false);

        Task<CompressionDecision> SearchAsync(
            MagickImage image,
            OutputImageFormat outputFormat,
            int quality) =>
            _search.FindQualityAsync(
                quality,
                95,
                request.Compression.TargetBytes,
                candidate => Task.FromResult(
                    (long)Encode(image, outputFormat, candidate, false).Length),
                cancellationToken);
    }

    private static EncodedImageData EncodePng(
        MagickImage transformed,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var originalSize = CurrentSize(transformed);
        var best = EncodePngCandidate(transformed, request, cancellationToken);
        if (best.ReachedTarget ||
            !CompressionSearchService.CanAutomaticallyResize(request))
        {
            return best;
        }

        foreach (var candidate in CompressionSearchService.BuildResizeCandidates(
                     originalSize,
                     request.Compression))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var resized = new MagickImage(transformed);
            resized.Resize((uint)candidate.Width, (uint)candidate.Height);
            best = EncodePngCandidate(resized, request, cancellationToken) with
            {
                FinalSize = candidate,
                UsedAutomaticResize = true
            };
            if (best.ReachedTarget)
            {
                break;
            }
        }

        return best;
    }

    private static EncodedImageData EncodePngCandidate(
        MagickImage image,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lossless = Encode(image, OutputImageFormat.Png, null, false);
        if (lossless.LongLength <= request.Compression.TargetBytes)
        {
            return new EncodedImageData(
                lossless,
                CurrentSize(image),
                null,
                true,
                false,
                false);
        }

        if (!request.Compression.AllowPngQuantization)
        {
            return new EncodedImageData(
                lossless,
                CurrentSize(image),
                null,
                false,
                false,
                false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var quantized = Encode(image, OutputImageFormat.Png, null, true);
        return new EncodedImageData(
            quantized.LongLength < lossless.LongLength ? quantized : lossless,
            CurrentSize(image),
            null,
            quantized.LongLength <= request.Compression.TargetBytes,
            false,
            quantized.LongLength < lossless.LongLength);
    }

    private static EncodedImageData EncodeDirect(
        MagickImage transformed,
        OutputImageFormat format,
        ProcessingRequest request)
    {
        var bytes = Encode(transformed, format, null, false);
        return new EncodedImageData(
            bytes,
            CurrentSize(transformed),
            null,
            bytes.LongLength <= request.Compression.TargetBytes,
            false,
            false);
    }

    private static byte[] Encode(
        MagickImage source,
        OutputImageFormat format,
        int? quality,
        bool quantizePng)
    {
        using var image = new MagickImage(source);
        image.Format = ToMagickFormat(format);

        if (quality is not null)
        {
            image.Quality = (uint)quality.Value;
        }

        if (format == OutputImageFormat.Png && quantizePng)
        {
            image.Quantize(new QuantizeSettings
            {
                Colors = 256,
                DitherMethod = DitherMethod.Riemersma
            });
        }

        using var stream = new MemoryStream();
        image.Write(stream);
        return stream.ToArray();
    }

    private static MagickFormat ToMagickFormat(OutputImageFormat format) =>
        format switch
        {
            OutputImageFormat.Jpeg => MagickFormat.Jpeg,
            OutputImageFormat.Png => MagickFormat.Png,
            OutputImageFormat.Webp => MagickFormat.WebP,
            OutputImageFormat.Bmp => MagickFormat.Bmp,
            OutputImageFormat.Tiff => MagickFormat.Tiff,
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    private static PixelSize CurrentSize(MagickImage image) =>
        new((int)image.Width, (int)image.Height);
}

public sealed record EncodedImageData(
    byte[] Bytes,
    PixelSize FinalSize,
    int? Quality,
    bool ReachedTarget,
    bool UsedAutomaticResize,
    bool UsedPngQuantization);
