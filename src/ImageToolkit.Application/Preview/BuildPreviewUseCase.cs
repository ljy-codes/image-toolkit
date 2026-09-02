using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Preview;

public sealed class BuildPreviewUseCase : IDisposable
{
    private readonly IImagePreviewRenderer _renderer;
    private readonly object _sync = new();
    private CancellationTokenSource? _activeRequest;

    public BuildPreviewUseCase(IImagePreviewRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public async Task<PreviewImage> ExecuteAsync(
        string sourcePath,
        ProcessingRequest request,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource current;
        lock (_sync)
        {
            _activeRequest?.Cancel();
            _activeRequest?.Dispose();
            _activeRequest = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            current = _activeRequest;
        }

        await Task.Delay(200, current.Token).ConfigureAwait(false);
        return await _renderer.RenderAsync(
            sourcePath,
            request,
            maximumWidth,
            maximumHeight,
            current.Token).ConfigureAwait(false);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _activeRequest?.Cancel();
            _activeRequest?.Dispose();
            _activeRequest = null;
        }
    }
}
