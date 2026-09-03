using System.Collections;
using System.Collections.Specialized;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToolkit.App.Models;
using ImageToolkit.App.Services;
using ImageToolkit.Application.Batch;
using ImageToolkit.Application.Import;
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Config;

namespace ImageToolkit.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ImportImagesUseCase _importImages;
    private readonly IImageMetadataReader _metadataReader;
    private readonly ImageProcessingPipeline _pipeline;
    private readonly ProcessingRequestValidator _validator;
    private readonly IDesktopFilePicker _filePicker;
    private readonly IUserDialogService _dialogs;
    private readonly IConfigurationPackageService _configurationPackageService;
    private readonly IConfigurationStore<AppConfiguration> _configurationStore;
    private readonly IProcessingPresetStore _presetStore;
    private readonly object _batchStateLock = new();
    private BatchTaskCoordinator? _coordinator;
    private CancellationTokenSource? _batchCancellation;
    private CancellationTokenSource? _previewCancellation;
    private Task? _activeBatchTask;
    private long _batchVersion;

    public MainWindowViewModel(
        ImportImagesUseCase importImages,
        IImageMetadataReader metadataReader,
        ImageProcessingPipeline pipeline,
        ProcessingRequestValidator validator,
        IDesktopFilePicker filePicker,
        IUserDialogService dialogs,
        IConfigurationPackageService configurationPackageService,
        IConfigurationStore<AppConfiguration> configurationStore,
        IProcessingPresetStore presetStore,
        ProcessingSettingsViewModel settings,
        ProcessingPresetViewModel presets,
        AiBackgroundRemovalViewModel aiBackgroundRemoval,
        AppearanceSettingsViewModel appearance,
        PreviewViewModel preview,
        FileQueueViewModel fileQueue,
        BatchProgressViewModel progress)
    {
        _importImages = importImages;
        _metadataReader = metadataReader;
        _pipeline = pipeline;
        _validator = validator;
        _filePicker = filePicker;
        _dialogs = dialogs;
        _configurationPackageService = configurationPackageService;
        _configurationStore = configurationStore;
        _presetStore = presetStore;
        Settings = settings;
        Presets = presets;
        AiBackgroundRemoval = aiBackgroundRemoval;
        Appearance = appearance;
        Preview = preview;
        FileQueue = fileQueue;
        Progress = progress;
        FileQueue.Items.CollectionChanged += OnQueueChanged;
        FileQueue.SelectedItemChanged += OnSelectedItemChanged;
        Settings.PropertyChanged += OnProcessingSettingsChanged;
        AiBackgroundRemoval.PropertyChanged += OnAiBackgroundRemovalChanged;
    }

    public ProcessingSettingsViewModel Settings { get; }

    public ProcessingPresetViewModel Presets { get; }

    public AiBackgroundRemovalViewModel AiBackgroundRemoval { get; }

    public AppearanceSettingsViewModel Appearance { get; }

    public PreviewViewModel Preview { get; }

    public FileQueueViewModel FileQueue { get; }

    public BatchProgressViewModel Progress { get; }

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _includeSubdirectories;

    [ObservableProperty]
    private string _workspaceTitle = "图片处理工作台";

    [ObservableProperty]
    private string _pauseButtonText = "暂停";

    [ObservableProperty]
    private bool _hasCompletedBatch;

    public bool ShowStartAction => !HasCompletedBatch;

    public bool ShowNewBatchAction => HasCompletedBatch;

    public bool HasActiveWork => IsRunning || AiBackgroundRemoval.IsBusy;

    [ObservableProperty]
    private string? _configurationNotice;

    [ObservableProperty]
    private bool _isCompletionNoticeVisible;

    [ObservableProperty]
    private string _completionNoticeText = string.Empty;

    private bool CanStart() =>
        FileQueue.Items.Count > 0 &&
        !IsRunning &&
        !AiBackgroundRemoval.IsBusy;

    private bool CanEditQueue() => !IsRunning;

    private bool CanRemoveSelected(IList? selectedItems) =>
        selectedItems is { Count: > 0 } && !IsRunning;

    private bool CanPauseOrCancel() => IsRunning;

    private bool CanRetryFailed() =>
        !IsRunning &&
        !AiBackgroundRemoval.IsBusy &&
        FileQueue.Items.Any(IsRetryable);

    private bool CanStartNextBatch() => HasCompletedBatch && !IsRunning;

    private bool CanTransferConfiguration() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanTransferConfiguration))]
    private async Task ExportConfigurationAsync()
    {
        var suggestedFileName =
            $"苏影枢配置-{DateTime.Now:yyyyMMdd-HHmmss}.syconfig";
        var path = await _filePicker.PickConfigurationExportPathAsync(
            suggestedFileName);
        if (path is null)
        {
            return;
        }

        try
        {
            var package = ConfigurationPackage.Create(
                BuildConfiguration(),
                Presets.GetUserPresets());
            await _configurationPackageService.ExportAsync(
                path,
                package,
                CancellationToken.None);
            ConfigurationNotice =
                $"配置已导出，包含 {package.Presets.Length} 个命名方案。";
            _dialogs.ShowMessage(
                $"{ConfigurationNotice}\n文件：{path}");
        }
        catch (Exception exception)
        {
            ConfigurationNotice =
                $"配置导出失败：{exception.Message} 本机原配置未受影响。";
            _dialogs.ShowMessage(ConfigurationNotice);
        }
    }

    [RelayCommand(CanExecute = nameof(CanTransferConfiguration))]
    private async Task ImportConfigurationAsync()
    {
        var path = await _filePicker.PickConfigurationImportPathAsync();
        if (path is null)
        {
            return;
        }

        ConfigurationPackage package;
        try
        {
            package = await _configurationPackageService.ImportAsync(
                path,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            ConfigurationNotice =
                $"配置导入失败：{exception.Message} 当前配置未发生变化。";
            _dialogs.ShowMessage(ConfigurationNotice);
            return;
        }

        var normalized = NormalizeImportedPackage(package);
        var validationError = ValidateImportedPackage(normalized.Package);
        if (validationError is not null)
        {
            ConfigurationNotice =
                $"配置导入失败：{validationError} 当前配置未发生变化。";
            _dialogs.ShowMessage(ConfigurationNotice);
            return;
        }

        if (!_dialogs.Confirm(
                $"配置包导出时间：{package.ExportedAt:yyyy-MM-dd HH:mm:ss zzz}\n" +
                $"包含 {package.Presets.Length} 个命名方案。\n" +
                "导入将覆盖当前处理参数、外观设置和全部命名方案，是否继续？"))
        {
            ConfigurationNotice = "已取消配置导入，当前配置未发生变化。";
            return;
        }

        var originalConfiguration = BuildConfiguration();
        var originalPresets = Presets.GetUserPresets();
        try
        {
            await _configurationStore.SaveAsync(
                normalized.Package.Configuration,
                CancellationToken.None);
            await _presetStore.SaveAsync(
                normalized.Package.Presets,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            var rollbackError = await TryRestoreConfigurationAsync(
                originalConfiguration,
                originalPresets);
            var rollbackMessage = rollbackError is null
                ? "已恢复导入前配置。"
                : $"恢复导入前配置时也发生错误：{rollbackError.Message}";
            ConfigurationNotice =
                $"配置导入失败：{exception.Message} {rollbackMessage}";
            _dialogs.ShowMessage(ConfigurationNotice);
            return;
        }

        ApplyConfiguration(normalized.Package.Configuration);
        Presets.ReplaceImported(normalized.Package.Presets);
        var pathMessage = normalized.RepairedPathCount == 0
            ? "所有指定输出目录均可用。"
            : $"{normalized.RepairedPathCount} 个指定输出目录在本机不存在或不可用，" +
              "已改为“原目录新文件”，避免处理时写入失败。";
        ConfigurationNotice =
            $"配置导入成功，共导入 {normalized.Package.Presets.Length} 个命名方案。" +
            pathMessage;
        _dialogs.ShowMessage(ConfigurationNotice);
    }

    public AppConfiguration BuildConfiguration() =>
        new(
            Settings.BuildRequest(),
            0,
            Appearance.Theme,
            IncludeSubdirectories,
            Appearance.WorkspaceBackground,
            Appearance.CustomWorkspaceColor,
            Appearance.FontSize);

    public void ApplyConfiguration(AppConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Settings.Apply(configuration.Processing);
        Appearance.Apply(
            configuration.Theme,
            configuration.WorkspaceBackground,
            configuration.CustomWorkspaceColor,
            configuration.FontSize);
        IncludeSubdirectories = configuration.IncludeSubdirectories;
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private async Task AddFilesAsync()
    {
        var files = await _filePicker.PickFilesAsync();
        await AddPathsAsync(files, false, CancellationToken.None);
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private async Task AddFolderAsync()
    {
        var folder = await _filePicker.PickFolderAsync();
        if (folder is not null)
        {
            await AddPathsAsync([folder], IncludeSubdirectories, CancellationToken.None);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private async Task ChooseOutputFolderAsync()
    {
        var folder = await _filePicker.PickFolderAsync();
        if (folder is null)
        {
            return;
        }

        Settings.OutputDirectory = folder;
        Settings.OutputMode = OutputMode.SpecificDirectory;
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected(IList? selectedItems)
    {
        if (selectedItems is null)
        {
            return;
        }

        var itemsToRemove = selectedItems
            .OfType<ImageQueueItemViewData>()
            .ToArray();
        foreach (var item in itemsToRemove)
        {
            FileQueue.Items.Remove(item);
        }

        FileQueue.SelectedItem = FileQueue.Items.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(CanEditQueue))]
    private void ClearQueue()
    {
        ResetBatchState();
        HasCompletedBatch = false;
    }

    [RelayCommand(CanExecute = nameof(CanStartNextBatch))]
    private void StartNextBatch()
    {
        ResetBatchState();
        HasCompletedBatch = false;
    }

    private void ResetBatchState()
    {
        lock (_batchStateLock)
        {
            Interlocked.Increment(ref _batchVersion);
            IsCompletionNoticeVisible = false;
            CompletionNoticeText = string.Empty;
            FileQueue.Items.Clear();
            FileQueue.SelectedItem = null;
            Preview.Clear();
            Progress.Reset(0);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() =>
        RunTrackedBatchAsync(FileQueue.Items.ToArray());

    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private Task RetryFailedAsync()
    {
        var itemsToRetry = FileQueue.Items
            .Where(IsRetryable)
            .ToArray();
        return RunTrackedBatchAsync(itemsToRetry);
    }

    private Task RunTrackedBatchAsync(
        IReadOnlyList<ImageQueueItemViewData> itemsToRun)
    {
        var task = RunBatchAsync(itemsToRun);
        _activeBatchTask = task;
        return AwaitTrackedBatchAsync(task);
    }

    private async Task AwaitTrackedBatchAsync(Task task)
    {
        try
        {
            await task;
        }
        finally
        {
            if (ReferenceEquals(_activeBatchTask, task))
            {
                _activeBatchTask = null;
            }
        }
    }

    private async Task RunBatchAsync(IReadOnlyList<ImageQueueItemViewData> itemsToRun)
    {
        if (itemsToRun.Count == 0)
        {
            return;
        }

        var request = Settings.BuildRequest();
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            _dialogs.ShowMessage(validation.Errors[0].Message);
            return;
        }

        if (request.Output.Mode == OutputMode.OverwriteOriginal &&
            !_dialogs.Confirm("覆盖原文件会替换图片内容，是否继续？"))
        {
            return;
        }

        IsRunning = true;
        IsPaused = false;
        HasCompletedBatch = false;
        IsCompletionNoticeVisible = false;
        CompletionNoticeText = string.Empty;
        _previewCancellation?.Cancel();
        var batchVersion = Interlocked.Increment(ref _batchVersion);
        _batchCancellation = new CancellationTokenSource();
        Progress.Reset(itemsToRun.Count);
        foreach (var item in itemsToRun)
        {
            item.Status = "等待";
            item.OutputPath = null;
        }

        var lookup = itemsToRun.ToDictionary(
            item => item.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        _coordinator = new BatchTaskCoordinator(
            (item, snapshot, token) =>
                _pipeline.ProcessAsync(
                    lookup[item.SourcePath].ImportEntry,
                    snapshot,
                    token));
        var progress = new Progress<BatchItem>(item =>
        {
            lock (_batchStateLock)
            {
                if (batchVersion != Volatile.Read(ref _batchVersion))
                {
                    return;
                }

                if (!lookup.TryGetValue(item.SourcePath, out var viewData))
                {
                    return;
                }

                viewData.Status = ToChineseStatus(item.Status);
                viewData.OutputPath = item.Result?.OutputPath;
                viewData.ResultDetails = FormatResultDetails(item.Result);
                viewData.Message = item.Result?.Message;
                if (item.Status != BatchItemStatus.Processing)
                {
                    Progress.Advance();
                }

                RetryFailedCommand.NotifyCanExecuteChanged();
            }
        });

        try
        {
            var summary = await _coordinator.RunAsync(
                itemsToRun.Select(item => BatchItem.Waiting(item.SourcePath)),
                request,
                0,
                progress,
                _batchCancellation.Token);
            Progress.StatusText = summary.State switch
            {
                BatchRunState.Completed => $"处理完成，共 {summary.Completed} 张",
                BatchRunState.Cancelled => "任务已取消",
                _ => $"处理完成：成功 {summary.Completed}，未达标 {summary.Unmet}，失败 {summary.Failed}"
            };
            CompletionNoticeText = summary.State switch
            {
                BatchRunState.Completed =>
                    $"已完成\n成功处理 {summary.Completed} 张图片。",
                BatchRunState.Cancelled =>
                    $"已完成\n任务已取消。成功 {summary.Completed} 张，取消 {summary.Cancelled} 张。",
                _ =>
                    $"已完成\n成功 {summary.Completed} 张，未达标 {summary.Unmet} 张，失败 {summary.Failed} 张。" +
                    "\n请在列表中查看每张图片的明确原因。"
            };
            IsCompletionNoticeVisible = true;
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            HasCompletedBatch = true;
            PauseButtonText = "暂停";
            _batchCancellation.Dispose();
            _batchCancellation = null;
            _coordinator = null;
            RefreshPreview();
        }
    }

    [RelayCommand]
    private void DismissCompletionNotice() =>
        IsCompletionNoticeVisible = false;

    [RelayCommand(CanExecute = nameof(CanPauseOrCancel))]
    private void PauseResume()
    {
        if (_coordinator is null)
        {
            return;
        }

        if (IsPaused)
        {
            _coordinator.Resume();
            IsPaused = false;
            PauseButtonText = "暂停";
            Progress.StatusText = "继续处理";
        }
        else
        {
            _coordinator.Pause();
            IsPaused = true;
            PauseButtonText = "继续";
            Progress.StatusText = "已暂停";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPauseOrCancel))]
    private void Cancel()
    {
        Progress.StatusText = "正在安全取消";
        _batchCancellation?.Cancel();
    }

    public async Task AddPathsAsync(
        IEnumerable<string> paths,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            return;
        }

        ImageImportResult result;
        try
        {
            result = await _importImages.ExecuteAsync(
                paths,
                includeSubdirectories,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _dialogs.ShowMessage(
                $"导入失败：{exception.Message}",
                "无法添加图片");
            return;
        }
        if (result.Entries.Count > 0 && HasCompletedBatch)
        {
            StartNextBatch();
        }

        var existing = FileQueue.Items
            .Select(item => item.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in result.Entries)
        {
            var path = entry.SourcePath;
            if (!existing.Add(path))
            {
                continue;
            }

            try
            {
                var metadata = await _metadataReader.ReadAsync(path, cancellationToken);
                FileQueue.Items.Add(new ImageQueueItemViewData(
                    metadata.SourcePath,
                    metadata.FileName,
                    $"{metadata.PixelSize.Width} × {metadata.PixelSize.Height}",
                    FormatFileSize(metadata.SizeBytes),
                    entry));
            }
            catch (Exception exception)
            {
                _dialogs.ShowMessage($"无法读取 {Path.GetFileName(path)}：{exception.Message}");
            }
        }

        if (FileQueue.SelectedItem is null)
        {
            FileQueue.SelectedItem = FileQueue.Items.FirstOrDefault();
        }

        if (result.Rejected.Count > 0)
        {
            _dialogs.ShowMessage($"已忽略 {result.Rejected.Count} 个不支持或无法访问的项目。");
        }
    }

    public async Task<bool> PrepareCloseAsync()
    {
        var isInstallingModel = AiBackgroundRemoval.IsBusy;
        if (!IsRunning && !isInstallingModel)
        {
            return true;
        }

        var message = IsRunning && isInstallingModel
            ? "当前批处理和 AI 模型操作仍在运行。取消它们并退出吗？"
            : IsRunning
                ? "当前任务仍在运行。取消任务并退出吗？"
                : "AI 模型操作仍在运行。取消操作并退出吗？";
        if (!_dialogs.Confirm(message))
        {
            return false;
        }

        _batchCancellation?.Cancel();
        if (isInstallingModel)
        {
            await AiBackgroundRemoval.CancelActiveInstallAsync();
        }

        var activeBatch = _activeBatchTask;
        if (activeBatch is not null)
        {
            await activeBatch;
        }

        return true;
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(HasActiveWork));
        NotifyCommandStates();
    }

    partial void OnHasCompletedBatchChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowStartAction));
        OnPropertyChanged(nameof(ShowNewBatchAction));
    }

    private void OnQueueChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        NotifyCommandStates();

    private void OnSelectedItemChanged(object? sender, EventArgs e)
    {
        RefreshPreview();
    }

    private void OnProcessingSettingsChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e) =>
        RefreshPreview();

    private void OnAiBackgroundRemovalChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AiBackgroundRemovalViewModel.IsBusy))
        {
            return;
        }

        OnPropertyChanged(nameof(HasActiveWork));
        NotifyCommandStates();
    }

    private void RefreshPreview()
    {
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        _previewCancellation = new CancellationTokenSource();

        var selected = FileQueue.SelectedItem;
        if (selected is null)
        {
            Preview.Clear();
            return;
        }

        if (IsRunning)
        {
            return;
        }

        _ = RefreshPreviewAfterDelayAsync(
            selected.SourcePath,
            Settings.BuildRequest(),
            _previewCancellation.Token);
    }

    private async Task RefreshPreviewAfterDelayAsync(
        string sourcePath,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            if (!IsRunning)
            {
                await Preview.UpdateAsync(sourcePath, request, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void NotifyCommandStates()
    {
        StartCommand.NotifyCanExecuteChanged();
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        ChooseOutputFolderCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        RetryFailedCommand.NotifyCanExecuteChanged();
        ClearQueueCommand.NotifyCanExecuteChanged();
        StartNextBatchCommand.NotifyCanExecuteChanged();
        PauseResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ImportConfigurationCommand.NotifyCanExecuteChanged();
        ExportConfigurationCommand.NotifyCanExecuteChanged();
    }

    private string? ValidateImportedPackage(ConfigurationPackage package)
    {
        var configuration = package.Configuration;
        if (configuration.Theme is not ("System" or "Light" or "Dark"))
        {
            return $"主题值“{configuration.Theme}”无效。";
        }

        if (configuration.WorkspaceBackground is not (
                "System" or
                "White" or
                "LightGray" or
                "DarkGray" or
                "Black" or
                "Custom"))
        {
            return $"工作区背景值“{configuration.WorkspaceBackground}”无效。";
        }

        if (configuration.FontSize is not (12d or 14d or 16d or 18d))
        {
            return $"字体大小 {configuration.FontSize} 无效。";
        }

        if (configuration.WorkspaceBackground == "Custom" &&
            !IsValidColor(configuration.CustomWorkspaceColor))
        {
            return "自定义背景色无效，请使用有效的颜色值。";
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in package.Presets)
        {
            if (preset is null)
            {
                return "命名方案中存在空项目。";
            }

            var name = preset.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return "命名方案名称不能为空。";
            }

            if (string.Equals(
                    name,
                    ProcessingPresetViewModel.DefaultPresetName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return $"“{ProcessingPresetViewModel.DefaultPresetName}”是保留名称。";
            }

            if (!names.Add(name))
            {
                return $"命名方案“{name}”重复。";
            }

            var presetValidation = _validator.Validate(preset.Request);
            if (!presetValidation.IsValid)
            {
                return $"命名方案“{name}”无效：{presetValidation.Errors[0].Message}";
            }
        }

        var configurationValidation = _validator.Validate(
            package.Configuration.Processing);
        return configurationValidation.IsValid
            ? null
            : $"当前处理参数无效：{configurationValidation.Errors[0].Message}";
    }

    private static bool IsValidColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return System.Windows.Media.ColorConverter.ConvertFromString(value)
                   is System.Windows.Media.Color;
        }
        catch (Exception exception)
            when (exception is FormatException or NotSupportedException)
        {
            return false;
        }
    }

    private static NormalizedConfigurationPackage NormalizeImportedPackage(
        ConfigurationPackage package)
    {
        var repairedPathCount = 0;
        var configuration = package.Configuration with
        {
            Processing = NormalizeOutputPath(
                package.Configuration.Processing,
                ref repairedPathCount)
        };
        var presets = package.Presets
            .Select(preset => preset with
            {
                Name = preset.Name.Trim(),
                Request = NormalizeOutputPath(
                    preset.Request,
                    ref repairedPathCount)
            })
            .ToArray();
        return new NormalizedConfigurationPackage(
            package with
            {
                Configuration = configuration,
                Presets = presets
            },
            repairedPathCount);
    }

    private static ProcessingRequest NormalizeOutputPath(
        ProcessingRequest request,
        ref int repairedPathCount)
    {
        if (request.Output.Mode != OutputMode.SpecificDirectory ||
            (!string.IsNullOrWhiteSpace(request.Output.DirectoryPath) &&
             Directory.Exists(request.Output.DirectoryPath)))
        {
            return request;
        }

        repairedPathCount++;
        return request with
        {
            Output = request.Output with
            {
                Mode = OutputMode.SourceDirectory,
                DirectoryPath = null
            }
        };
    }

    private async Task<Exception?> TryRestoreConfigurationAsync(
        AppConfiguration configuration,
        IReadOnlyList<ProcessingPreset> presets)
    {
        try
        {
            await _configurationStore.SaveAsync(
                configuration,
                CancellationToken.None);
            await _presetStore.SaveAsync(presets, CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed record NormalizedConfigurationPackage(
        ConfigurationPackage Package,
        int RepairedPathCount);

    private static string ToChineseStatus(BatchItemStatus status) =>
        status switch
        {
            BatchItemStatus.Completed => "已完成",
            BatchItemStatus.Unmet => "未达标",
            BatchItemStatus.Failed => "失败",
            BatchItemStatus.Cancelled => "已取消",
            BatchItemStatus.Processing => "处理中",
            _ => "等待"
        };

    private static bool IsRetryable(ImageQueueItemViewData item) =>
        item.Status is "失败" or "未达标" or "已取消";

    private static string? FormatResultDetails(ImageProcessingResult? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result.Status is ImageProcessingStatus.Unmet or ImageProcessingStatus.Failed)
        {
            var diagnostic = result.Diagnostic;
            if (diagnostic is null)
            {
                return result.Message ?? "处理失败。";
            }

            var suggestions = diagnostic.Suggestions.Count > 0
                ? $" 建议：{string.Join("；", diagnostic.Suggestions)}"
                : string.Empty;
            return
                $"失败环节：{diagnostic.Stage}。{diagnostic.UserMessage}{suggestions}";
        }

        var parts = new List<string>();
        if (result.OutputSizeBytes > 0)
        {
            parts.Add(FormatFileSize(result.OutputSizeBytes));
        }

        if (result.FinalSize is not null)
        {
            parts.Add(
                $"{result.FinalSize.Value.Width} × {result.FinalSize.Value.Height}");
        }

        if (result.Quality is not null)
        {
            parts.Add($"质量 {result.Quality}");
        }

        return parts.Count == 0 ? result.Message : string.Join(" · ", parts);
    }

    private static string FormatFileSize(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024d / 1024d:F2} MB"
            : $"{Math.Max(1, bytes / 1024d):F0} KB";
}
