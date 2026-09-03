using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Infrastructure.AI;

namespace ImageToolkit.App.ViewModels;

public sealed partial class AiBackgroundRemovalViewModel : ObservableObject
{
    private readonly IAiModelManager _modelManager;

    public AiBackgroundRemovalViewModel(IAiModelManager modelManager)
    {
        _modelManager = modelManager;
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

    [RelayCommand]
    private Task InstallPortraitAsync() =>
        InstallAsync(AiModelManifest.PortraitModelId);

    [RelayCommand]
    private Task InstallGeneralAsync() =>
        InstallAsync(AiModelManifest.GeneralModelId);

    [RelayCommand]
    private Task RemovePortraitAsync() =>
        RemoveAsync(AiModelManifest.PortraitModelId);

    [RelayCommand]
    private Task RemoveGeneralAsync() =>
        RemoveAsync(AiModelManifest.GeneralModelId);

    private async Task InstallAsync(string modelId)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        DownloadProgress = 0;
        Notice = "正在下载并校验模型，请保持网络连接。";
        try
        {
            var progress = new Progress<double>(value => DownloadProgress = value);
            await _modelManager.InstallModelAsync(
                modelId,
                progress,
                CancellationToken.None);
            Notice = "模型已安装，可断网使用。";
        }
        catch (Exception exception)
        {
            Notice = $"模型安装失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync(CancellationToken.None);
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
            await RefreshAsync(CancellationToken.None);
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
}
