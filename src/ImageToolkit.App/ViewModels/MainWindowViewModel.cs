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

namespace ImageToolkit.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ImportImagesUseCase _importImages;
    private readonly IImageMetadataReader _metadataReader;
    private readonly ImageProcessingPipeline _pipeline;
    private readonly ProcessingRequestValidator _validator;
    private readonly IDesktopFilePicker _filePicker;
    private readonly IUserDialogService _dialogs;
    private BatchTaskCoordinator? _coordinator;
    private CancellationTokenSource? _batchCancellation;
    private CancellationTokenSource? _previewCancellation;
    private Task? _activeBatchTask;

    public MainWindowViewModel(
        ImportImagesUseCase importImages,
        IImageMetadataReader metadataReader,
        ImageProcessingPipeline pipeline,
        ProcessingRequestValidator validator,
        IDesktopFilePicker filePicker,
        IUserDialogService dialogs,
        ProcessingSettingsViewModel settings,
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
        Settings = settings;
        Appearance = appearance;
        Preview = preview;
        FileQueue = fileQueue;
        Progress = progress;
        FileQueue.Items.CollectionChanged += OnQueueChanged;
        FileQueue.SelectedItemChanged += OnSelectedItemChanged;
        Settings.PropertyChanged += OnProcessingSettingsChanged;
    }

    public ProcessingSettingsViewModel Settings { get; }

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

    private bool CanStart() => FileQueue.Items.Count > 0 && !IsRunning;

    private bool CanEditQueue() => !IsRunning;

    private bool CanRemoveSelected(IList? selectedItems) =>
        selectedItems is { Count: > 0 } && !IsRunning;

    private bool CanPauseOrCancel() => IsRunning;

    private bool CanRetryFailed() =>
        !IsRunning && FileQueue.Items.Any(IsRetryable);

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
        FileQueue.Items.Clear();
        FileQueue.SelectedItem = null;
        Preview.Clear();
        Progress.Reset(0);
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
                _pipeline.ProcessAsync(item.SourcePath, snapshot, token));
        var progress = new Progress<BatchItem>(item =>
        {
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
        }
        finally
        {
            IsRunning = false;
            IsPaused = false;
            PauseButtonText = "暂停";
            _batchCancellation.Dispose();
            _batchCancellation = null;
            _coordinator = null;
        }
    }

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

        var result = await _importImages.ExecuteAsync(
            paths,
            includeSubdirectories,
            cancellationToken);
        var existing = FileQueue.Items
            .Select(item => item.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in result.Files)
        {
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
                    FormatFileSize(metadata.SizeBytes)));
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
        if (!IsRunning)
        {
            return true;
        }

        if (!_dialogs.Confirm("当前任务仍在运行。取消任务并退出吗？"))
        {
            return false;
        }

        _batchCancellation?.Cancel();
        var activeBatch = _activeBatchTask;
        if (activeBatch is not null)
        {
            await activeBatch;
        }

        return true;
    }

    partial void OnIsRunningChanged(bool value) => NotifyCommandStates();

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

        _ = Preview.UpdateAsync(
            selected.SourcePath,
            Settings.BuildRequest(),
            _previewCancellation.Token);
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
        PauseResumeCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

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
