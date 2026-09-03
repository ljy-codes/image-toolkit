using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToolkit.App.Services;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.AI;

namespace ImageToolkit.App.ViewModels;

public sealed partial class AiBackgroundRemovalViewModel : ObservableObject
{
    private readonly IAiModelManager _modelManager;
    private readonly IUserDialogService _dialogs;
    private readonly IDesktopFilePicker _filePicker;
    private CancellationTokenSource? _operationCancellation;
    private Task? _activeOperationTask;

    public AiBackgroundRemovalViewModel(
        IAiModelManager modelManager,
        IUserDialogService dialogs,
        IDesktopFilePicker filePicker)
    {
        _modelManager = modelManager;
        _dialogs = dialogs;
        _filePicker = filePicker;
    }

    [ObservableProperty]
    private string _portraitStatus = "正在检查";

    [ObservableProperty]
    private string _generalStatus = "正在检查";

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _notice;

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        RefreshAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallPortraitAsync() =>
        StartInstallAsync(AiModelManifest.PortraitModelId);

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallGeneralAsync() =>
        StartInstallAsync(AiModelManifest.GeneralModelId);

    [RelayCommand(CanExecute = nameof(CanModifyModels))]
    private Task RemovePortraitAsync() =>
        RemoveAsync(AiModelManifest.PortraitModelId);

    [RelayCommand(CanExecute = nameof(CanModifyModels))]
    private Task RemoveGeneralAsync() =>
        RemoveAsync(AiModelManifest.GeneralModelId);

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task ImportLocalModelAsync()
    {
        var task = ImportLocalModelCoreAsync();
        _activeOperationTask = task;
        return task;
    }

    [RelayCommand(CanExecute = nameof(CanCancelInstall))]
    private void CancelInstall()
    {
        Notice = "正在取消模型操作。";
        _operationCancellation?.Cancel();
    }

    public async Task CancelActiveInstallAsync()
    {
        _operationCancellation?.Cancel();
        if (_activeOperationTask is not null)
        {
            await _activeOperationTask.ConfigureAwait(true);
        }
    }

    private Task StartInstallAsync(string modelId)
    {
        var task = InstallAsync(modelId);
        _activeOperationTask = task;
        return task;
    }

    private async Task InstallAsync(string modelId)
    {
        if (IsBusy)
        {
            return;
        }

        AiModelStatus status;
        try
        {
            status = await _modelManager.GetStatusAsync(
                modelId,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            Notice = $"无法读取模型状态：{exception.Message}";
            return;
        }

        var manifest = AiModelManifest.Defaults.Single(item => item.ModelId == modelId);
        var action = status.IsInstalled ? "重新下载并安装" : "下载并安装";
        var size = status.SizeBytes / 1024d / 1024d;
        var confirmation =
            $"将{action}“{status.DisplayName}”。\n\n" +
            $"下载大小：约 {size:F0} MB\n" +
            $"来源：{manifest.DownloadUri.Host} 的 rembg 发布页（{status.License}）\n" +
            $"保存位置：本机应用数据目录\n" +
            $"隐私：图片不会上传；安装完成后可离线使用。\n\n" +
            "是否继续？";
        if (!_dialogs.Confirm(confirmation, "安装 AI 模型"))
        {
            Notice = "已取消模型安装。";
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        DownloadProgress = 0;
        Notice = "正在下载并校验模型，请保持网络连接。";
        try
        {
            var progress = new Progress<double>(value => DownloadProgress = value);
            await _modelManager.InstallModelAsync(
                modelId,
                progress,
                _operationCancellation.Token);
            Notice = "模型已安装，可断网使用。";
        }
        catch (OperationCanceledException)
        {
            Notice = "模型安装已取消，未完成文件已清理。";
        }
        catch (Exception exception)
        {
            Notice = $"模型安装失败：{exception.Message}";
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            IsBusy = false;
            await TryRefreshAsync();
        }
    }

    private async Task ImportLocalModelCoreAsync()
    {
        if (IsBusy)
        {
            return;
        }

        string? sourcePath;
        try
        {
            sourcePath = await _filePicker.PickAiModelPathAsync();
        }
        catch (Exception exception)
        {
            Notice = $"无法选择本地模型文件：{exception.Message}";
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        IsBusy = true;
        DownloadProgress = 0;
        Notice = "正在校验本地模型文件。";
        try
        {
            var progress = new Progress<double>(value => DownloadProgress = value);
            var candidate = await _modelManager.IdentifyLocalModelAsync(
                sourcePath,
                progress,
                _operationCancellation.Token);
            if (candidate.IsInstalled &&
                !_dialogs.Confirm(
                    $"已识别为“{candidate.DisplayName}”，本机已安装同一模型。\n\n" +
                    "是否使用所选文件替换现有模型？",
                    "替换 AI 模型"))
            {
                Notice = "已取消本地模型导入。";
                return;
            }

            DownloadProgress = 0;
            Notice = $"正在导入“{candidate.DisplayName}”。";
            await _modelManager.ImportLocalModelAsync(
                candidate.ModelId,
                sourcePath,
                progress,
                _operationCancellation.Token);
            Notice = $"“{candidate.DisplayName}”已从本地文件导入，可离线使用。";
        }
        catch (OperationCanceledException)
        {
            Notice = "模型操作已取消，未完成文件已清理。";
        }
        catch (Exception exception)
        {
            Notice = $"本地模型导入失败：{exception.Message}";
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            IsBusy = false;
            await TryRefreshAsync();
        }
    }

    private async Task RemoveAsync(string modelId)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            await _modelManager.RemoveModelAsync(modelId, CancellationToken.None);
            Notice = "模型已从本机删除。";
        }
        catch (Exception exception)
        {
            Notice = $"删除模型失败：{exception.Message}";
        }
        finally
        {
            await TryRefreshAsync();
        }
    }

    private async Task TryRefreshAsync()
    {
        try
        {
            await RefreshAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            Notice = $"{Notice} 模型状态刷新失败：{exception.Message}";
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var portrait = await _modelManager.GetStatusAsync(
            AiModelManifest.PortraitModelId,
            cancellationToken);
        var general = await _modelManager.GetStatusAsync(
            AiModelManifest.GeneralModelId,
            cancellationToken);
        PortraitStatus = FormatStatus(portrait.IsInstalled, portrait.SizeBytes);
        GeneralStatus = FormatStatus(general.IsInstalled, general.SizeBytes);
    }

    private static string FormatStatus(bool installed, long sizeBytes) =>
        installed
            ? $"已安装 · {sizeBytes / 1024d / 1024d:F0} MB"
            : $"未安装 · 需下载 {sizeBytes / 1024d / 1024d:F0} MB";

    private bool CanInstall() => !IsBusy;

    private bool CanModifyModels() => !IsBusy;

    private bool CanCancelInstall() =>
        IsBusy && _operationCancellation is not null;

    partial void OnIsBusyChanged(bool value)
    {
        InstallPortraitCommand.NotifyCanExecuteChanged();
        InstallGeneralCommand.NotifyCanExecuteChanged();
        ImportLocalModelCommand.NotifyCanExecuteChanged();
        RemovePortraitCommand.NotifyCanExecuteChanged();
        RemoveGeneralCommand.NotifyCanExecuteChanged();
        CancelInstallCommand.NotifyCanExecuteChanged();
    }
}
