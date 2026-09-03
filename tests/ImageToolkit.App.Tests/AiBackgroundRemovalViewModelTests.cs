using ImageToolkit.App.Services;
using ImageToolkit.App.ViewModels;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.AI;

namespace ImageToolkit.App.Tests;

public sealed class AiBackgroundRemovalViewModelTests
{
    [Fact]
    public async Task Install_requires_confirmation_with_download_details()
    {
        var manager = new ModelManager();
        var dialogs = new Dialogs { ConfirmResult = false };
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            dialogs,
            new Picker());

        await viewModel.InstallPortraitCommand.ExecuteAsync(null);

        Assert.Empty(manager.InstalledModels);
        Assert.Contains("MB", dialogs.LastConfirmation);
        Assert.Contains("图片不会上传", dialogs.LastConfirmation);
        Assert.Contains("离线", dialogs.LastConfirmation);
    }

    [Fact]
    public async Task Active_install_can_be_cancelled()
    {
        var manager = new ModelManager { WaitForCancellation = true };
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            new Dialogs(),
            new Picker());

        var install = viewModel.InstallPortraitCommand.ExecuteAsync(null);
        await manager.InstallStarted.Task;

        viewModel.CancelInstallCommand.Execute(null);
        await install;

        Assert.True(manager.CancellationObserved);
        Assert.Contains("已取消", viewModel.Notice);
    }

    [Fact]
    public async Task Import_does_nothing_when_file_selection_is_cancelled()
    {
        var manager = new ModelManager();
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            new Dialogs(),
            new Picker());

        await viewModel.ImportLocalModelCommand.ExecuteAsync(null);

        Assert.Empty(manager.IdentifiedPaths);
        Assert.Empty(manager.ImportedModels);
    }

    [Fact]
    public async Task Import_identifies_and_installs_new_local_model()
    {
        var manager = new ModelManager();
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            new Dialogs(),
            new Picker { ModelPath = @"C:\Models\shared.onnx" });

        await viewModel.ImportLocalModelCommand.ExecuteAsync(null);

        Assert.Equal([@"C:\Models\shared.onnx"], manager.IdentifiedPaths);
        Assert.Equal(
            [(AiModelManifest.PortraitModelId, @"C:\Models\shared.onnx")],
            manager.ImportedModels);
        Assert.Contains("本地文件导入", viewModel.Notice);
    }

    [Fact]
    public async Task Import_requires_confirmation_before_replacing_installed_model()
    {
        var manager = new ModelManager
        {
            LocalCandidate = new AiModelStatus(
                AiModelManifest.GeneralModelId,
                "BiRefNet 高精度通用模型",
                928L * 1024 * 1024,
                true,
                "MIT")
        };
        var dialogs = new Dialogs { ConfirmResult = false };
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            dialogs,
            new Picker { ModelPath = @"C:\Models\shared.onnx" });

        await viewModel.ImportLocalModelCommand.ExecuteAsync(null);

        Assert.Empty(manager.ImportedModels);
        Assert.Contains("BiRefNet 高精度通用模型", dialogs.LastConfirmation);
        Assert.Contains("替换", dialogs.LastConfirmation);
    }

    [Fact]
    public async Task Import_replaces_installed_model_after_confirmation()
    {
        var manager = new ModelManager
        {
            LocalCandidate = new AiModelStatus(
                AiModelManifest.GeneralModelId,
                "BiRefNet 高精度通用模型",
                928L * 1024 * 1024,
                true,
                "MIT")
        };
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            new Dialogs(),
            new Picker { ModelPath = @"C:\Models\shared.onnx" });

        await viewModel.ImportLocalModelCommand.ExecuteAsync(null);

        Assert.Equal(
            [(AiModelManifest.GeneralModelId, @"C:\Models\shared.onnx")],
            manager.ImportedModels);
    }

    [Fact]
    public async Task Active_local_import_can_be_cancelled()
    {
        var manager = new ModelManager { WaitForImportCancellation = true };
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            new Dialogs(),
            new Picker { ModelPath = @"C:\Models\shared.onnx" });

        var import = viewModel.ImportLocalModelCommand.ExecuteAsync(null);
        await manager.ImportStarted.Task;

        viewModel.CancelInstallCommand.Execute(null);
        await import;

        Assert.True(manager.CancellationObserved);
        Assert.Contains("已取消", viewModel.Notice);
    }

    [Fact]
    public async Task File_picker_failure_is_reported_without_starting_import()
    {
        var manager = new ModelManager();
        var viewModel = new AiBackgroundRemovalViewModel(
            manager,
            new Dialogs(),
            new Picker { Failure = new IOException("模拟选择失败") });

        await viewModel.ImportLocalModelCommand.ExecuteAsync(null);

        Assert.Empty(manager.IdentifiedPaths);
        Assert.Contains("无法选择本地模型文件", viewModel.Notice);
        Assert.Contains("模拟选择失败", viewModel.Notice);
    }

    private sealed class ModelManager : IAiModelManager
    {
        public List<string> InstalledModels { get; } = [];

        public List<string> IdentifiedPaths { get; } = [];

        public List<(string ModelId, string SourcePath)> ImportedModels { get; } = [];

        public TaskCompletionSource InstallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ImportStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WaitForCancellation { get; init; }

        public bool WaitForImportCancellation { get; init; }

        public bool CancellationObserved { get; private set; }

        public AiModelStatus LocalCandidate { get; init; } =
            new(
                AiModelManifest.PortraitModelId,
                "BiRefNet 高精度人像模型",
                928L * 1024 * 1024,
                false,
                "MIT");

        public Task<AiModelStatus> GetStatusAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiModelStatus(
                modelId,
                modelId == AiModelManifest.PortraitModelId
                    ? "BiRefNet 高精度人像模型"
                    : "BiRefNet 高精度通用模型",
                928L * 1024 * 1024,
                false,
                "Apache-2.0"));

        public async Task InstallModelAsync(
            string modelId,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            InstalledModels.Add(modelId);
            InstallStarted.TrySetResult();
            if (!WaitForCancellation)
            {
                return;
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }

        public Task<AiModelStatus> IdentifyLocalModelAsync(
            string sourcePath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            IdentifiedPaths.Add(sourcePath);
            return Task.FromResult(LocalCandidate);
        }

        public async Task ImportLocalModelAsync(
            string modelId,
            string sourcePath,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            ImportedModels.Add((modelId, sourcePath));
            ImportStarted.TrySetResult();
            if (!WaitForImportCancellation)
            {
                return;
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }

        public Task RemoveModelAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<string> GetModelPathAsync(
            string modelId,
            CancellationToken cancellationToken) =>
            Task.FromResult(modelId);
    }

    private sealed class Dialogs : IUserDialogService
    {
        public bool ConfirmResult { get; init; } = true;

        public string LastConfirmation { get; private set; } = string.Empty;

        public void ShowMessage(string message, string title = "苏影枢")
        {
        }

        public bool Confirm(string message, string title = "苏影枢")
        {
            LastConfirmation = message;
            return ConfirmResult;
        }
    }

    private sealed class Picker : IDesktopFilePicker
    {
        public string? ModelPath { get; init; }

        public Exception? Failure { get; init; }

        public Task<IReadOnlyList<string>> PickFilesAsync() =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string?> PickFolderAsync() =>
            Task.FromResult<string?>(null);

        public Task<string?> PickConfigurationImportPathAsync() =>
            Task.FromResult<string?>(null);

        public Task<string?> PickConfigurationExportPathAsync(
            string suggestedFileName) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickAiModelPathAsync() =>
            Failure is null
                ? Task.FromResult(ModelPath)
                : Task.FromException<string?>(Failure);
    }
}
