using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using ImageToolkit.App.ViewModels;

namespace ImageToolkit.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _closeApproved;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        await _viewModel.AddPathsAsync(
            paths,
            _viewModel.IncludeSubdirectories,
            CancellationToken.None);
    }

    private void OnQueueSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.RemoveSelectedCommand.NotifyCanExecuteChanged();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_closeApproved || !_viewModel.IsRunning)
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
