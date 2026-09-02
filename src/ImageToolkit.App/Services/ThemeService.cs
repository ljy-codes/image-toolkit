using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ImageToolkit.App.Services;

public interface IThemeService
{
    void Apply(
        string theme,
        string workspaceBackground,
        string customWorkspaceColor,
        double fontSize);
}

public sealed class ThemeService : IThemeService
{
    public void Apply(
        string theme,
        string workspaceBackground,
        string customWorkspaceColor,
        double fontSize)
    {
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            return;
        }

        var useDark = theme switch
        {
            "Dark" => true,
            "Light" => false,
            _ => IsSystemDarkTheme()
        };
        var colors = new ResourceDictionary
        {
            Source = new Uri(
                useDark
                    ? "Resources/Colors.Dark.xaml"
                    : "Resources/Colors.xaml",
                UriKind.Relative)
        };

        if (application.Resources.MergedDictionaries.Count == 0)
        {
            application.Resources.MergedDictionaries.Add(colors);
        }
        else
        {
            application.Resources.MergedDictionaries[0] = colors;
        }

        application.Resources["WorkspaceBackgroundBrush"] =
            ResolveWorkspaceBackground(
                application,
                workspaceBackground,
                customWorkspaceColor,
                useDark);
        application.Resources["AppFontSize"] = fontSize is 12 or 14 or 16 or 18
            ? fontSize
            : 14d;
    }

    private static SolidColorBrush ResolveWorkspaceBackground(
        System.Windows.Application application,
        string mode,
        string customColor,
        bool useDark)
    {
        var color = mode switch
        {
            "White" => Colors.White,
            "LightGray" => Color.FromRgb(243, 245, 247),
            "DarkGray" => Color.FromRgb(46, 50, 56),
            "Black" => Colors.Black,
            "Custom" => ParseColor(customColor, useDark),
            _ => ((SolidColorBrush)application.FindResource("WindowBackgroundBrush")).Color
        };
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string value, bool useDark)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch (FormatException)
        {
            return useDark
                ? Color.FromRgb(31, 34, 39)
                : Color.FromRgb(243, 245, 247);
        }
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
