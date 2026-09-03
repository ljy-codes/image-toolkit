using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using ImageToolkit.App.Services;
using ImageToolkit.App.ViewModels;

namespace ImageToolkit.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IUserDialogService _dialogs;
    private bool _closeApproved;

    public MainWindow(
        MainWindowViewModel viewModel,
        IUserDialogService dialogs)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _dialogs = dialogs;
        DataContext = viewModel;
    }

    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        try
        {
            await _viewModel.AddPathsAsync(
                paths,
                _viewModel.IncludeSubdirectories,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _dialogs.ShowMessage(
                $"拖放导入失败：{exception.Message}",
                "无法添加图片");
        }
    }

    private void OnQueueSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string color })
        {
            _viewModel.Settings.CustomBackgroundColor = color;
            BackgroundColorPickerPopup.IsOpen = false;
        }
    }

    private void OnChooseCustomBackgroundColor(object sender, RoutedEventArgs e)
    {
        BackgroundColorPickerPopup.IsOpen = true;
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_closeApproved || !_viewModel.HasActiveWork)
        {
            return;
        }

        e.Cancel = true;
        if (await _viewModel.PrepareCloseAsync())
        {
            _closeApproved = true;
            Close();
        }
    }
}
