using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class CompressionSearchService
{
    public async Task<CompressionDecision> FindQualityAsync(
        int minimumQuality,
        int maximumQuality,
        long targetBytes,
        Func<int, Task<long>> probe,
        CancellationToken cancellationToken)
    {
        if (minimumQuality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumQuality));
        }

        if (maximumQuality < minimumQuality || maximumQuality > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumQuality));
        }

        if (targetBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetBytes));
        }

        ArgumentNullException.ThrowIfNull(probe);

        var attempts = new Dictionary<int, long>();
        CompressionAttempt? best = null;
        var low = minimumQuality;
        var high = maximumQuality;

        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quality = low + ((high - low) / 2);
            var size = await ProbeAsync(quality).ConfigureAwait(false);

            if (size <= targetBytes)
            {
                best = new CompressionAttempt(quality, size);
                low = quality + 1;
            }
            else
            {
                high = quality - 1;
            }
        }

        if (best is not null)
        {
            return new CompressionDecision(
                true,
                best.Quality,
                best.SizeBytes,
                ToAttempts(attempts));
        }

        var minimumSize = await ProbeAsync(minimumQuality).ConfigureAwait(false);
        return new CompressionDecision(
            false,
            minimumQuality,
            minimumSize,
            ToAttempts(attempts));

        async Task<long> ProbeAsync(int quality)
        {
            if (attempts.TryGetValue(quality, out var cached))
            {
                return cached;
            }

            var size = await probe(quality).ConfigureAwait(false);
            attempts.Add(quality, size);
            return size;
        }
    }

    public static IReadOnlyList<PixelSize> BuildResizeCandidates(
        PixelSize source,
        CompressionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (!options.AllowAutomaticResize || source.ShortEdge <= options.MinimumShortEdge)
        {
            return [];
        }

        var shortEdgeScale = (double)options.MinimumShortEdge / source.ShortEdge;
        var minimumScale = Math.Max(options.MinimumScaleRatio, shortEdgeScale);
        if (minimumScale >= 1)
        {
            return [];
        }

        var candidates = new List<PixelSize>();
        var scale = 0.9;

        while (scale > minimumScale)
        {
            AddCandidate(scale);
            scale *= 0.9;
        }

        AddCandidate(minimumScale);
        return candidates;

        void AddCandidate(double candidateScale)
        {
            PixelSize size;
            if (source.Width >= source.Height)
            {
                var width = Math.Max(1, (int)Math.Round(source.Width * candidateScale));
                var height = Math.Max(
                    1,
                    (int)Math.Round((double)width * source.Height / source.Width));
                size = new PixelSize(width, height);
            }
            else
            {
                var height = Math.Max(1, (int)Math.Round(source.Height * candidateScale));
                var width = Math.Max(
                    1,
                    (int)Math.Round((double)height * source.Width / source.Height));
                size = new PixelSize(width, height);
            }

            if (size.ShortEdge < options.MinimumShortEdge)
            {
                return;
            }

            if (candidates.Count == 0 || candidates[^1] != size)
            {
                candidates.Add(size);
            }
        }
    }

    public static bool CanAutomaticallyResize(ProcessingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Compression.Enabled &&
               request.Compression.AllowAutomaticResize &&
               !request.Resize.Enabled;
    }

    private static IReadOnlyList<CompressionAttempt> ToAttempts(
        IDictionary<int, long> attempts) =>
        attempts
            .OrderBy(pair => pair.Key)
            .Select(pair => new CompressionAttempt(pair.Key, pair.Value))
            .ToArray();
}
