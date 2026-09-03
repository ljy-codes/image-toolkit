using ImageMagick;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Concurrent;

namespace ImageToolkit.Infrastructure.AI;

public sealed class OnnxBackgroundRemovalEngine :
    IBackgroundRemovalEngine,
    IDisposable
{
    private const int FallbackInputSize = 320;
    private readonly IAiModelManager _modelManager;
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private readonly ConcurrentDictionary<string, InferenceSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);

    public OnnxBackgroundRemovalEngine(IAiModelManager modelManager)
    {
        _modelManager = modelManager;
    }

    public async Task RemoveBackgroundAsync(
        Stream input,
        Stream output,
        BackgroundRemovalMode mode,
        CancellationToken cancellationToken)
    {
        if (mode == BackgroundRemovalMode.Disabled)
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return;
        }

        var modelId = mode == BackgroundRemovalMode.Portrait
            ? AiModelManifest.PortraitModelId
            : AiModelManifest.GeneralModelId;
        var modelPath = await _modelManager.GetModelPathAsync(
            modelId,
            cancellationToken).ConfigureAwait(false);

        await _inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var image = new MagickImage(input);
            var session = _sessions.GetOrAdd(
                modelPath,
                static path => new InferenceSession(path));
            var inputName = session.InputMetadata.Keys.First();
            var (inputWidth, inputHeight) = ResolveInputSize(
                session.InputMetadata[inputName].Dimensions);
            var tensor = CreateInputTensor(image, inputWidth, inputHeight);
            using var results = session.Run(
            [
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            ]);
            var maskTensor = results.First().AsTensor<float>();
            ApplyMask(image, maskTensor);
            image.Format = MagickFormat.Png;
            image.Write(output);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _inferenceGate.Dispose();
    }

    private static (int Width, int Height) ResolveInputSize(
        IReadOnlyList<int> dimensions)
    {
        if (dimensions.Count < 2)
        {
            return (FallbackInputSize, FallbackInputSize);
        }

        var height = dimensions[^2];
        var width = dimensions[^1];
        return width > 0 && height > 0
            ? (width, height)
            : (FallbackInputSize, FallbackInputSize);
    }

    private static DenseTensor<float> CreateInputTensor(
        MagickImage source,
        int inputWidth,
        int inputHeight)
    {
        using var resized = source.Clone();
        resized.BackgroundColor = MagickColors.White;
        if (resized.HasAlpha)
        {
            resized.Alpha(AlphaOption.Remove);
        }

        resized.ColorSpace = ColorSpace.sRGB;
        resized.Resize(new MagickGeometry((uint)inputWidth, (uint)inputHeight)
        {
            IgnoreAspectRatio = true
        });
        var bytes = resized.GetPixels().ToByteArray(PixelMapping.RGB)
            ?? throw new InvalidDataException("无法读取图片像素。");
        var tensor = new DenseTensor<float>(
            new[] { 1, 3, inputHeight, inputWidth });
        var means = new[] { 0.485f, 0.456f, 0.406f };
        var standardDeviations = new[] { 0.229f, 0.224f, 0.225f };

        for (var y = 0; y < inputHeight; y++)
        {
            for (var x = 0; x < inputWidth; x++)
            {
                var pixelOffset = (y * inputWidth + x) * 3;
                for (var channel = 0; channel < 3; channel++)
                {
                    var value = bytes[pixelOffset + channel] / 255f;
                    tensor[0, channel, y, x] =
                        (value - means[channel]) / standardDeviations[channel];
                }
            }
        }

        return tensor;
    }

    private static void ApplyMask(MagickImage image, Tensor<float> tensor)
    {
        var values = tensor.ToArray();
        if (tensor.Dimensions.Length < 2)
        {
            throw new InvalidDataException("AI 模型输出的遮罩维度不正确。");
        }

        var maskHeight = tensor.Dimensions[^2];
        var maskWidth = tensor.Dimensions[^1];
        var planeSize = maskWidth * maskHeight;
        if (values.Length < planeSize)
        {
            throw new InvalidDataException("AI 模型输出的遮罩尺寸不正确。");
        }

        var probabilities = new float[planeSize];
        var requiresSigmoid = values
            .Take(planeSize)
            .Any(value => value is < 0f or > 1f);
        for (var index = 0; index < planeSize; index++)
        {
            probabilities[index] = requiresSigmoid
                ? 1f / (1f + MathF.Exp(-values[index]))
                : values[index];
        }

        var minimum = probabilities.Min();
        var maximum = probabilities.Max();
        var range = Math.Max(0.000001f, maximum - minimum);
        for (var index = 0; index < planeSize; index++)
        {
            probabilities[index] = Math.Clamp(
                (probabilities[index] - minimum) / range,
                0f,
                1f);
        }

        RemoveSmallComponents(
            probabilities,
            maskWidth,
            maskHeight,
            foreground: true);
        RemoveSmallComponents(
            probabilities,
            maskWidth,
            maskHeight,
            foreground: false);
        ValidateMaskQuality(probabilities);

        var maskBytes = new byte[planeSize * 3];
        for (var index = 0; index < planeSize; index++)
        {
            var value = (byte)Math.Round(probabilities[index] * 255);
            var offset = index * 3;
            maskBytes[offset] = value;
            maskBytes[offset + 1] = value;
            maskBytes[offset + 2] = value;
        }

        using var mask = new MagickImage(
            maskBytes,
            new PixelReadSettings(
                (uint)maskWidth,
                (uint)maskHeight,
                StorageType.Char,
                PixelMapping.RGB));
        mask.Resize(new MagickGeometry(image.Width, image.Height)
        {
            IgnoreAspectRatio = true
        });
        mask.ColorSpace = ColorSpace.Gray;
        image.Alpha(AlphaOption.Activate);
        image.Composite(mask, CompositeOperator.CopyAlpha);
        DecontaminateSoftEdges(image);
    }

    private static void ValidateMaskQuality(float[] mask)
    {
        const double minimumConfidentRatio = 0.005d;
        var confidentForeground = mask.Count(value => value >= 0.94f);
        var confidentBackground = mask.Count(value => value <= 0.06f);
        var foregroundRatio = confidentForeground / (double)mask.Length;
        var backgroundRatio = confidentBackground / (double)mask.Length;

        if (foregroundRatio < minimumConfidentRatio)
        {
            throw new InvalidDataException(
                "AI 抠图未识别到明确主体，图片可能是全景、纹理图或主体过于分散。" +
                "请改用主体更明确的图片，或关闭 AI 抠图。");
        }

        if (backgroundRatio < minimumConfidentRatio)
        {
            throw new InvalidDataException(
                "AI 抠图未识别到可移除背景，图片可能被主体完全占满。" +
                "请改用主体与背景区分更明显的图片，或关闭 AI 抠图。");
        }
    }

    private static void DecontaminateSoftEdges(MagickImage image)
    {
        const int searchRadius = 4;
        var width = checked((int)image.Width);
        var height = checked((int)image.Height);
        var source = image.GetPixels().ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidDataException("无法读取 AI 输出像素。");
        var result = (byte[])source.Clone();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                var alpha = source[offset + 3];
                if (alpha is <= 8 or >= 248)
                {
                    continue;
                }

                var nearestOffset = -1;
                var nearestDistance = int.MaxValue;
                var minY = Math.Max(0, y - searchRadius);
                var maxY = Math.Min(height - 1, y + searchRadius);
                var minX = Math.Max(0, x - searchRadius);
                var maxX = Math.Min(width - 1, x + searchRadius);
                for (var neighborY = minY; neighborY <= maxY; neighborY++)
                {
                    for (var neighborX = minX; neighborX <= maxX; neighborX++)
                    {
                        var neighborOffset =
                            (neighborY * width + neighborX) * 4;
                        if (source[neighborOffset + 3] < 248)
                        {
                            continue;
                        }

                        var deltaX = neighborX - x;
                        var deltaY = neighborY - y;
                        var distance = deltaX * deltaX + deltaY * deltaY;
                        if (distance >= nearestDistance)
                        {
                            continue;
                        }

                        nearestDistance = distance;
                        nearestOffset = neighborOffset;
                    }
                }

                if (nearestOffset < 0)
                {
                    continue;
                }

                var normalizedAlpha = alpha / 255d;
                var strength = Math.Pow(1d - normalizedAlpha, 0.7d) * 0.85d;
                for (var channel = 0; channel < 3; channel++)
                {
                    result[offset + channel] = (byte)Math.Clamp(
                        Math.Round(
                            source[offset + channel] * (1d - strength) +
                            source[nearestOffset + channel] * strength),
                        0d,
                        255d);
                }
            }
        }

        using var pixels = image.GetPixels();
        pixels.SetByteArea(0, 0, (uint)width, (uint)height, result);
    }

    private static void RemoveSmallComponents(
        float[] mask,
        int width,
        int height,
        bool foreground)
    {
        var visited = new bool[mask.Length];
        var queue = new Queue<int>();
        var component = new List<int>();
        var minimumArea = Math.Max(4, mask.Length / 10_000);

        for (var start = 0; start < mask.Length; start++)
        {
            if (visited[start] || !Matches(mask[start], foreground))
            {
                continue;
            }

            queue.Clear();
            component.Clear();
            queue.Enqueue(start);
            visited[start] = true;
            var touchesBorder = false;

            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                component.Add(index);
                var x = index % width;
                var y = index / width;
                touchesBorder |= x == 0 || y == 0 || x == width - 1 || y == height - 1;

                VisitNeighbor(index - 1, x > 0);
                VisitNeighbor(index + 1, x < width - 1);
                VisitNeighbor(index - width, y > 0);
                VisitNeighbor(index + width, y < height - 1);
            }

            var shouldReplace = component.Count < minimumArea &&
                (foreground || !touchesBorder);
            if (shouldReplace)
            {
                var replacement = foreground ? 0f : 1f;
                foreach (var index in component)
                {
                    mask[index] = replacement;
                }
            }
        }

        return;

        void VisitNeighbor(int index, bool isValid)
        {
            if (!isValid || visited[index] || !Matches(mask[index], foreground))
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }
    }

    private static bool Matches(float value, bool foreground) =>
        foreground ? value >= 0.5f : value <= 0.92f;
}
