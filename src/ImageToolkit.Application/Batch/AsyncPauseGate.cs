namespace ImageToolkit.Application.Batch;

public sealed class AsyncPauseGate
{
    private TaskCompletionSource _resume =
        CreateCompletionSource(completed: true);

    public bool IsPaused => !Volatile.Read(ref _resume).Task.IsCompleted;

    public Task WaitAsync(CancellationToken cancellationToken) =>
        Volatile.Read(ref _resume).Task.WaitAsync(cancellationToken);

    public void Pause()
    {
        while (true)
        {
            var current = Volatile.Read(ref _resume);
            if (!current.Task.IsCompleted)
            {
                return;
            }

            var paused = CreateCompletionSource(completed: false);
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _resume, paused, current),
                    current))
            {
                return;
            }
        }
    }

    public void Resume() => Volatile.Read(ref _resume).TrySetResult();

    private static TaskCompletionSource CreateCompletionSource(bool completed)
    {
        var source = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (completed)
        {
            source.SetResult();
        }

        return source;
    }
}
