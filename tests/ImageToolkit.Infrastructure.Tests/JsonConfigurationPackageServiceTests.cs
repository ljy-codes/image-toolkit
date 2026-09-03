using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Config;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class JsonConfigurationPackageServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(
            Path.GetTempPath(),
            "ImageToolkitPackageTests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Complete_configuration_package_round_trips()
    {
        var service = new JsonConfigurationPackageService();
        var path = PackagePath;
        var expected = ConfigurationPackage.Create(
            AppConfiguration.Default with
            {
                Theme = "Dark",
                IncludeSubdirectories = true
            },
            [
                new ProcessingPreset(
                    "电商图片",
                    ProcessingRequest.Default)
            ],
            new DateTimeOffset(2026, 9, 3, 10, 30, 0, TimeSpan.FromHours(8)));

        await service.ExportAsync(path, expected, CancellationToken.None);
        var actual = await service.ImportAsync(path, CancellationToken.None);

        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal("苏影枢", actual.ProductName);
        Assert.Equal(expected.ExportedAt, actual.ExportedAt);
        Assert.Equal(expected.Configuration, actual.Configuration);
        Assert.Equal(expected.Presets, actual.Presets);
        Assert.Empty(Directory.EnumerateFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"productName":"其他工具","exportedAt":"2026-09-03T10:30:00+08:00","configuration":{},"presets":[]}""", "不是苏影枢")]
    [InlineData("""{"schemaVersion":99,"productName":"苏影枢","exportedAt":"2026-09-03T10:30:00+08:00","configuration":{},"presets":[]}""", "版本")]
    [InlineData("""{ invalid""", "无法读取")]
    public async Task Invalid_package_is_rejected_with_clear_reason(
        string content,
        string expectedMessage)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(PackagePath, content);
        var service = new JsonConfigurationPackageService();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ImportAsync(PackagePath, CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task Missing_configuration_is_rejected()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            PackagePath,
            """{"schemaVersion":1,"productName":"苏影枢","exportedAt":"2026-09-03T10:30:00+08:00","presets":[]}""");
        var service = new JsonConfigurationPackageService();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ImportAsync(PackagePath, CancellationToken.None));

        Assert.Contains("缺少应用配置", exception.Message);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"productName":"苏影枢","exportedAt":"2026-09-03T10:30:00+08:00","configuration":{"workerCount":0,"theme":"Dark","includeSubdirectories":false},"presets":[]}""", "缺少处理参数")]
    [InlineData("""{"schemaVersion":1,"productName":"苏影枢","exportedAt":"2026-09-03T10:30:00+08:00","configuration":{"processing":{"compression":{},"resize":{},"aspectRatio":{},"aiBackgroundRemoval":{},"background":{},"metadata":{},"output":{}},"workerCount":0,"theme":"Dark","includeSubdirectories":false},"presets":[null]}""", "空项目")]
    public async Task Structurally_incomplete_package_is_rejected(
        string content,
        string expectedMessage)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(PackagePath, content);
        var service = new JsonConfigurationPackageService();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ImportAsync(PackagePath, CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private string PackagePath => Path.Combine(_directory, "完整配置.syconfig");
}
