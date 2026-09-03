using System.Xml.Linq;

namespace ImageToolkit.App.Tests;

public sealed class MainWindowLayoutTests
{
    [Fact]
    public void Settings_tabs_default_to_basic_processing_and_split_output_metadata()
    {
        var path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "ImageToolkit.App",
                "MainWindow.xaml"));
        var document = XDocument.Load(path);
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var tabs = document
            .Descendants(presentation + "TabItem")
            .Select(element => new
            {
                Header = (string?)element.Attribute("Header"),
                IsSelected = (string?)element.Attribute("IsSelected")
            })
            .ToArray();

        Assert.Contains(
            tabs,
            tab => tab.Header == "基础处理" && tab.IsSelected == "True");
        Assert.Contains(tabs, tab => tab.Header == "输出");
        Assert.Contains(tabs, tab => tab.Header == "元数据");
        Assert.DoesNotContain(tabs, tab => tab.Header == "输出与元数据");
    }

    [Fact]
    public void Ai_settings_offer_local_model_import_and_generic_cancellation()
    {
        var path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "ImageToolkit.App",
                "MainWindow.xaml"));
        var document = XDocument.Load(path);
        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var buttons = document
            .Descendants(presentation + "Button")
            .Select(element => new
            {
                Content = (string?)element.Attribute("Content"),
                Command = (string?)element.Attribute("Command")
            })
            .ToArray();

        Assert.Contains(
            buttons,
            button =>
                button.Content == "从本地文件导入模型" &&
                button.Command ==
                "{Binding AiBackgroundRemoval.ImportLocalModelCommand}");
        Assert.Contains(
            buttons,
            button => button.Content == "取消模型操作");
    }

    [Fact]
    public void Window_closing_checks_all_active_work()
    {
        var path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "ImageToolkit.App",
                "MainWindow.xaml.cs"));
        var source = File.ReadAllText(path);

        Assert.Contains("!_viewModel.HasActiveWork", source);
        Assert.DoesNotContain("!_viewModel.IsRunning", source);
    }
}
