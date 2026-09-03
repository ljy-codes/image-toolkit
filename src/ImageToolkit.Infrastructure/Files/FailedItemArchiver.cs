using System.Text;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Files;

public sealed class FailedItemArchiver : IFailedItemArchiver, IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly SemaphoreSlim _reportLock = new(1, 1);

    public async Task ArchiveAsync(
        ImageImportEntry entry,
        ImageProcessingResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(result);
        if (entry.SourceKind != ImportSourceKind.Folder)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var failedRoot = GetFailedRoot(entry.SourceRoot);
        var destination = Path.Combine(failedRoot, entry.RelativePath);
        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("失败文件路径缺少目录。");
        Directory.CreateDirectory(destinationDirectory);
        destination = ReserveCopyPath(destination);
        File.Copy(entry.SourcePath, destination, false);

        await _reportLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(failedRoot);
            await AppendTextReportAsync(
                Path.Combine(failedRoot, "失败原因.txt"),
                entry,
                destination,
                result,
                cancellationToken).ConfigureAwait(false);
            await AppendCsvReportAsync(
                Path.Combine(failedRoot, "失败原因.csv"),
                entry,
                destination,
                result,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _reportLock.Release();
        }
    }

    public void Dispose() => _reportLock.Dispose();

    private static string GetFailedRoot(string sourceRoot)
    {
        var parent = Path.GetDirectoryName(sourceRoot)
            ?? throw new ArgumentException("导入文件夹缺少父目录。", nameof(sourceRoot));
        return Path.Combine(parent, Path.GetFileName(sourceRoot) + "-未处理");
    }

    private static string ReserveCopyPath(string destination)
    {
        if (!File.Exists(destination))
        {
            return destination;
        }

        var directory = Path.GetDirectoryName(destination)!;
        var name = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(directory, $"{name}-{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static async Task AppendTextReportAsync(
        string reportPath,
        ImageImportEntry entry,
        string destination,
        ImageProcessingResult result,
        CancellationToken cancellationToken)
    {
        var diagnostic = result.Diagnostic;
        var suggestions = diagnostic?.Suggestions.Count > 0
            ? string.Join("；", diagnostic.Suggestions)
            : "请查看技术详情或重试。";
        var text =
            $"时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
            $"源文件：{entry.SourcePath}{Environment.NewLine}" +
            $"归档文件：{destination}{Environment.NewLine}" +
            $"失败阶段：{diagnostic?.Stage ?? "processing"}{Environment.NewLine}" +
            $"失败原因：{diagnostic?.UserMessage ?? result.Message ?? "处理失败。"}{Environment.NewLine}" +
            $"处理建议：{suggestions}{Environment.NewLine}" +
            $"错误代码：{result.ErrorCode ?? "processing.failed"}{Environment.NewLine}" +
            $"{new string('-', 72)}{Environment.NewLine}";
        await File.AppendAllTextAsync(
            reportPath,
            text,
            Utf8WithoutBom,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task AppendCsvReportAsync(
        string reportPath,
        ImageImportEntry entry,
        string destination,
        ImageProcessingResult result,
        CancellationToken cancellationToken)
    {
        var newFile = !File.Exists(reportPath);
        var diagnostic = result.Diagnostic;
        var builder = new StringBuilder();
        if (newFile)
        {
            builder.AppendLine("时间,源文件,归档文件,失败阶段,失败原因,处理建议,错误代码");
        }

        builder
            .Append(Csv(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))).Append(',')
            .Append(Csv(entry.SourcePath)).Append(',')
            .Append(Csv(destination)).Append(',')
            .Append(Csv(diagnostic?.Stage ?? "processing")).Append(',')
            .Append(Csv(diagnostic?.UserMessage ?? result.Message ?? "处理失败。")).Append(',')
            .Append(Csv(string.Join("；", diagnostic?.Suggestions ?? []))).Append(',')
            .AppendLine(Csv(result.ErrorCode ?? "processing.failed"));
        await File.AppendAllTextAsync(
            reportPath,
            builder.ToString(),
            Utf8WithoutBom,
            cancellationToken).ConfigureAwait(false);
    }

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
