using System.Windows;

namespace ImageToolkit.App.Services;

public interface IUserDialogService
{
    void ShowMessage(string message, string title = "苏影枢");

    bool Confirm(string message, string title = "苏影枢");
}

public sealed class UserDialogService : IUserDialogService
{
    public void ShowMessage(string message, string title = "苏影枢") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public bool Confirm(string message, string title = "苏影枢") =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
