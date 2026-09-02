using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageToolkit.App.Models;

namespace ImageToolkit.App.ViewModels;

public sealed partial class FileQueueViewModel : ObservableObject
{
    public ObservableCollection<ImageQueueItemViewData> Items { get; } = [];

    public event EventHandler? SelectedItemChanged;

    [ObservableProperty]
    private ImageQueueItemViewData? _selectedItem;

    partial void OnSelectedItemChanged(ImageQueueItemViewData? value) =>
        SelectedItemChanged?.Invoke(this, EventArgs.Empty);
}
