using CommunityToolkit.Mvvm.ComponentModel;
using ImageToolkit.App.Models;
using ImageToolkit.App.Services;

namespace ImageToolkit.App.ViewModels;

public sealed partial class AppearanceSettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    public AppearanceSettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
    }

    public IReadOnlyList<ChoiceOption<string>> ThemeOptions { get; } =
    [
        new("System", "跟随系统"),
        new("Light", "浅色"),
        new("Dark", "深色")
    ];

    public IReadOnlyList<ChoiceOption<string>> BackgroundOptions { get; } =
    [
        new("System", "系统默认"),
        new("White", "白色"),
        new("LightGray", "浅灰"),
        new("DarkGray", "深灰"),
        new("Black", "黑色"),
        new("Custom", "自定义")
    ];

    public IReadOnlyList<ChoiceOption<double>> FontSizeOptions { get; } =
    [
        new(12, "小"),
        new(14, "标准"),
        new(16, "大"),
        new(18, "超大")
    ];

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private string _workspaceBackground = "System";

    [ObservableProperty]
    private string _customWorkspaceColor = "#F3F5F7";

    [ObservableProperty]
    private double _fontSize = 14;

    public void Apply(
        string theme,
        string workspaceBackground,
        string customWorkspaceColor,
        double fontSize)
    {
        Theme = theme;
        WorkspaceBackground = workspaceBackground;
        CustomWorkspaceColor = customWorkspaceColor;
        FontSize = fontSize;
        ApplyTheme();
    }

    partial void OnThemeChanged(string value) => ApplyTheme();

    partial void OnWorkspaceBackgroundChanged(string value) => ApplyTheme();

    partial void OnCustomWorkspaceColorChanged(string value) => ApplyTheme();

    partial void OnFontSizeChanged(double value) => ApplyTheme();

    private void ApplyTheme() =>
        _themeService.Apply(
            Theme,
            WorkspaceBackground,
            CustomWorkspaceColor,
            FontSize);
}
