using System.Collections;
using System.Collections.Concurrent;
using ImageToolkit.App.Models;
using ImageToolkit.App.Services;
using ImageToolkit.App.ViewModels;
using ImageToolkit.Application.Import;
using ImageToolkit.Application.Preview;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Start_is_disabled_for_empty_queue_and_enabled_after_add()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.StartCommand.CanExecute(null));

        viewModel.FileQueue.Items.Add(
            new ImageQueueItemViewData(
                @"C:\images\one.jpg",
                "one.jpg",
                "800 × 600",
                "100 KB"));

        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public void Remove_selected_removes_every_selected_queue_item()
    {
        var viewModel = CreateViewModel();
        var first = CreateItem("one.jpg");
        var second = CreateItem("two.jpg");
        var third = CreateItem("three.jpg");
        viewModel.FileQueue.Items.Add(first);
        viewModel.FileQueue.Items.Add(second);
        viewModel.FileQueue.Items.Add(third);

        viewModel.RemoveSelectedCommand.Execute(new ArrayList { first, third });

        Assert.Equal([second], viewModel.FileQueue.Items);
    }

    [Fact]
    public async Task Retry_failed_processes_only_retryable_items()
    {
        var processor = new Processor();
        var viewModel = CreateViewModel(processor);
        var completed = CreateItem("completed.jpg");
        completed.Status = "已完成";
        var failed = CreateItem("failed.jpg");
        failed.Status = "失败";
        var unmet = CreateItem("unmet.jpg");
        unmet.Status = "未达标";
        viewModel.FileQueue.Items.Add(completed);
        viewModel.FileQueue.Items.Add(failed);
        viewModel.FileQueue.Items.Add(unmet);

        await viewModel.RetryFailedCommand.ExecuteAsync(null);

        Assert.Equal(
            [failed.SourcePath, unmet.SourcePath],
            processor.ProcessedPaths.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Closing_running_batch_waits_for_safe_cancellation()
    {
        var processor = new CancellableProcessor();
        var viewModel = CreateViewModel(processor);
        viewModel.FileQueue.Items.Add(CreateItem("running.jpg"));
        var run = viewModel.StartCommand.ExecuteAsync(null);
        await processor.Started.Task;

        var canClose = await viewModel.PrepareCloseAsync();
        await run;

        Assert.True(canClose);
        Assert.True(processor.CancellationObserved);
        Assert.False(viewModel.IsRunning);
    }

    private static ImageQueueItemViewData CreateItem(string fileName) =>
        new(
            $@"C:\images\{fileName}",
            fileName,
            "800 × 600",
            "100 KB");

    private static MainWindowViewModel CreateViewModel(IImageProcessor? processor = null)
    {
        var previewUseCase = new BuildPreviewUseCase(new PreviewRenderer());
        return new MainWindowViewModel(
            new ImportImagesUseCase(new Discovery()),
            new MetadataReader(),
            new ImageProcessingPipeline(
                new ProcessingRequestValidator(),
                new PathResolver(),
                processor ?? new Processor()),
            new ProcessingRequestValidator(),
            new Picker(),
            new Dialogs(),
            new ProcessingSettingsViewModel(),
            new AppearanceSettingsViewModel(new ThemeServiceStub()),
            new PreviewViewModel(previewUseCase),
            new FileQueueViewModel(),
            new BatchProgressViewModel());
    }

    private sealed class Discovery : IImageFileDiscovery
    {
        public Task<ImageImportResult> DiscoverAsync(
            IEnumerable<string> inputPaths,
            bool includeSubdirectories,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ImageImportResult([], []));
    }

    private sealed class MetadataReader : IImageMetadataReader
    {
        public Task<ImageFileInfo> ReadAsync(
            string sourcePath,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class PathResolver : IOutputPathResolver
    {
        public string Resolve(
            string sourcePath,
            Domain.Options.OutputOptions options,
            string outputExtension) =>
            sourcePath + outputExtension;
    }

    private sealed class Processor : IImageProcessor
    {
        public ConcurrentBag<string> ProcessedPaths { get; } = [];

        public Task<ImageProcessingResult> ProcessAsync(
            string sourcePath,
            string outputPath,
            ProcessingRequest request,
            CancellationToken cancellationToken)
        {
            ProcessedPaths.Add(sourcePath);
            return Task.FromResult(
                ImageProcessingResult.Completed(
                    sourcePath,
                    outputPath,
                    1,
                    new PixelSize(1, 1),
                    90,
                    false,
                    false));
        }
    }

    private sealed class CancellableProcessor : IImageProcessor
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationObserved { get; private set; }

        public async Task<ImageProcessingResult> ProcessAsync(
            string sourcePath,
            string outputPath,
            ProcessingRequest request,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }

            return ImageProcessingResult.Completed(sourcePath, outputPath, 1);
        }
    }

    private sealed class PreviewRenderer : IImagePreviewRenderer
    {
        public Task<PreviewImage> RenderAsync(
            string sourcePath,
            ProcessingRequest request,
            int maximumWidth,
            int maximumHeight,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PreviewImage([1], new PixelSize(1, 1)));
    }

    private sealed class Picker : IDesktopFilePicker
    {
        public Task<IReadOnlyList<string>> PickFilesAsync() =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> PickFolderAsync() =>
            Task.FromResult<string?>(null);
    }

    private sealed class Dialogs : IUserDialogService
    {
        public void ShowMessage(string message, string title = "图批处理")
        {
        }

        public bool Confirm(string message, string title = "图批处理") => true;
    }

    private sealed class ThemeServiceStub : IThemeService
    {
        public void Apply(
            string theme,
            string workspaceBackground,
            string customWorkspaceColor,
            double fontSize)
        {
        }
    }
}
