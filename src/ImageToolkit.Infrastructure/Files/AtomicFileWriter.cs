using ImageToolkit.Domain.Interfaces;

namespace ImageToolkit.Infrastructure.Files;

public sealed class AtomicFileWriter : IAtomicFileWriter
{
    public async Task WriteNewAsync(
        string targetPath,
        Func<Stream, Task> write,
        Func<string, Task<bool>> validate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(validate);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureEmptyReservation(targetPath);
        var temporaryPath = CreateTemporaryPath(targetPath);
        var completed = false;

        try
        {
            await WriteAndFlushAsync(
                temporaryPath,
                write,
                cancellationToken).ConfigureAwait(false);

            if (!await validate(temporaryPath).ConfigureAwait(false))
            {
                throw new InvalidDataException("输出文件校验失败。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, targetPath, true);
            completed = true;
        }
        finally
        {
            DeleteIfExists(temporaryPath);
            if (!completed && File.Exists(targetPath) && new FileInfo(targetPath).Length == 0)
            {
                DeleteIfExists(targetPath);
            }
        }
    }

    public async Task ReplaceAsync(
        string targetPath,
        Func<Stream, Task> write,
        Func<string, Task<bool>> validate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(validate);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("需要覆盖的原文件不存在。", targetPath);
        }

        var temporaryPath = CreateTemporaryPath(targetPath);
        var backupPath = temporaryPath + ".bak";

        try
        {
            await WriteAndFlushAsync(
                temporaryPath,
                write,
                cancellationToken).ConfigureAwait(false);

            if (!await validate(temporaryPath).ConfigureAwait(false))
            {
                throw new InvalidDataException("输出文件校验失败。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(temporaryPath, targetPath, backupPath, true);
            DeleteIfExists(backupPath);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
            DeleteIfExists(backupPath);
        }
    }

    private static async Task WriteAndFlushAsync(
        string temporaryPath,
        Func<Stream, Task> write,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            131072,
            FileOptions.Asynchronous | FileOptions.WriteThrough);

        await write(stream).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(true);
    }

    private static void EnsureEmptyReservation(string targetPath)
    {
        if (!File.Exists(targetPath))
        {
            using var reservation = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            return;
        }

        if (new FileInfo(targetPath).Length != 0)
        {
            throw new IOException("输出路径已被其他文件占用。");
        }
    }

    private static string CreateTemporaryPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("目标路径缺少目录。", nameof(targetPath));
        return Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
