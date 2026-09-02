using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Options;
using ImageToolkit.Infrastructure.Files;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class OutputPathResolverTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Adds_default_suffix_in_source_directory()
    {
        Directory.CreateDirectory(_directory);
        var source = CreateSource("photo.jpg");

        var result = new OutputPathResolver().Resolve(source, OutputOptions.Default, ".jpg");

        Assert.Equal(Path.Combine(_directory, "photo-已处理.jpg"), result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void Adds_numeric_suffix_when_name_exists()
    {
        Directory.CreateDirectory(_directory);
        var source = CreateSource("photo.jpg");
        File.WriteAllBytes(Path.Combine(_directory, "photo-已处理.jpg"), [1]);

        var result = new OutputPathResolver().Resolve(source, OutputOptions.Default, ".jpg");

        Assert.Equal(Path.Combine(_directory, "photo-已处理-2.jpg"), result);
    }

    [Theory]
    [InlineData(".jpeg", ".jpg")]
    [InlineData("png", ".png")]
    [InlineData(".webp", ".webp")]
    [InlineData(".bmp", ".bmp")]
    public void Uses_selected_output_extension(string requestedExtension, string expectedExtension)
    {
        Directory.CreateDirectory(_directory);
        var source = CreateSource("photo.tif");

        var result = new OutputPathResolver()
            .Resolve(source, OutputOptions.Default, requestedExtension);

        Assert.Equal(expectedExtension, Path.GetExtension(result));
    }

    [Fact]
    public void Writes_to_specific_directory()
    {
        Directory.CreateDirectory(_directory);
        var source = CreateSource("photo.jpg");
        var outputDirectory = Path.Combine(_directory, "output");
        var options = OutputOptions.Default with
        {
            Mode = OutputMode.SpecificDirectory,
            DirectoryPath = outputDirectory
        };

        var result = new OutputPathResolver().Resolve(source, options, ".png");

        Assert.Equal(Path.Combine(outputDirectory, "photo-已处理.png"), result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void Overwrite_mode_returns_source_without_reserving()
    {
        Directory.CreateDirectory(_directory);
        var source = CreateSource("photo.jpg");
        var options = OutputOptions.Default with { Mode = OutputMode.OverwriteOriginal };

        var result = new OutputPathResolver().Resolve(source, options, ".png");

        Assert.Equal(source, result);
        Assert.Equal(1, new FileInfo(source).Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private string CreateSource(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        File.WriteAllBytes(path, [1]);
        return path;
    }
}
