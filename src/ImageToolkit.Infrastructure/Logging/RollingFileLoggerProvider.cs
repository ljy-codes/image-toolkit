using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ImageToolkit.Infrastructure.Logging;

public sealed class RollingFileLoggerProvider :
    ILoggerProvider,
    IAsyncDisposable
{
    private readonly string _logDirectory;
    private readonly string[] _sensitiveValues;
    private readonly Channel<LogEntry> _entries;
    private readonly Task _writerTask;
    private DateOnly? _lastCleanupDate;
    private int _disposed;

    public RollingFileLoggerProvider(
        string logDirectory,
        IEnumerable<string>? sensitiveValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = Path.GetFullPath(logDirectory);
        _sensitiveValues = sensitiveValues?
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"无法创建日志目录：{exception}");
        }

        TryDeleteExpiredLogs();
        _entries = Channel.CreateUnbounded<LogEntry>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        _writerTask = WriteLoopAsync();
    }

    public ILogger CreateLogger(string categoryName) =>
        new RollingFileLogger(this, categoryName);

    public void Dispose() =>
        DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _entries.Writer.TryComplete();
        try
        {
            await _writerTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"日志写入任务退出异常：{exception}");
        }

        GC.SuppressFinalize(this);
    }

    private void Enqueue(
        string category,
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _entries.Writer.TryWrite(new LogEntry(
            DateTimeOffset.Now,
            category,
            level,
            eventId,
            Redact(message),
            exception is null
                ? null
                : $"{exception.GetType().Name}: {Redact(exception.Message)}"));
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var entry in _entries.Reader.ReadAllAsync())
        {
            try
            {
                var path = Path.Combine(
                    _logDirectory,
                    $"ImageToolkit-{entry.Timestamp:yyyyMMdd}.log");
                var line = Format(entry);
                await File.AppendAllTextAsync(
                    path,
                    line,
                    new UTF8Encoding(false)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"日志写入失败，已丢弃当前记录：{exception}");
                continue;
            }

            var entryDate = DateOnly.FromDateTime(entry.Timestamp.LocalDateTime);
            if (_lastCleanupDate != entryDate && TryDeleteExpiredLogs())
            {
                _lastCleanupDate = entryDate;
            }
        }
    }

    private string Redact(string text)
    {
        var result = text;
        foreach (var value in _sensitiveValues)
        {
            result = result.Replace(
                value,
                "[已隐藏]",
                StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private bool TryDeleteExpiredLogs()
    {
        try
        {
            var files = Directory
                .EnumerateFiles(_logDirectory, "ImageToolkit-*.log")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Skip(14)
                .ToArray();

            foreach (var file in files)
            {
                file.Delete();
            }

            return true;
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"清理过期日志失败：{exception}");
            return false;
        }
    }

    private static string Format(LogEntry entry)
    {
        var exception = entry.Exception is null
            ? string.Empty
            : $" | {entry.Exception}";
        return
            $"{entry.Timestamp:O} | {entry.Level} | {entry.EventId.Id}:{entry.EventId.Name} | " +
            $"{entry.Category} | {entry.Message}{exception}{Environment.NewLine}";
    }

    private sealed class RollingFileLogger(
        RollingFileLoggerProvider provider,
        string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            provider.Enqueue(
                categoryName,
                logLevel,
                eventId,
                formatter(state, exception),
                exception);
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed record LogEntry(
        DateTimeOffset Timestamp,
        string Category,
        LogLevel Level,
        EventId EventId,
        string Message,
        string? Exception);
}
