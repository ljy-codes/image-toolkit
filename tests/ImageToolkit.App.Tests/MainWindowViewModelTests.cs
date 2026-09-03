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
using ImageToolkit.Domain.Enums;
using ImageToolkit.Infrastructure.Config;
using ImageToolkit.Infrastructure.AI;

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

    [Fact]
    public async Task Active_model_operation_disables_batch_start_and_counts_as_active_work()
    {
        var modelManager = new CancellableAiModelManager();
        var viewModel = CreateViewModel(aiModelManager: modelManager);
        viewModel.FileQueue.Items.Add(CreateItem("waiting.jpg"));

        Assert.True(viewModel.StartCommand.CanExecute(null));

        var install = viewModel.AiBackgroundRemoval.InstallPortraitCommand
            .ExecuteAsync(null);
        await modelManager.Started.Task;

        Assert.True(viewModel.HasActiveWork);
        Assert.False(viewModel.StartCommand.CanExecute(null));

        await viewModel.AiBackgroundRemoval.CancelActiveInstallAsync();
        await install;

        Assert.False(viewModel.HasActiveWork);
        Assert.True(viewModel.StartCommand.CanExecute(null));
    }

    [Fact]
    public void Queue_item_keeps_folder_import_context()
    {
        var entry = ImageImportEntry.FromFolder(
            @"C:\images",
            @"C:\images\album\one.jpg");

        var item = new ImageQueueItemViewData(
            entry.SourcePath,
            "one.jpg",
            "800 × 600",
            "100 KB",
            entry);

        Assert.Same(entry, item.ImportEntry);
    }

    [Fact]
    public async Task Start_next_batch_clears_previous_results_and_allows_second_run()
    {
        var processor = new Processor();
        var viewModel = CreateViewModel(processor);
        viewModel.Settings.TargetMegabytes = 2.5;
        var first = CreateItem("first.jpg");
        viewModel.FileQueue.Items.Add(first);

        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasCompletedBatch);
        Assert.True(viewModel.StartNextBatchCommand.CanExecute(null));

        viewModel.StartNextBatchCommand.Execute(null);

        Assert.Empty(viewModel.FileQueue.Items);
        Assert.Null(viewModel.FileQueue.SelectedItem);
        Assert.Equal(0, viewModel.Progress.Total);
        Assert.Equal("准备就绪", viewModel.Progress.StatusText);
        Assert.Equal(2.5, viewModel.Settings.TargetMegabytes);
        Assert.False(viewModel.HasCompletedBatch);

        var second = CreateItem("second.jpg");
        viewModel.FileQueue.Items.Add(second);
        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.Equal([second], viewModel.FileQueue.Items);
        Assert.Equal(
            [first.SourcePath, second.SourcePath],
            processor.ProcessedPaths.Order(StringComparer.OrdinalIgnoreCase));
        Assert.True(viewModel.HasCompletedBatch);
    }

    [Fact]
    public async Task Primary_action_switches_between_start_and_new_batch()
    {
        var viewModel = CreateViewModel();
        viewModel.FileQueue.Items.Add(CreateItem("first.jpg"));

        Assert.True(viewModel.ShowStartAction);
        Assert.False(viewModel.ShowNewBatchAction);

        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.False(viewModel.ShowStartAction);
        Assert.True(viewModel.ShowNewBatchAction);

        viewModel.StartNextBatchCommand.Execute(null);

        Assert.True(viewModel.ShowStartAction);
        Assert.False(viewModel.ShowNewBatchAction);
    }

    [Fact]
    public async Task Completed_batch_shows_dismissible_completion_notice()
    {
        var viewModel = CreateViewModel();
        viewModel.FileQueue.Items.Add(CreateItem("done.jpg"));

        await viewModel.StartCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCompletionNoticeVisible);
        Assert.Contains("已完成", viewModel.CompletionNoticeText);

        viewModel.DismissCompletionNoticeCommand.Execute(null);

        Assert.False(viewModel.IsCompletionNoticeVisible);
    }

    [Fact]
    public async Task Export_configuration_includes_current_settings_appearance_and_named_presets()
    {
        var picker = new Picker
        {
            ConfigurationExportPath = @"C:\backup\苏影枢配置.syconfig"
        };
        var packageService = new PackageService();
        var presetStore = new PresetStore(
        [
            new ProcessingPreset("电商图片", ProcessingRequest.Default)
        ]);
        var viewModel = CreateViewModel(
            picker: picker,
            packageService: packageService,
            presetStore: presetStore);
        await viewModel.Presets.InitializeAsync(CancellationToken.None);
        viewModel.Settings.TargetMegabytes = 3;
        viewModel.Appearance.Theme = "Dark";
        viewModel.IncludeSubdirectories = true;

        await viewModel.ExportConfigurationCommand.ExecuteAsync(null);

        Assert.Equal(picker.ConfigurationExportPath, packageService.ExportedPath);
        Assert.NotNull(packageService.ExportedPackage);
        Assert.Equal("苏影枢", packageService.ExportedPackage.ProductName);
        Assert.Equal("Dark", packageService.ExportedPackage.Configuration.Theme);
        Assert.True(packageService.ExportedPackage.Configuration.IncludeSubdirectories);
        Assert.Equal(
            3 * 1024 * 1024,
            packageService.ExportedPackage.Configuration.Processing.Compression.TargetBytes);
        Assert.Equal("电商图片", Assert.Single(packageService.ExportedPackage.Presets).Name);
    }

    [Fact]
    public async Task Import_configuration_applies_complete_package_and_repairs_missing_directories()
    {
        var missingDirectory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "missing");
        var importedRequest = ProcessingRequest.Default with
        {
            Output = ProcessingRequest.Default.Output with
            {
                Mode = OutputMode.SpecificDirectory,
                DirectoryPath = missingDirectory
            }
        };
        var packageService = new PackageService
        {
            ImportedPackage = ConfigurationPackage.Create(
                AppConfiguration.Default with
                {
                    Processing = importedRequest,
                    Theme = "Dark",
                    IncludeSubdirectories = true
                },
                [new ProcessingPreset("商品图", importedRequest)])
        };
        var picker = new Picker
        {
            ConfigurationImportPath = @"C:\backup\苏影枢配置.syconfig"
        };
        var dialogs = new Dialogs();
        var configurationStore = new ConfigurationStore();
        var presetStore = new PresetStore([]);
        var viewModel = CreateViewModel(
            picker: picker,
            dialogs: dialogs,
            packageService: packageService,
            configurationStore: configurationStore,
            presetStore: presetStore);
        await viewModel.Presets.InitializeAsync(CancellationToken.None);

        await viewModel.ImportConfigurationCommand.ExecuteAsync(null);

        Assert.Equal("Dark", viewModel.Appearance.Theme);
        Assert.True(viewModel.IncludeSubdirectories);
        Assert.Equal(OutputMode.SourceDirectory, viewModel.Settings.OutputMode);
        Assert.Null(viewModel.Settings.OutputDirectory);
        Assert.Equal(
            "商品图",
            viewModel.Presets.Items.Single(item => item.Name == "商品图").Name);
        Assert.Equal(
            OutputMode.SourceDirectory,
            Assert.Single(presetStore.Saved).Request.Output.Mode);
        Assert.Contains(
            dialogs.Messages,
            message => message.Contains("2 个指定输出目录") &&
                       message.Contains("本机不存在") &&
                       message.Contains("原目录新文件"));
        Assert.Contains("2 个指定输出目录", viewModel.ConfigurationNotice);
    }

    [Fact]
    public async Task Import_configuration_cancelled_by_user_does_not_change_or_save_settings()
    {
        var packageService = new PackageService
        {
            ImportedPackage = ConfigurationPackage.Create(
                AppConfiguration.Default with { Theme = "Dark" },
                [])
        };
        var picker = new Picker
        {
            ConfigurationImportPath = @"C:\backup\苏影枢配置.syconfig"
        };
        var dialogs = new Dialogs { ConfirmResult = false };
        var configurationStore = new ConfigurationStore();
        var presetStore = new PresetStore([]);
        var viewModel = CreateViewModel(
            picker: picker,
            dialogs: dialogs,
            packageService: packageService,
            configurationStore: configurationStore,
            presetStore: presetStore);
        await viewModel.Presets.InitializeAsync(CancellationToken.None);

        await viewModel.ImportConfigurationCommand.ExecuteAsync(null);

        Assert.Equal("System", viewModel.Appearance.Theme);
        Assert.Empty(configurationStore.Saved);
        Assert.Empty(presetStore.SavedHistory);
    }

    [Fact]
    public async Task Import_configuration_rejects_invalid_custom_workspace_color_before_saving()
    {
        var packageService = new PackageService
        {
            ImportedPackage = ConfigurationPackage.Create(
                AppConfiguration.Default with
                {
                    Theme = "Dark",
                    WorkspaceBackground = "Custom",
                    CustomWorkspaceColor = null!
                },
                [])
        };
        var picker = new Picker
        {
            ConfigurationImportPath = @"C:\backup\苏影枢配置.syconfig"
        };
        var dialogs = new Dialogs();
        var configurationStore = new ConfigurationStore();
        var presetStore = new PresetStore([]);
        var viewModel = CreateViewModel(
            picker: picker,
            dialogs: dialogs,
            packageService: packageService,
            configurationStore: configurationStore,
            presetStore: presetStore);
        await viewModel.Presets.InitializeAsync(CancellationToken.None);

        await viewModel.ImportConfigurationCommand.ExecuteAsync(null);

        Assert.Empty(configurationStore.Saved);
        Assert.Empty(presetStore.SavedHistory);
        Assert.Contains(
            dialogs.Messages,
            message => message.Contains("自定义背景色") &&
                       message.Contains("当前配置未发生变化"));
    }

    [Fact]
    public async Task Import_configuration_rolls_back_when_preset_save_fails()
    {
        var packageService = new PackageService
        {
            ImportedPackage = ConfigurationPackage.Create(
                AppConfiguration.Default with { Theme = "Dark" },
                [new ProcessingPreset("导入方案", ProcessingRequest.Default)])
        };
        var picker = new Picker
        {
            ConfigurationImportPath = @"C:\backup\苏影枢配置.syconfig"
        };
        var dialogs = new Dialogs();
        var configurationStore = new ConfigurationStore();
        var presetStore = new PresetStore(
        [
            new ProcessingPreset("原方案", ProcessingRequest.Default)
        ])
        {
            FailNextSave = true
        };
        var viewModel = CreateViewModel(
            picker: picker,
            dialogs: dialogs,
            packageService: packageService,
            configurationStore: configurationStore,
            presetStore: presetStore);
        await viewModel.Presets.InitializeAsync(CancellationToken.None);
        var originalConfiguration = viewModel.BuildConfiguration();

        await viewModel.ImportConfigurationCommand.ExecuteAsync(null);

        Assert.Equal(originalConfiguration, configurationStore.Saved.Last());
        Assert.Equal(
            "原方案",
            Assert.Single(presetStore.SavedHistory.Last()).Name);
        Assert.Equal("System", viewModel.Appearance.Theme);
        Assert.Contains(
            dialogs.Messages,
            message => message.Contains("导入失败") && message.Contains("已恢复"));
    }

    [Fact]
    public async Task Import_failure_keeps_queue_and_shows_message()
    {
        var dialogs = new Dialogs();
        var viewModel = CreateViewModel(
            dialogs: dialogs,
            discovery: new FailingDiscovery());
        var existing = CreateItem("existing.jpg");
        viewModel.FileQueue.Items.Add(existing);

        await viewModel.AddPathsAsync(
            [@"C:\images"],
            true,
            CancellationToken.None);

        Assert.Equal([existing], viewModel.FileQueue.Items);
        Assert.Contains(
            dialogs.Messages,
            message => message.Contains("导入失败") &&
                       message.Contains("模拟路径错误"));
    }

    private static ImageQueueItemViewData CreateItem(string fileName) =>
        new(
            $@"C:\images\{fileName}",
            fileName,
            "800 × 600",
            "100 KB");

    private static MainWindowViewModel CreateViewModel(
        IImageProcessor? processor = null,
        Picker? picker = null,
        Dialogs? dialogs = null,
        PackageService? packageService = null,
        ConfigurationStore? configurationStore = null,
        PresetStore? presetStore = null,
        IImageFileDiscovery? discovery = null,
        IAiModelManager? aiModelManager = null)
    {
        var previewUseCase = new BuildPreviewUseCase(new PreviewRenderer());
        var settings = new ProcessingSettingsViewModel();
        presetStore ??= new PresetStore([]);
        dialogs ??= new Dialogs();
        return new MainWindowViewModel(
            new ImportImagesUseCase(discovery ?? new Discovery()),
            new MetadataReader(),
            new ImageProcessingPipeline(
                new ProcessingRequestValidator(),
                new PathResolver(),
                processor ?? new Processor()),
            new ProcessingRequestValidator(),
            picker ?? new Picker(),
            dialogs,
            packageService ?? new PackageService(),
            configurationStore ?? new ConfigurationStore(),
            presetStore,
            settings,
            new ProcessingPresetViewModel(
                presetStore,
                settings),
            new AiBackgroundRemovalViewModel(
                aiModelManager ?? new AiModelManager(),
                dialogs,
                picker ?? new Picker()),
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

    private sealed class FailingDiscovery : IImageFileDiscovery
    {
        public Task<ImageImportResult> DiscoverAsync(
            IEnumerable<string> inputPaths,
            bool includeSubdirectories,
            CancellationToken cancellationToken) =>
            throw new IOException("模拟路径错误");
    }

    private sealed class PresetStore : IProcessingPresetStore
    {
        private readonly IReadOnlyList<ProcessingPreset> _loaded;

        public PresetStore(IReadOnlyList<ProcessingPreset> loaded)
        {
            _loaded = loaded;
        }

        public bool FailNextSave { get; set; }

        public IReadOnlyList<ProcessingPreset> Saved { get; private set; } = [];

        public List<IReadOnlyList<ProcessingPreset>> SavedHistory { get; } = [];

        public Task<IReadOnlyList<ProcessingPreset>> LoadAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(_loaded);

        public Task SaveAsync(
            IReadOnlyList<ProcessingPreset> presets,
            CancellationToken cancellationToken)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new IOException("模拟预设保存失败");
            }

            Saved = presets.ToArray();
            SavedHistory.Add(Saved);
            return Task.CompletedTask;
        }
    }

    private sealed class ConfigurationStore : IConfigurationStore<AppConfiguration>
    {
        public List<AppConfiguration> Saved { get; } = [];

        public Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(AppConfiguration.Default);

        public Task SaveAsync(
            AppConfiguration configuration,
            CancellationToken cancellationToken)
        {
            Saved.Add(configuration);
            return Task.CompletedTask;
        }
    }

    private sealed class PackageService : IConfigurationPackageService
    {
        public string? ExportedPath { get; private set; }

        public ConfigurationPackage? ExportedPackage { get; private set; }

        public ConfigurationPackage ImportedPackage { get; set; } =
            ConfigurationPackage.Create(AppConfiguration.Default, []);

        public Task ExportAsync(
            string path,
            ConfigurationPackage package,
            CancellationToken cancellationToken)
        {
            ExportedPath = path;
            ExportedPackage = package;
            return Task.CompletedTask;
        }

        public Task<ConfigurationPackage> ImportAsync(
            string path,
            CancellationToken cancellationToken) =>
            Task.FromResult(ImportedPackage);
    }

    private sealed class AiModelManager : IAiModelManager
    {
        public Task<AiModelStatus> GetStatusAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiModelStatus(
                modelId,
                modelId,
                0,
                false,
                "test"));

        public Task InstallModelAsync(
            string modelId,
            IProgress<double>? progress,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<AiModelStatus> IdentifyLocalModelAsync(
            string sourcePath,
            IProgress<double>? progress,
            CancellationToken cancellationToken) =>
            GetStatusAsync("birefnet-portrait", cancellationToken);

        public Task ImportLocalModelAsync(
            string modelId,
            string sourcePath,
            IProgress<double>? progress,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveModelAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string> GetModelPathAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(modelId);
    }

    private sealed class CancellableAiModelManager : IAiModelManager
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AiModelStatus> GetStatusAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiModelStatus(
                modelId,
                modelId,
                1024,
                false,
                "test"));

        public async Task InstallModelAsync(
            string modelId,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public Task<AiModelStatus> IdentifyLocalModelAsync(
            string sourcePath,
            IProgress<double>? progress,
            CancellationToken cancellationToken) =>
            GetStatusAsync(AiModelManifest.PortraitModelId, cancellationToken);

        public Task ImportLocalModelAsync(
            string modelId,
            string sourcePath,
            IProgress<double>? progress,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveModelAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string> GetModelPathAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(modelId);
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
        public string? ConfigurationImportPath { get; init; }

        public string? ConfigurationExportPath { get; init; }

        public Task<IReadOnlyList<string>> PickFilesAsync() =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> PickFolderAsync() =>
            Task.FromResult<string?>(null);

        public Task<string?> PickConfigurationImportPathAsync() =>
            Task.FromResult(ConfigurationImportPath);

        public Task<string?> PickConfigurationExportPathAsync(
            string suggestedFileName) =>
            Task.FromResult(ConfigurationExportPath);

        public Task<string?> PickAiModelPathAsync() =>
            Task.FromResult<string?>(null);
    }

    private sealed class Dialogs : IUserDialogService
    {
        public bool ConfirmResult { get; init; } = true;

        public List<string> Messages { get; } = [];

        public void ShowMessage(string message, string title = "苏影枢")
        {
            Messages.Add(message);
        }

        public bool Confirm(string message, string title = "苏影枢") => ConfirmResult;
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
