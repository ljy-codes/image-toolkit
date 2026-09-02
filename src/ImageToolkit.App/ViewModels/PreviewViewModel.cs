using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageToolkit.Application.Preview;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.App.ViewModels;

public sealed partial class PreviewViewModel : ObservableObject
{
    private readonly BuildPreviewUseCase _buildPreview;

    public PreviewViewModel(BuildPreviewUseCase buildPreview)
    {
        _buildPreview = buildPreview;
    }

    [ObservableProperty]
    private ImageSource? _originalImage;

    [ObservableProperty]
    private ImageSource? _processedImage;

    [ObservableProperty]
    private string _caption = "选择队列中的图片以查看预览";

    [ObservableProperty]
    private bool _isLoading;

    public async Task UpdateAsync(
        string sourcePath,
        ProcessingRequest request,
        CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            OriginalImage = LoadBitmap(sourcePath);
            var preview = await _buildPreview.ExecuteAsync(
                sourcePath,
                request,
                1400,
                1000,
                cancellationToken);
            ProcessedImage = LoadBitmap(preview.Bytes);
            Caption = $"{preview.Size.Width} × {preview.Size.Height}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Caption = $"预览失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Clear()
    {
        OriginalImage = null;
        ProcessedImage = null;
        Caption = "选择队列中的图片以查看预览";
    }

    private static BitmapImage LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        return LoadBitmap(stream);
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return LoadBitmap(stream);
    }

    private static BitmapImage LoadBitmap(Stream stream)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 1400;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
