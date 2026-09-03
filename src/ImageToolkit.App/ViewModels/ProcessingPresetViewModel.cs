using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageToolkit.Domain.Interfaces;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.App.ViewModels;

public sealed partial class ProcessingPresetViewModel : ObservableObject
{
    public const string DefaultPresetName = "默认参数";

    private readonly IProcessingPresetStore _store;
    private readonly ProcessingSettingsViewModel _settings;

    public ProcessingPresetViewModel(
        IProcessingPresetStore store,
        ProcessingSettingsViewModel settings)
    {
        _store = store;
        _settings = settings;
    }

    public ObservableCollection<ProcessingPreset> Items { get; } = [];

    [ObservableProperty]
    private ProcessingPreset? _selectedPreset;

    [ObservableProperty]
    private string _presetName = string.Empty;

    [ObservableProperty]
    private string? _notice;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ReplaceImported(await _store.LoadAsync(cancellationToken));
    }

    public IReadOnlyList<ProcessingPreset> GetUserPresets() =>
        Items
            .Where(item => !IsReservedName(item.Name))
            .ToArray();

    public void ReplaceImported(IReadOnlyList<ProcessingPreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);
        Items.Clear();
        Items.Add(new ProcessingPreset(DefaultPresetName, ProcessingRequest.Default));
        foreach (var preset in presets)
        {
            if (!IsReservedName(preset.Name) &&
                Items.All(item => !NamesEqual(item.Name, preset.Name)))
            {
                Items.Add(preset);
            }
        }

        SelectedPreset = Items[0];
        PresetName = string.Empty;
    }

    [RelayCommand]
    private void ApplySelected()
    {
        if (SelectedPreset is null)
        {
            Notice = "请先选择一个参数方案。";
            return;
        }

        _settings.Apply(SelectedPreset.Request);
        Notice = $"已应用“{SelectedPreset.Name}”。";
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        var name = PresetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Notice = "请输入方案名称。";
            return;
        }

        if (IsReservedName(name) || Items.Any(item => NamesEqual(item.Name, name)))
        {
            Notice = "方案名称已存在，请换一个名称。";
            return;
        }

        var preset = new ProcessingPreset(name, _settings.BuildRequest());
        Items.Add(preset);
        SelectedPreset = preset;
        PresetName = string.Empty;
        await PersistAsync();
        Notice = $"已保存“{name}”。";
    }

    [RelayCommand]
    private async Task UpdateSelectedAsync()
    {
        if (SelectedPreset is null || IsReservedName(SelectedPreset.Name))
        {
            Notice = "默认参数不能更新，请另存为新方案。";
            return;
        }

        var index = Items.IndexOf(SelectedPreset);
        var updated = SelectedPreset with { Request = _settings.BuildRequest() };
        Items[index] = updated;
        SelectedPreset = updated;
        await PersistAsync();
        Notice = $"已更新“{updated.Name}”。";
    }

    [RelayCommand]
    private async Task RenameSelectedAsync()
    {
        if (SelectedPreset is null || IsReservedName(SelectedPreset.Name))
        {
            Notice = "默认参数不能重命名。";
            return;
        }

        var name = PresetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Notice = "请输入新的方案名称。";
            return;
        }

        if (IsReservedName(name) ||
            Items.Any(item =>
                !ReferenceEquals(item, SelectedPreset) &&
                NamesEqual(item.Name, name)))
        {
            Notice = "方案名称已存在，请换一个名称。";
            return;
        }

        var index = Items.IndexOf(SelectedPreset);
        var renamed = SelectedPreset with { Name = name };
        Items[index] = renamed;
        SelectedPreset = renamed;
        PresetName = string.Empty;
        await PersistAsync();
        Notice = $"已重命名为“{name}”。";
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedPreset is null || IsReservedName(SelectedPreset.Name))
        {
            Notice = "默认参数不能删除。";
            return;
        }

        var name = SelectedPreset.Name;
        Items.Remove(SelectedPreset);
        SelectedPreset = Items[0];
        await PersistAsync();
        Notice = $"已删除“{name}”。";
    }

    private Task PersistAsync() =>
        _store.SaveAsync(
            Items.Where(item => !IsReservedName(item.Name)).ToArray(),
            CancellationToken.None);

    private static bool IsReservedName(string name) =>
        NamesEqual(name, DefaultPresetName);

    private static bool NamesEqual(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
