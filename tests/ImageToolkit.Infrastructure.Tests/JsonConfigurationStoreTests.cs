using ImageToolkit.Infrastructure.Config;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class JsonConfigurationStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Missing_configuration_returns_defaults()
    {
        var store = CreateStore();

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AppConfiguration.Default, result);
    }

    [Fact]
    public async Task Valid_configuration_round_trips()
    {
        var store = CreateStore();
        var expected = AppConfiguration.Default with
        {
            Theme = "Dark",
            WorkerCount = 4
        };

        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Invalid_json_returns_defaults_and_preserves_corrupt_file()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(ConfigurationPath, "{ invalid");
        var store = CreateStore();

        var result = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(AppConfiguration.Default, result);
        Assert.False(File.Exists(ConfigurationPath));
        Assert.Single(Directory.EnumerateFiles(_directory, "*.corrupt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private string ConfigurationPath => Path.Combine(_directory, "settings.json");

    private JsonConfigurationStore CreateStore() =>
        new(ConfigurationPath);
}
