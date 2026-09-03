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
    private const int InputWidth = 320;
    private const int InputHeight = 320;
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
            var tensor = CreateInputTensor(image);
            var session = _sessions.GetOrAdd(
                modelPath,
                static path => new InferenceSession(path));
            var inputName = session.InputMetadata.Keys.First();
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

    private static DenseTensor<float> CreateInputTensor(MagickImage source)
    {
        using var resized = source.Clone();
        resized.BackgroundColor = MagickColors.White;
        if (resized.HasAlpha)
        {
            resized.Alpha(AlphaOption.Remove);
        }

        resized.ColorSpace = ColorSpace.sRGB;
        resized.Resize(new MagickGeometry(InputWidth, InputHeight)
        {
            IgnoreAspectRatio = true
        });
        var bytes = resized.GetPixels().ToByteArray(PixelMapping.RGB)
            ?? throw new InvalidDataException("无法读取图片像素。");
        var tensor = new DenseTensor<float>(
            new[] { 1, 3, InputHeight, InputWidth });
        var means = new[] { 0.485f, 0.456f, 0.406f };
        var standardDeviations = new[] { 0.229f, 0.224f, 0.225f };

        for (var y = 0; y < InputHeight; y++)
        {
            for (var x = 0; x < InputWidth; x++)
            {
                var pixelOffset = (y * InputWidth + x) * 3;
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
        var planeSize = InputWidth * InputHeight;
        if (values.Length < planeSize)
        {
            throw new InvalidDataException("AI 模型输出的遮罩尺寸不正确。");
        }

        var minimum = values.Take(planeSize).Min();
        var maximum = values.Take(planeSize).Max();
        var range = Math.Max(0.000001f, maximum - minimum);
        var maskBytes = new byte[planeSize * 3];
        for (var index = 0; index < planeSize; index++)
        {
            var normalized = Math.Clamp(
                (values[index] - minimum) / range,
                0f,
                1f);
            var value = (byte)Math.Round(normalized * 255);
            var offset = index * 3;
            maskBytes[offset] = value;
            maskBytes[offset + 1] = value;
            maskBytes[offset + 2] = value;
        }

        using var mask = new MagickImage(
            maskBytes,
            new PixelReadSettings(
                InputWidth,
                InputHeight,
                StorageType.Char,
                PixelMapping.RGB));
        mask.Resize(new MagickGeometry(image.Width, image.Height)
        {
            IgnoreAspectRatio = true
        });
        mask.ColorSpace = ColorSpace.Gray;
        image.Alpha(AlphaOption.Activate);
        image.Composite(mask, CompositeOperator.CopyAlpha);
    }
}
