using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageToolkit.App.ViewModels;

public sealed partial class BatchProgressViewModel : ObservableObject
{
    [ObservableProperty]
    private double _percentage;

    [ObservableProperty]
    private int _completed;

    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private string _statusText = "准备就绪";

    public void Reset(int total)
    {
        Total = total;
        Completed = 0;
        Percentage = 0;
        StatusText = total == 0 ? "准备就绪" : $"等待处理，共 {total} 张";
    }

    public void Advance()
    {
        Completed++;
        Percentage = Total == 0 ? 0 : (double)Completed / Total * 100;
        StatusText = $"正在处理 {Completed}/{Total}";
    }
}
