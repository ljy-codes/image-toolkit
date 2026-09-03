using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Config;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class JsonProcessingPresetStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Saves_and_loads_named_presets()
    {
        var path = Path.Combine(_directory, "presets.json");
        var store = new JsonProcessingPresetStore(path);
        var presets = new[]
        {
            new ProcessingPreset("电商图片", ProcessingRequest.Default)
        };

        await store.SaveAsync(presets, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Single(loaded);
        Assert.Equal("电商图片", loaded[0].Name);
        Assert.Equal(ProcessingRequest.Default, loaded[0].Request);
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_empty_and_is_preserved()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "presets.json");
        await File.WriteAllTextAsync(path, "{broken");
        var store = new JsonProcessingPresetStore(path);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Empty(loaded);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_directory, "presets.*.corrupt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
