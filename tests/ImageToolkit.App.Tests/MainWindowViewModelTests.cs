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

    private static MainWindowViewModel CreateViewModel()
    {
        var previewUseCase = new BuildPreviewUseCase(new PreviewRenderer());
        return new MainWindowViewModel(
            new ImportImagesUseCase(new Discovery()),
            new MetadataReader(),
            new ImageProcessingPipeline(
                new ProcessingRequestValidator(),
                new PathResolver(),
                new Processor()),
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
        public Task<ImageProcessingResult> ProcessAsync(
            string sourcePath,
            string outputPath,
            ProcessingRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                ImageProcessingResult.Completed(
                    sourcePath,
                    outputPath,
                    1,
                    new PixelSize(1, 1),
                    90,
                    false,
                    false));
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
