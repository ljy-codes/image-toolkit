namespace ImageToolkit.Domain.Interfaces;

public interface IAtomicFileWriter
{
    Task WriteNewAsync(
        string targetPath,
        Func<Stream, Task> write,
        Func<string, Task<bool>> validate,
        CancellationToken cancellationToken);

    Task ReplaceAsync(
        string targetPath,
        Func<Stream, Task> write,
        Func<string, Task<bool>> validate,
        CancellationToken cancellationToken);
}
