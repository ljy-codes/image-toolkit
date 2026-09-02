# Image Toolkit MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, test, publish, and package the Windows 11 x64 non-AI Image Toolkit MVP defined by the approved v4 requirements and design.

**Architecture:** Use a layered modular monolith with a WPF composition root, application use cases, dependency-free domain rules, and Magick.NET/file-system infrastructure. Every batch run receives an immutable processing snapshot; each image is decoded once, transformed through one in-memory pipeline, compression-probed from the transformed pixels, and persisted through a verified same-directory temporary file.

**Tech Stack:** C# 14, .NET 10 LTS, WPF, CommunityToolkit.Mvvm, Magick.NET-Q8-x64, Microsoft.Extensions.DependencyInjection, xUnit, win-x64 self-contained publish, Inno Setup.

---

## Scope Boundary

This plan implements the complete non-AI MVP. It creates the AI project boundary and domain interfaces but does not add ONNX Runtime, model downloads, or an AI model. AI receives a separate implementation plan after the base application passes build, test, publish, installer, and clean-machine smoke checks.

## File Map

```text
ImageToolkit.sln
Directory.Build.props
Directory.Packages.props
.gitignore
README.md
THIRD-PARTY-NOTICES.txt
src/
  ImageToolkit.App/
    App.xaml
    App.xaml.cs
    app.manifest
    MainWindow.xaml
    MainWindow.xaml.cs
    Resources/Colors.xaml
    Resources/Controls.xaml
    Resources/Strings.zh-CN.xaml
    ViewModels/MainWindowViewModel.cs
    ViewModels/FileQueueViewModel.cs
    ViewModels/PreviewViewModel.cs
    ViewModels/ProcessingSettingsViewModel.cs
    ViewModels/BatchProgressViewModel.cs
    Models/ImageQueueItemViewData.cs
    Services/DesktopFilePicker.cs
    Services/UserDialogService.cs
  ImageToolkit.Application/
    Import/ImportImagesUseCase.cs
    Preview/BuildPreviewUseCase.cs
    Processing/ImageProcessingPipeline.cs
    Processing/ProcessingRequestValidator.cs
    Batch/BatchTaskCoordinator.cs
    Batch/AsyncPauseGate.cs
  ImageToolkit.Domain/
    Models/ProcessingRequest.cs
    Models/ImageFileInfo.cs
    Models/ImageProcessingResult.cs
    Models/BatchItem.cs
    Models/BatchSummary.cs
    Options/CompressionOptions.cs
    Options/ResizeOptions.cs
    Options/AspectRatioOptions.cs
    Options/BackgroundOptions.cs
    Options/MetadataOptions.cs
    Options/OutputOptions.cs
    Results/ValidationResult.cs
    Enums/OutputImageFormat.cs
    Enums/AspectRatioMode.cs
    Enums/CropAnchor.cs
    Enums/BatchItemStatus.cs
    Enums/BatchRunState.cs
    Interfaces/IImageProcessor.cs
    Interfaces/IOutputPathResolver.cs
    Interfaces/IAtomicFileWriter.cs
    Interfaces/IConfigurationStore.cs
    Interfaces/IImageMetadataReader.cs
    Interfaces/IBackgroundRemovalEngine.cs
    Interfaces/IAiModelManager.cs
  ImageToolkit.Infrastructure/
    Imaging/MagickImageProcessor.cs
    Imaging/MagickGeometryCalculator.cs
    Imaging/MagickMetadataProcessor.cs
    Imaging/MagickCompressionEncoder.cs
    Imaging/CompressionSearchService.cs
    Files/OutputPathResolver.cs
    Files/AtomicFileWriter.cs
    Files/ImageFileDiscovery.cs
    Config/JsonConfigurationStore.cs
    Config/AppConfiguration.cs
    Logging/RollingFileLoggerProvider.cs
  ImageToolkit.Infrastructure.AI/
    AiModuleMarker.cs
tests/
  ImageToolkit.Domain.Tests/
  ImageToolkit.Application.Tests/
  ImageToolkit.Infrastructure.Tests/
  ImageToolkit.App.Tests/
installer/
  ImageToolkit.iss
scripts/
  build.ps1
  test.ps1
  publish.ps1
  package.ps1
```

## Task 1: Repository and Solution Baseline

**Files:**
- Create: `.gitignore`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `ImageToolkit.sln`
- Create: all project files under `src/` and `tests/`

- [ ] **Step 1: Install and verify the .NET 10 SDK**

Run:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --accept-package-agreements --accept-source-agreements
dotnet --info
```

Expected: `dotnet --info` reports a `10.0.x` SDK and Windows x64 RID.

- [ ] **Step 2: Create the solution and projects**

Run:

```powershell
dotnet new sln -n ImageToolkit
dotnet new wpf -n ImageToolkit.App -o src/ImageToolkit.App -f net10.0-windows
dotnet new classlib -n ImageToolkit.Application -o src/ImageToolkit.Application -f net10.0
dotnet new classlib -n ImageToolkit.Domain -o src/ImageToolkit.Domain -f net10.0
dotnet new classlib -n ImageToolkit.Infrastructure -o src/ImageToolkit.Infrastructure -f net10.0-windows
dotnet new classlib -n ImageToolkit.Infrastructure.AI -o src/ImageToolkit.Infrastructure.AI -f net10.0
dotnet new xunit -n ImageToolkit.Domain.Tests -o tests/ImageToolkit.Domain.Tests -f net10.0
dotnet new xunit -n ImageToolkit.Application.Tests -o tests/ImageToolkit.Application.Tests -f net10.0
dotnet new xunit -n ImageToolkit.Infrastructure.Tests -o tests/ImageToolkit.Infrastructure.Tests -f net10.0-windows
dotnet new xunit -n ImageToolkit.App.Tests -o tests/ImageToolkit.App.Tests -f net10.0-windows
```

Expected: nine project directories exist and each generated project restores successfully.

- [ ] **Step 3: Add project references**

Run:

```powershell
dotnet add src/ImageToolkit.Application reference src/ImageToolkit.Domain
dotnet add src/ImageToolkit.Infrastructure reference src/ImageToolkit.Domain
dotnet add src/ImageToolkit.Infrastructure.AI reference src/ImageToolkit.Domain
dotnet add src/ImageToolkit.App reference src/ImageToolkit.Application
dotnet add src/ImageToolkit.App reference src/ImageToolkit.Infrastructure
dotnet add tests/ImageToolkit.Domain.Tests reference src/ImageToolkit.Domain
dotnet add tests/ImageToolkit.Application.Tests reference src/ImageToolkit.Application
dotnet add tests/ImageToolkit.Application.Tests reference src/ImageToolkit.Domain
dotnet add tests/ImageToolkit.Infrastructure.Tests reference src/ImageToolkit.Infrastructure
dotnet add tests/ImageToolkit.Infrastructure.Tests reference src/ImageToolkit.Domain
dotnet add tests/ImageToolkit.App.Tests reference src/ImageToolkit.App
```

Expected: dependency direction matches the approved design and Domain has no project references.

- [ ] **Step 4: Add every project to the solution**

Run:

```powershell
dotnet sln ImageToolkit.sln add src/ImageToolkit.App
dotnet sln ImageToolkit.sln add src/ImageToolkit.Application
dotnet sln ImageToolkit.sln add src/ImageToolkit.Domain
dotnet sln ImageToolkit.sln add src/ImageToolkit.Infrastructure
dotnet sln ImageToolkit.sln add src/ImageToolkit.Infrastructure.AI
dotnet sln ImageToolkit.sln add tests/ImageToolkit.Domain.Tests
dotnet sln ImageToolkit.sln add tests/ImageToolkit.Application.Tests
dotnet sln ImageToolkit.sln add tests/ImageToolkit.Infrastructure.Tests
dotnet sln ImageToolkit.sln add tests/ImageToolkit.App.Tests
```

- [ ] **Step 5: Centralize build and package settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageVersion Include="Magick.NET-Q8-x64" Version="14.10.2" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>
</Project>
```

Create `.gitignore`:

```gitignore
bin/
obj/
.vs/
.idea/
*.user
*.suo
TestResults/
artifacts/
.worktrees/
*.tmp
*.bak
```

- [ ] **Step 6: Add package references**

Run:

```powershell
dotnet add src/ImageToolkit.App package CommunityToolkit.Mvvm
dotnet add src/ImageToolkit.App package Microsoft.Extensions.DependencyInjection
dotnet add src/ImageToolkit.App package Microsoft.Extensions.Logging.Abstractions
dotnet add src/ImageToolkit.Infrastructure package Magick.NET-Q8-x64
dotnet restore
```

Expected: restore completes with no warnings or errors.

- [ ] **Step 7: Build the empty solution**

Run:

```powershell
dotnet build ImageToolkit.sln
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 8: Commit**

```powershell
git add .gitignore Directory.Build.props Directory.Packages.props ImageToolkit.sln src tests
git commit -m "build: create image toolkit solution"
```

## Task 2: Domain Processing Contract

**Files:**
- Create: `tests/ImageToolkit.Domain.Tests/ProcessingRequestTests.cs`
- Create: domain enums, options, models, results, and interfaces listed in the file map
- Delete: generated `Class1.cs`

- [ ] **Step 1: Write failing defaults and immutability tests**

Create `ProcessingRequestTests.cs`:

```csharp
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Domain.Tests;

public sealed class ProcessingRequestTests
{
    [Fact]
    public void Defaults_match_product_safety_boundaries()
    {
        var request = ProcessingRequest.Default;

        Assert.True(request.Compression.Enabled);
        Assert.Equal(1_048_576, request.Compression.TargetBytes);
        Assert.Equal(45, request.Compression.MinimumJpegQuality);
        Assert.Equal(45, request.Compression.MinimumWebpQuality);
        Assert.Equal(0.25, request.Compression.MinimumScaleRatio);
        Assert.Equal(320, request.Compression.MinimumShortEdge);
        Assert.False(request.Compression.AllowPngQuantization);
        Assert.True(request.Metadata.PreserveIccProfile);
        Assert.False(request.Metadata.PreserveGps);
        Assert.Equal(OutputImageFormat.Original, request.Output.Format);
    }

    [Fact]
    public void With_expression_creates_a_new_request()
    {
        var original = ProcessingRequest.Default;
        var changed = original with
        {
            Compression = original.Compression with { TargetBytes = 500_000 }
        };

        Assert.Equal(1_048_576, original.Compression.TargetBytes);
        Assert.Equal(500_000, changed.Compression.TargetBytes);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
dotnet test tests/ImageToolkit.Domain.Tests --filter ProcessingRequestTests
```

Expected: compilation fails because `ProcessingRequest` and related types do not exist.

- [ ] **Step 3: Implement the domain records**

Create `CompressionOptions.cs`:

```csharp
namespace ImageToolkit.Domain.Options;

public sealed record CompressionOptions(
    bool Enabled,
    long TargetBytes,
    int MinimumJpegQuality,
    int MinimumWebpQuality,
    double MinimumScaleRatio,
    int MinimumShortEdge,
    bool AllowAutomaticResize,
    bool AllowPngQuantization)
{
    public static CompressionOptions Default { get; } =
        new(true, 1_048_576, 45, 45, 0.25, 320, true, false);
}
```

Create `ProcessingRequest.cs`:

```csharp
using ImageToolkit.Domain.Options;

namespace ImageToolkit.Domain.Models;

public sealed record ProcessingRequest(
    CompressionOptions Compression,
    ResizeOptions Resize,
    AspectRatioOptions AspectRatio,
    BackgroundOptions Background,
    MetadataOptions Metadata,
    OutputOptions Output)
{
    public static ProcessingRequest Default { get; } = new(
        CompressionOptions.Default,
        ResizeOptions.Default,
        AspectRatioOptions.Default,
        BackgroundOptions.Default,
        MetadataOptions.Default,
        OutputOptions.Default);
}
```

Implement the remaining options as immutable records with the exact defaults from the approved design. Implement domain enums and result records without framework dependencies.

- [ ] **Step 4: Run tests and verify GREEN**

Run:

```powershell
dotnet test tests/ImageToolkit.Domain.Tests --filter ProcessingRequestTests
```

Expected: two tests pass.

- [ ] **Step 5: Add interface compile tests**

Create a test that assigns a fake implementation to each domain interface. The test must compile without referencing WPF, Magick.NET, or file-system implementation namespaces.

- [ ] **Step 6: Run all Domain tests**

Run:

```powershell
dotnet test tests/ImageToolkit.Domain.Tests
```

Expected: all Domain tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/ImageToolkit.Domain tests/ImageToolkit.Domain.Tests
git commit -m "feat: define image processing domain contract"
```

## Task 3: Processing Request Validation

**Files:**
- Create: `tests/ImageToolkit.Application.Tests/ProcessingRequestValidatorTests.cs`
- Create: `src/ImageToolkit.Application/Processing/ProcessingRequestValidator.cs`
- Create: `src/ImageToolkit.Domain/Results/ValidationError.cs`

- [ ] **Step 1: Write failing validation tests**

```csharp
using ImageToolkit.Application.Processing;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Tests;

public sealed class ProcessingRequestValidatorTests
{
    private readonly ProcessingRequestValidator _validator = new();

    [Fact]
    public void Rejects_quality_outside_supported_range()
    {
        var request = ProcessingRequest.Default with
        {
            Compression = ProcessingRequest.Default.Compression with
            {
                MinimumJpegQuality = 19
            }
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "compression.jpeg-quality-range");
    }

    [Fact]
    public void Rejects_target_dimensions_without_positive_value()
    {
        var request = ProcessingRequest.Default with
        {
            Resize = ProcessingRequest.Default.Resize with
            {
                Enabled = true,
                Width = 0,
                Height = 900
            }
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "resize.width-positive");
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet test tests/ImageToolkit.Application.Tests --filter ProcessingRequestValidatorTests
```

Expected: compilation fails because `ProcessingRequestValidator` does not exist.

- [ ] **Step 3: Implement validation**

```csharp
using ImageToolkit.Domain.Models;
using ImageToolkit.Domain.Results;

namespace ImageToolkit.Application.Processing;

public sealed class ProcessingRequestValidator
{
    public ValidationResult Validate(ProcessingRequest request)
    {
        var errors = new List<ValidationError>();
        ValidateQuality(request, errors);
        ValidateResize(request, errors);
        ValidateCompressionLimits(request, errors);
        ValidateOutput(request, errors);
        return new ValidationResult(errors);
    }

    private static void ValidateQuality(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (request.Compression.MinimumJpegQuality is < 20 or > 95)
        {
            errors.Add(new("compression.jpeg-quality-range", "JPEG 最低质量必须在 20 到 95 之间。"));
        }

        if (request.Compression.MinimumWebpQuality is < 20 or > 95)
        {
            errors.Add(new("compression.webp-quality-range", "WebP 最低质量必须在 20 到 95 之间。"));
        }
    }

    private static void ValidateResize(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (!request.Resize.Enabled)
        {
            return;
        }

        if (request.Resize.Width is <= 0)
        {
            errors.Add(new("resize.width-positive", "宽度必须大于 0。"));
        }

        if (request.Resize.Height is <= 0)
        {
            errors.Add(new("resize.height-positive", "高度必须大于 0。"));
        }
    }

    private static void ValidateCompressionLimits(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (request.Compression.TargetBytes <= 0)
        {
            errors.Add(new("compression.target-positive", "目标文件大小必须大于 0。"));
        }

        if (request.Compression.MinimumScaleRatio is <= 0 or > 1)
        {
            errors.Add(new("compression.scale-range", "自动缩放比例下限必须大于 0 且不超过 1。"));
        }

        if (request.Compression.MinimumShortEdge <= 0)
        {
            errors.Add(new("compression.short-edge-positive", "最小短边必须大于 0。"));
        }
    }

    private static void ValidateOutput(
        ProcessingRequest request,
        ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(request.Output.FileNameSuffix))
        {
            errors.Add(new("output.suffix-required", "输出文件名后缀不能为空。"));
        }
    }
}
```

- [ ] **Step 4: Add boundary tests**

Add tests for Quality 20 and 95, minimum scale ratio 0.25, short edge 320, empty suffix, custom aspect ratio values, and writable output requirements.

- [ ] **Step 5: Run tests and verify GREEN**

Run:

```powershell
dotnet test tests/ImageToolkit.Application.Tests --filter ProcessingRequestValidatorTests
```

Expected: all validator tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ImageToolkit.Application src/ImageToolkit.Domain/Results tests/ImageToolkit.Application.Tests
git commit -m "feat: validate processing requests"
```

## Task 4: Geometry Rules

**Files:**
- Create: `tests/ImageToolkit.Infrastructure.Tests/MagickGeometryCalculatorTests.cs`
- Create: `src/ImageToolkit.Infrastructure/Imaging/MagickGeometryCalculator.cs`
- Create: `src/ImageToolkit.Domain/Models/PixelSize.cs`
- Create: `src/ImageToolkit.Domain/Models/PixelRectangle.cs`

- [ ] **Step 1: Write failing crop and canvas tests**

```csharp
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class MagickGeometryCalculatorTests
{
    [Fact]
    public void Center_crop_converts_4_by_3_to_square()
    {
        var crop = MagickGeometryCalculator.CalculateCrop(
            new PixelSize(4000, 3000),
            1,
            1,
            CropAnchor.Center);

        Assert.Equal(new PixelRectangle(500, 0, 3000, 3000), crop);
    }

    [Fact]
    public void Canvas_expands_square_to_4_by_3()
    {
        var canvas = MagickGeometryCalculator.CalculateCanvas(
            new PixelSize(1000, 1000),
            4,
            3);

        Assert.Equal(new PixelSize(1334, 1000), canvas);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter MagickGeometryCalculatorTests
```

Expected: compilation fails because geometry types do not exist.

- [ ] **Step 3: Implement deterministic geometry**

```csharp
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Imaging;

public static class MagickGeometryCalculator
{
    public static PixelRectangle CalculateCrop(
        PixelSize source,
        int ratioWidth,
        int ratioHeight,
        CropAnchor anchor)
    {
        var targetRatio = (double)ratioWidth / ratioHeight;
        var sourceRatio = (double)source.Width / source.Height;

        var width = source.Width;
        var height = source.Height;

        if (sourceRatio > targetRatio)
        {
            width = (int)Math.Round(source.Height * targetRatio);
        }
        else
        {
            height = (int)Math.Round(source.Width / targetRatio);
        }

        return PositionCrop(source, new PixelSize(width, height), anchor);
    }

    public static PixelSize CalculateCanvas(
        PixelSize source,
        int ratioWidth,
        int ratioHeight)
    {
        var targetRatio = (double)ratioWidth / ratioHeight;
        var sourceRatio = (double)source.Width / source.Height;

        return sourceRatio > targetRatio
            ? new PixelSize(source.Width, (int)Math.Ceiling(source.Width / targetRatio))
            : new PixelSize((int)Math.Ceiling(source.Height * targetRatio), source.Height);
    }

    private static PixelRectangle PositionCrop(
        PixelSize source,
        PixelSize crop,
        CropAnchor anchor)
    {
        var centeredX = (source.Width - crop.Width) / 2;
        var centeredY = (source.Height - crop.Height) / 2;

        return anchor switch
        {
            CropAnchor.Top => new(centeredX, 0, crop.Width, crop.Height),
            CropAnchor.Bottom => new(centeredX, source.Height - crop.Height, crop.Width, crop.Height),
            CropAnchor.Left => new(0, centeredY, crop.Width, crop.Height),
            CropAnchor.Right => new(source.Width - crop.Width, centeredY, crop.Width, crop.Height),
            _ => new(centeredX, centeredY, crop.Width, crop.Height)
        };
    }
}
```

- [ ] **Step 4: Add anchor and resize tests**

Cover all crop anchors, portrait ratios, exact-ratio no-op, width-only resize, height-only resize, locked ratio, unlocked stretch, and no-upscale behavior.

- [ ] **Step 5: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter MagickGeometryCalculatorTests
```

- [ ] **Step 6: Commit**

```powershell
git add src/ImageToolkit.Domain/Models src/ImageToolkit.Infrastructure/Imaging tests/ImageToolkit.Infrastructure.Tests
git commit -m "feat: add crop canvas and resize geometry"
```

## Task 5: Output Naming and Conflict Resolution

**Files:**
- Create: `tests/ImageToolkit.Infrastructure.Tests/OutputPathResolverTests.cs`
- Create: `src/ImageToolkit.Infrastructure/Files/OutputPathResolver.cs`

- [ ] **Step 1: Write failing naming tests**

```csharp
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
        var source = Path.Combine(_directory, "photo.jpg");
        File.WriteAllBytes(source, [1]);

        var resolver = new OutputPathResolver();
        var result = resolver.Resolve(source, OutputOptions.Default, ".jpg");

        Assert.Equal(Path.Combine(_directory, "photo-已处理.jpg"), result);
    }

    [Fact]
    public void Adds_numeric_suffix_when_name_exists()
    {
        Directory.CreateDirectory(_directory);
        var source = Path.Combine(_directory, "photo.jpg");
        File.WriteAllBytes(source, [1]);
        File.WriteAllBytes(Path.Combine(_directory, "photo-已处理.jpg"), [1]);

        var resolver = new OutputPathResolver();
        var result = resolver.Resolve(source, OutputOptions.Default, ".jpg");

        Assert.Equal(Path.Combine(_directory, "photo-已处理-2.jpg"), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter OutputPathResolverTests
```

- [ ] **Step 3: Implement atomic path reservation**

Implement `Resolve` with `Path.GetFileNameWithoutExtension`, selected output extension, configured suffix, specified-directory handling, overwrite-mode handling, and numeric conflict iteration. Reserve a selected non-overwrite path with `FileMode.CreateNew` immediately before final writing to prevent parallel workers choosing the same name.

- [ ] **Step 4: Add format-extension tests**

Cover JPG, PNG, WebP, BMP, original format, specified output directory, and overwrite mode.

- [ ] **Step 5: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter OutputPathResolverTests
```

- [ ] **Step 6: Commit**

```powershell
git add src/ImageToolkit.Infrastructure/Files tests/ImageToolkit.Infrastructure.Tests
git commit -m "feat: resolve safe output paths"
```

## Task 6: Verified Atomic File Writer

**Files:**
- Create: `tests/ImageToolkit.Infrastructure.Tests/AtomicFileWriterTests.cs`
- Create: `src/ImageToolkit.Infrastructure/Files/AtomicFileWriter.cs`

- [ ] **Step 1: Write failing preservation tests**

```csharp
using ImageToolkit.Infrastructure.Files;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class AtomicFileWriterTests
{
    [Fact]
    public async Task Failed_validation_preserves_original_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ImageToolkitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "photo.jpg");
        await File.WriteAllTextAsync(target, "original");

        var writer = new AtomicFileWriter();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            writer.ReplaceAsync(
                target,
                async stream => await stream.WriteAsync("new"u8.ToArray()),
                _ => Task.FromResult(false),
                CancellationToken.None));

        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        Directory.Delete(directory, true);
    }
}
```

- [ ] **Step 2: Run test and verify RED**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter AtomicFileWriterTests
```

- [ ] **Step 3: Implement verified same-directory replacement**

```csharp
namespace ImageToolkit.Infrastructure.Files;

public sealed class AtomicFileWriter
{
    public async Task ReplaceAsync(
        string targetPath,
        Func<Stream, Task> write,
        Func<string, Task<bool>> validate,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException("目标路径缺少目录。", nameof(targetPath));
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        var backupPath = temporaryPath + ".bak";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                131072,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await write(stream);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }

            if (!await validate(temporaryPath))
            {
                throw new InvalidDataException("输出文件校验失败。");
            }

            File.Replace(temporaryPath, targetPath, backupPath, true);
            File.Delete(backupPath);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
            DeleteIfExists(backupPath);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 4: Add success, cancellation, and unsupported replacement tests**

Use real files. Assert successful replacement changes the target; cancellation and validation failure preserve the original; temporary and backup files are cleaned.

- [ ] **Step 5: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter AtomicFileWriterTests
```

- [ ] **Step 6: Commit**

```powershell
git add src/ImageToolkit.Infrastructure/Files tests/ImageToolkit.Infrastructure.Tests
git commit -m "feat: write outputs with verified atomic replacement"
```

## Task 7: Compression Search Rules

**Files:**
- Create: `tests/ImageToolkit.Infrastructure.Tests/CompressionSearchServiceTests.cs`
- Create: `src/ImageToolkit.Infrastructure/Imaging/CompressionSearchService.cs`
- Create: `src/ImageToolkit.Domain/Models/CompressionAttempt.cs`
- Create: `src/ImageToolkit.Domain/Models/CompressionDecision.cs`

- [ ] **Step 1: Write failing binary-search tests**

```csharp
using ImageToolkit.Infrastructure.Imaging;

namespace ImageToolkit.Infrastructure.Tests;

public sealed class CompressionSearchServiceTests
{
    [Fact]
    public async Task Selects_highest_quality_not_exceeding_target()
    {
        var service = new CompressionSearchService();

        var result = await service.FindQualityAsync(
            45,
            95,
            800,
            quality => Task.FromResult((long)quality * 10),
            CancellationToken.None);

        Assert.True(result.ReachedTarget);
        Assert.Equal(80, result.Quality);
        Assert.Equal(800, result.SizeBytes);
    }

    [Fact]
    public async Task Reports_unmet_when_minimum_quality_is_too_large()
    {
        var service = new CompressionSearchService();

        var result = await service.FindQualityAsync(
            45,
            95,
            400,
            quality => Task.FromResult((long)quality * 10),
            CancellationToken.None);

        Assert.False(result.ReachedTarget);
        Assert.Equal(45, result.Quality);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter CompressionSearchServiceTests
```

- [ ] **Step 3: Implement quality search**

```csharp
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Infrastructure.Imaging;

public sealed class CompressionSearchService
{
    public async Task<CompressionDecision> FindQualityAsync(
        int minimumQuality,
        int maximumQuality,
        long targetBytes,
        Func<int, Task<long>> probe,
        CancellationToken cancellationToken)
    {
        CompressionDecision? best = null;
        var low = minimumQuality;
        var high = maximumQuality;

        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quality = low + ((high - low) / 2);
            var size = await probe(quality);

            if (size <= targetBytes)
            {
                best = new(true, quality, size);
                low = quality + 1;
            }
            else
            {
                high = quality - 1;
            }
        }

        if (best is not null)
        {
            return best;
        }

        var minimumSize = await probe(minimumQuality);
        return new(false, minimumQuality, minimumSize);
    }
}
```

- [ ] **Step 4: Add automatic resize boundary tests**

Test that generated resize candidates preserve aspect ratio, never go below 25% of the compression-stage dimensions, never go below a 320 px short edge, and never upscale an image whose short edge is already below 320 px.

- [ ] **Step 5: Add manual-size hard-constraint test**

Assert that `AllowAutomaticResize` is ignored when `ResizeOptions.Enabled` is true and both final dimensions are specified.

- [ ] **Step 6: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter CompressionSearchServiceTests
```

- [ ] **Step 7: Commit**

```powershell
git add src/ImageToolkit.Domain/Models src/ImageToolkit.Infrastructure/Imaging tests/ImageToolkit.Infrastructure.Tests
git commit -m "feat: enforce compression quality and size limits"
```

## Task 8: Batch Coordinator

**Files:**
- Create: `tests/ImageToolkit.Application.Tests/BatchTaskCoordinatorTests.cs`
- Create: `src/ImageToolkit.Application/Batch/AsyncPauseGate.cs`
- Create: `src/ImageToolkit.Application/Batch/BatchTaskCoordinator.cs`

- [ ] **Step 1: Write failing isolation test**

```csharp
using ImageToolkit.Application.Batch;
using ImageToolkit.Domain.Enums;
using ImageToolkit.Domain.Models;

namespace ImageToolkit.Application.Tests;

public sealed class BatchTaskCoordinatorTests
{
    [Fact]
    public async Task One_failure_does_not_stop_remaining_items()
    {
        var processed = new List<string>();
        var coordinator = new BatchTaskCoordinator(async (item, _, _) =>
        {
            processed.Add(item.SourcePath);
            await Task.Yield();
            return item.SourcePath.EndsWith("bad.jpg", StringComparison.OrdinalIgnoreCase)
                ? ImageProcessingResult.Failed(item.SourcePath, "read.failed", "读取失败")
                : ImageProcessingResult.Completed(item.SourcePath, item.SourcePath + ".out", 100);
        });

        var summary = await coordinator.RunAsync(
            [
                BatchItem.Waiting("good-1.jpg"),
                BatchItem.Waiting("bad.jpg"),
                BatchItem.Waiting("good-2.jpg")
            ],
            ProcessingRequest.Default,
            1,
            null,
            CancellationToken.None);

        Assert.Equal(3, processed.Count);
        Assert.Equal(2, summary.Completed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(BatchRunState.CompletedWithIssues, summary.State);
    }
}
```

- [ ] **Step 2: Run test and verify RED**

```powershell
dotnet test tests/ImageToolkit.Application.Tests --filter BatchTaskCoordinatorTests
```

- [ ] **Step 3: Implement bounded concurrency**

Implement the coordinator with `Channel<BatchItem>`, a fixed worker count of 1, 2, 4, or a conservative automatic value, per-item exception capture, progress reporting, and an immutable request snapshot.

- [ ] **Step 4: Implement pause and cancellation**

`AsyncPauseGate.WaitAsync` blocks workers before taking a new item. Running image work receives the same cancellation token and is allowed to finish only through safe file boundaries.

- [ ] **Step 5: Add tests**

Cover:

- Pause prevents the next item from starting.
- Resume continues queued items.
- Cancellation marks queued items cancelled.
- An unmet compression result increments `Unmet` rather than `Failed`.
- Running configuration is not mutated when UI draft changes.
- A second run is rejected while one run is active.

- [ ] **Step 6: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Application.Tests --filter BatchTaskCoordinatorTests
```

- [ ] **Step 7: Commit**

```powershell
git add src/ImageToolkit.Application/Batch tests/ImageToolkit.Application.Tests
git commit -m "feat: coordinate cancellable batch processing"
```

## Task 9: Magick.NET Processing Pipeline

**Files:**
- Create: `tests/ImageToolkit.Infrastructure.Tests/MagickImageProcessorTests.cs`
- Create: `tests/ImageToolkit.Infrastructure.Tests/TestImages.cs`
- Create: `src/ImageToolkit.Infrastructure/Imaging/MagickImageProcessor.cs`
- Create: `src/ImageToolkit.Infrastructure/Imaging/MagickMetadataProcessor.cs`
- Create: `src/ImageToolkit.Infrastructure/Imaging/MagickCompressionEncoder.cs`
- Create: `src/ImageToolkit.Application/Processing/ImageProcessingPipeline.cs`

- [ ] **Step 1: Generate deterministic test images**

`TestImages` must generate small images in temporary directories with Magick.NET:

- JPEG with EXIF Orientation.
- JPEG with GPS fields.
- PNG with Alpha.
- JPEG with sRGB profile.
- JPEG with no ICC profile.
- High-entropy JPEG large enough to exercise compression.

Do not modify or commit the user's `测试图/` directory.

- [ ] **Step 2: Write failing integration tests**

Test:

- Orientation is applied and reset to 1.
- GPS is removed by default.
- ordinary EXIF is retained.
- ICC is retained when supported.
- PNG Alpha survives PNG output.
- transparent output requested as JPG changes final format to PNG.
- crop runs before resize.
- manually specified dimensions remain unchanged when compression is unmet.

- [ ] **Step 3: Run tests and verify RED**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter MagickImageProcessorTests
```

Expected: tests fail because the processor is missing.

- [ ] **Step 4: Implement metadata processing**

Use Magick.NET profile APIs:

```csharp
public void ApplyInputOrientation(MagickImage image)
{
    image.AutoOrient();
    image.Orientation = OrientationType.TopLeft;
}

public void ApplyOutputMetadata(MagickImage image, MetadataOptions options)
{
    if (!options.PreserveGps)
    {
        image.RemoveProfile("8bim");
        RemoveGpsExifProperties(image);
    }

    if (!options.PreserveIccProfile)
    {
        ConvertToSrgbBeforeRemovingProfile(image);
    }
}
```

Implement GPS removal by enumerating EXIF GPS tags rather than deleting all EXIF. ICC removal must follow a successful sRGB conversion.

- [ ] **Step 5: Implement pixel operations**

Apply operations in this order:

```csharp
metadata.ApplyInputOrientation(image);
ApplyAspectRatio(image, request.AspectRatio, request.Background);
ApplyResize(image, request.Resize);
ResolveTransparencyAndFormat(image, request);
metadata.ApplyOutputMetadata(image, request.Metadata);
```

- [ ] **Step 6: Implement format encoders**

- JPEG: quality search with Alpha flattened against the selected background.
- WebP: lossy quality search and Alpha retention.
- PNG: lossless optimization; quantization only when explicitly enabled.
- BMP: direct output without target-size guarantee; return unmet when target compression cannot be expressed safely.

Every probe encodes from a clone of the final transformed image or resets all encoder-only settings before the next probe. The original source is not decoded again.

- [ ] **Step 7: Implement final pipeline and safe output**

`ImageProcessingPipeline` validates the request, resolves the final format and output path, calls `IImageProcessor`, then persists through `IAtomicFileWriter`. It converts expected limits to an unmet result and operational exceptions to a failed result with stable error codes.

- [ ] **Step 8: Run integration tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter MagickImageProcessorTests
```

- [ ] **Step 9: Run all non-UI tests**

```powershell
dotnet test tests/ImageToolkit.Domain.Tests
dotnet test tests/ImageToolkit.Application.Tests
dotnet test tests/ImageToolkit.Infrastructure.Tests
```

Expected: all tests pass with no warnings.

- [ ] **Step 10: Commit**

```powershell
git add src/ImageToolkit.Application/Processing src/ImageToolkit.Infrastructure/Imaging tests/ImageToolkit.Infrastructure.Tests
git commit -m "feat: process images through magick pipeline"
```

## Task 10: Configuration and Logging

**Files:**
- Create: `tests/ImageToolkit.Infrastructure.Tests/JsonConfigurationStoreTests.cs`
- Create: `tests/ImageToolkit.Infrastructure.Tests/RollingFileLoggerProviderTests.cs`
- Create: `src/ImageToolkit.Infrastructure/Config/AppConfiguration.cs`
- Create: `src/ImageToolkit.Infrastructure/Config/JsonConfigurationStore.cs`
- Create: `src/ImageToolkit.Infrastructure/Logging/RollingFileLoggerProvider.cs`

- [ ] **Step 1: Write failing configuration recovery tests**

Test:

- Missing config returns defaults.
- Valid config round-trips.
- Invalid JSON returns defaults and renames the bad file with a timestamped `.corrupt` suffix.
- Writes use a temporary file and replacement.

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter JsonConfigurationStoreTests
```

- [ ] **Step 3: Implement JSON configuration**

Use `System.Text.Json` with explicit enum conversion, UTF-8 without BOM, camel-case property names, and an application-data path supplied through the constructor for testability.

- [ ] **Step 4: Write failing log privacy tests**

Assert that an exception log contains timestamp, level, event ID, message, and exception type but does not serialize byte arrays or configured sensitive Metadata values.

- [ ] **Step 5: Implement rolling logs**

Write one UTF-8 log file per day under `%LOCALAPPDATA%\ImageToolkit\logs`. Keep the newest 14 files. Serialize writes through a single asynchronous queue and flush on application shutdown.

- [ ] **Step 6: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter "JsonConfigurationStoreTests|RollingFileLoggerProviderTests"
```

- [ ] **Step 7: Commit**

```powershell
git add src/ImageToolkit.Infrastructure/Config src/ImageToolkit.Infrastructure/Logging tests/ImageToolkit.Infrastructure.Tests
git commit -m "feat: persist settings and write privacy-safe logs"
```

## Task 11: WPF Shell and MVVM State

**Files:**
- Create: App, MainWindow, resource, ViewModel, model, picker, and dialog files listed under `ImageToolkit.App`
- Create: `tests/ImageToolkit.App.Tests/MainWindowViewModelTests.cs`
- Create: `tests/ImageToolkit.App.Tests/ProcessingSettingsViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel command tests**

Test:

- Start is disabled with an empty queue.
- Start is enabled with valid settings and at least one supported file.
- Start is disabled while a batch runs.
- Pause and cancel command states follow batch state.
- Changing settings during a run does not mutate the active request snapshot.
- JPG plus transparent background changes the draft output to PNG and raises a non-blocking notice.

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests/ImageToolkit.App.Tests
```

- [ ] **Step 3: Implement ViewModels with CommunityToolkit.Mvvm**

Use `[ObservableProperty]` for UI state and `[RelayCommand]`/`[AsyncRelayCommand]` for commands. Inject use cases and dialog abstractions through constructors. Do not use service locators or static application state.

- [ ] **Step 4: Build the single-window layout**

`MainWindow.xaml` must contain:

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
    <RowDefinition Height="Auto" />
  </Grid.RowDefinitions>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="176" />
    <ColumnDefinition Width="*" />
    <ColumnDefinition Width="320" />
  </Grid.ColumnDefinitions>
</Grid>
```

Place the command bar at the top, navigation at left, preview and file list in a vertically split center workspace, settings at right, and progress/actions at bottom. Use native WPF controls with resource-based styles, a maximum 8 px corner radius, stable icon-button dimensions, and tooltips for unfamiliar icons.

- [ ] **Step 5: Add Chinese resources and themes**

Create:

- neutral light and dark semantic colors.
- control styles for button, text box, combo box, list view, progress bar, toggle, and dialog.
- `Strings.zh-CN.xaml` keys for every user-visible message.
- font sizes 12, 14, 16, and 18.

Avoid a one-hue palette. Use neutral grays, blue command emphasis, green success, amber unmet, and red failure.

- [ ] **Step 6: Configure dependency injection**

`App.xaml.cs` creates a `ServiceCollection`, registers domain/application/infrastructure services, creates `MainWindow`, and asynchronously disposes logging and running tasks on exit.

- [ ] **Step 7: Run tests and verify GREEN**

```powershell
dotnet test tests/ImageToolkit.App.Tests
dotnet build src/ImageToolkit.App
```

- [ ] **Step 8: Commit**

```powershell
git add src/ImageToolkit.App tests/ImageToolkit.App.Tests
git commit -m "feat: add single-window wpf workspace"
```

## Task 12: File Import, Preview, and End-to-End UI Flow

**Files:**
- Create: `tests/ImageToolkit.Application.Tests/ImportImagesUseCaseTests.cs`
- Create: `tests/ImageToolkit.Application.Tests/BuildPreviewUseCaseTests.cs`
- Create: `src/ImageToolkit.Application/Import/ImportImagesUseCase.cs`
- Create: `src/ImageToolkit.Application/Preview/BuildPreviewUseCase.cs`
- Create: `src/ImageToolkit.Infrastructure/Files/ImageFileDiscovery.cs`
- Modify: App ViewModels and MainWindow drag/drop bindings

- [ ] **Step 1: Write failing import tests**

Cover:

- Single file.
- Multiple files.
- Folder without subfolders.
- Folder with subfolders.
- Unsupported extensions ignored with a reported reason.
- Duplicate paths collapsed using case-insensitive Windows path comparison.

- [ ] **Step 2: Implement file discovery**

Support `.jpg`, `.jpeg`, `.png`, `.webp`, `.bmp`, and `.tif/.tiff`. Enumerate lazily, catch per-directory access errors, and return successful files plus rejected paths.

- [ ] **Step 3: Write failing preview cancellation tests**

Start preview A, then request preview B. Assert A receives cancellation and only B updates the current preview result.

- [ ] **Step 4: Implement preview**

Debounce settings changes by 200 ms, downsample the source during decode, cap preview dimensions, use the same geometry and metadata rules as production, and never write output files.

- [ ] **Step 5: Wire file dialogs and drag/drop**

Support:

- OpenFileDialog multiselect.
- FolderBrowserDialog.
- File and folder drag/drop.
- Ctrl/Shift selection in the native ListView.
- Remove selected and clear queue commands.

- [ ] **Step 6: Run tests**

```powershell
dotnet test tests/ImageToolkit.Application.Tests --filter "ImportImagesUseCaseTests|BuildPreviewUseCaseTests"
dotnet test tests/ImageToolkit.App.Tests
```

- [ ] **Step 7: Commit**

```powershell
git add src/ImageToolkit.Application/Import src/ImageToolkit.Application/Preview src/ImageToolkit.Infrastructure/Files src/ImageToolkit.App tests
git commit -m "feat: import images and render cancellable previews"
```

## Task 13: DPI, Manifest, Integration, and User Test Images

**Files:**
- Create: `src/ImageToolkit.App/app.manifest`
- Create: `tests/ImageToolkit.Infrastructure.Tests/UserImageSmokeTests.cs`
- Modify: `src/ImageToolkit.App/ImageToolkit.App.csproj`
- Modify: WPF resources where visual checks expose layout issues

- [ ] **Step 1: Enable PerMonitorV2**

Create `app.manifest` containing:

```xml
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
      PerMonitorV2
    </dpiAwareness>
    <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
      true
    </longPathAware>
  </windowsSettings>
</application>
```

Reference it from the App project with `<ApplicationManifest>app.manifest</ApplicationManifest>`.

- [ ] **Step 2: Add optional local smoke tests**

`UserImageSmokeTests` discovers files under `测试图/` only when the directory exists. It copies each image to a temporary directory, processes the copy, and asserts the source hash is unchanged. Mark the tests with a `UserAssets` trait so CI can exclude them when the directory is absent.

- [ ] **Step 3: Run the user-image smoke suite**

```powershell
dotnet test tests/ImageToolkit.Infrastructure.Tests --filter UserAssets
```

Expected: seven current user images can be read; processing never modifies originals.

- [ ] **Step 4: Launch and visually verify**

Run:

```powershell
dotnet run --project src/ImageToolkit.App
```

Verify:

- Empty state.
- Seven-image import.
- Original/processed preview.
- Ratio, resize, compression, output, and background controls.
- Batch start, pause, resume, cancel, retry.
- Completed, unmet, failed, and cancelled row states.
- Light, dark, and system themes.

- [ ] **Step 5: Capture DPI evidence**

At 100%, 125%, 150%, and 200%, capture the main window and dialogs. Check text clipping, control overlap, preview scaling, list columns, combo boxes, and monitor-to-monitor movement when available.

- [ ] **Step 6: Commit**

```powershell
git add src/ImageToolkit.App tests/ImageToolkit.Infrastructure.Tests
git commit -m "test: verify dpi and user image workflows"
```

## Task 14: Build, Publish, Installer, and Documentation

**Files:**
- Create: `scripts/build.ps1`
- Create: `scripts/test.ps1`
- Create: `scripts/publish.ps1`
- Create: `scripts/package.ps1`
- Create: `installer/ImageToolkit.iss`
- Create: `README.md`
- Create: `THIRD-PARTY-NOTICES.txt`

- [ ] **Step 1: Add build and test scripts**

`scripts/build.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
dotnet restore "$PSScriptRoot\..\ImageToolkit.sln"
dotnet build "$PSScriptRoot\..\ImageToolkit.sln" -c Release --no-restore
```

`scripts/test.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test "$PSScriptRoot\..\ImageToolkit.sln" -c Release --no-build
```

- [ ] **Step 2: Add publish script**

`scripts/publish.ps1`:

```powershell
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$output = Join-Path $root 'artifacts\publish\win-x64'
dotnet publish "$root\src\ImageToolkit.App\ImageToolkit.App.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o $output
```

- [ ] **Step 3: Install and verify Inno Setup**

Run:

```powershell
winget install --id JRSoftware.InnoSetup --exact --accept-package-agreements --accept-source-agreements
Get-Command ISCC.exe
```

Expected: `ISCC.exe` resolves to the installed Inno Setup compiler.

- [ ] **Step 4: Add installer**

`ImageToolkit.iss` must:

- read the application version from a build parameter.
- install the entire self-contained publish directory.
- create Start Menu and optional desktop shortcuts.
- set `ArchitecturesAllowed=x64compatible`.
- set `ArchitecturesInstallIn64BitMode=x64compatible`.
- provide uninstall metadata.
- avoid bundling user configuration, logs, test images, or AI models.
- accept optional signing command parameters without requiring a certificate for internal builds.

- [ ] **Step 5: Add package script**

`scripts/package.ps1` invokes `publish.ps1`, locates `ISCC.exe`, writes output under `artifacts/installer`, and fails if the setup executable is absent after compilation.

- [ ] **Step 6: Write README and notices**

README includes:

- product purpose and feature list.
- Windows 11 x64 support boundary.
- installation and uninstall steps for ordinary users.
- privacy statement.
- developer restore, build, test, run, publish, and installer commands.
- AI model is not part of the MVP.
- HEIC/AVIF are not supported in the MVP.
- ARM64 is experimental and unverified.

`THIRD-PARTY-NOTICES.txt` records the exact package versions and license links for .NET, Magick.NET, CommunityToolkit.Mvvm, Microsoft.Extensions packages, xUnit, and Inno Setup.

- [ ] **Step 7: Run full verification**

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

Expected:

- Build: zero warnings, zero errors.
- Tests: all automated tests pass.
- Publish: `artifacts/publish/win-x64/ImageToolkit.App.exe` exists.
- Package: `artifacts/installer/ImageToolkitSetup.exe` exists.

- [ ] **Step 8: Run installation smoke test**

Install the generated setup package, launch from the Start Menu, process copies of the seven images in `测试图/`, verify originals remain unchanged, uninstall, and verify user-created output and `%LOCALAPPDATA%\ImageToolkit` data are handled according to the documented uninstall policy.

- [ ] **Step 9: Commit**

```powershell
git add scripts installer README.md THIRD-PARTY-NOTICES.txt
git commit -m "build: publish and package image toolkit"
```

## Task 15: Final Review and Repository Integration

**Files:**
- Modify only files required by review findings

- [ ] **Step 1: Run formatting**

```powershell
dotnet format ImageToolkit.sln --verify-no-changes
```

If formatting changes are required:

```powershell
dotnet format ImageToolkit.sln
```

- [ ] **Step 2: Run complete verification again**

```powershell
dotnet build ImageToolkit.sln -c Release
dotnet test ImageToolkit.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
```

- [ ] **Step 3: Review working tree**

```powershell
git status --short
git diff --check
git log --oneline --decorate -15
```

Expected: no whitespace errors; only intentionally untracked user assets remain.

- [ ] **Step 4: Review requirement coverage**

Compare the implementation against every MVP item in sections 34, 38, 41, 42, 44, 45, and 46 of `image-toolkit-codex-requirements.md`. Record any platform-only manual validation that cannot run in the current environment.

- [ ] **Step 5: Commit review fixes**

```powershell
git add src tests scripts installer README.md THIRD-PARTY-NOTICES.txt
git commit -m "fix: address final mvp review"
```

- [ ] **Step 6: Synchronize with GitHub safely**

```powershell
git fetch origin
git branch -M main
git rebase origin/main
git push -u origin main
```

If `origin/main` does not exist, omit the rebase and push the local `main`. Never force-push.
