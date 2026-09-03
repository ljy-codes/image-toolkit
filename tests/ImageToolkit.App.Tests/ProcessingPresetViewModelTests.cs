using ImageToolkit.App.ViewModels;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.App.Tests;

public sealed class ProcessingPresetViewModelTests
{
    [Fact]
    public async Task Renames_selected_preset_and_persists_it()
    {
        var store = new RecordingPresetStore(
        [
            new ProcessingPreset("旧方案", ProcessingRequest.Default)
        ]);
        var viewModel = new ProcessingPresetViewModel(
            store,
            new ProcessingSettingsViewModel());
        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.SelectedPreset = viewModel.Items.Single(
            item => item.Name == "旧方案");
        viewModel.PresetName = "电商图片";

        await viewModel.RenameSelectedCommand.ExecuteAsync(null);

        Assert.Equal("电商图片", viewModel.SelectedPreset?.Name);
        Assert.Equal("电商图片", Assert.Single(store.Saved).Name);
        Assert.DoesNotContain(
            viewModel.Items,
            item => item.Name == "旧方案");
    }

    [Fact]
    public async Task Default_preset_cannot_be_renamed()
    {
        var store = new RecordingPresetStore([]);
        var viewModel = new ProcessingPresetViewModel(
            store,
            new ProcessingSettingsViewModel());
        await viewModel.InitializeAsync(CancellationToken.None);
        viewModel.PresetName = "新名称";

        await viewModel.RenameSelectedCommand.ExecuteAsync(null);

        Assert.Equal(ProcessingPresetViewModel.DefaultPresetName, viewModel.SelectedPreset?.Name);
        Assert.Empty(store.Saved);
        Assert.Contains("不能重命名", viewModel.Notice);
    }

    private sealed class RecordingPresetStore : IProcessingPresetStore
    {
        private readonly IReadOnlyList<ProcessingPreset> _loaded;

        public RecordingPresetStore(IReadOnlyList<ProcessingPreset> loaded)
        {
            _loaded = loaded;
        }

        public IReadOnlyList<ProcessingPreset> Saved { get; private set; } = [];

        public Task<IReadOnlyList<ProcessingPreset>> LoadAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(_loaded);

        public Task SaveAsync(
            IReadOnlyList<ProcessingPreset> presets,
            CancellationToken cancellationToken)
        {
            Saved = presets;
            return Task.CompletedTask;
        }
    }
}
