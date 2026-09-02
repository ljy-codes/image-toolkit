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
            _activeRequest = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            current = _activeRequest;
        }

        try
        {
            await Task.Delay(200, current.Token).ConfigureAwait(false);
            var result = await _renderer.RenderAsync(
                sourcePath,
                request,
                maximumWidth,
                maximumHeight,
                current.Token).ConfigureAwait(false);
            current.Token.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_activeRequest, current))
                {
                    _activeRequest = null;
                }
            }

            current.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _activeRequest?.Cancel();
            _activeRequest = null;
        }
    }
}
