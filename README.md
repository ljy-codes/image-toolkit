# 图批处理 Image Toolkit

[![Release](https://img.shields.io/github/v/release/ljy-codes/image-toolkit?display_name=tag&sort=semver)](https://github.com/ljy-codes/image-toolkit/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-11%20x64-0078D4?logo=windows11&logoColor=white)](#运行环境)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](#开发与构建)
[![License](https://img.shields.io/github/license/ljy-codes/image-toolkit)](LICENSE)

图批处理是一款开源的 Windows 本地桌面图片批处理工具。它把图片导入、预览、压缩、尺寸调整、格式转换和安全导出集中在一个工作台中完成。

图片处理全程在本机执行，不上传图片、文件路径、配置或日志。

## 软件界面

![图批处理主界面](docs/images/main-window.png)

## 下载

前往 [GitHub Releases](https://github.com/ljy-codes/image-toolkit/releases/latest) 下载最新正式版本。

正式版提供以下附件：

- `ImageToolkit-Setup-v1.0.0.exe`：Windows 11 x64 自包含安装包。
- `ImageToolkit-Product-Guide-v1.0.0.pdf`：产品与功能介绍。
- `ImageToolkit-Install-Guide-v1.0.0.pdf`：安装、卸载和校验说明。

安装包自包含 .NET 运行环境，普通用户无需额外安装 .NET SDK。

## 快速开始

1. 从 Releases 下载 `ImageToolkit-Setup-v1.0.0.exe`。
2. 双击安装包，按向导完成安装。
3. 启动“图批处理”，点击“添加图片”或“添加文件夹”。
4. 在右侧设置目标大小、尺寸、格式、背景和输出方式。
5. 选择队列中的图片检查处理后预览。
6. 点击“开始处理”，完成后按输出路径取用文件。

> 当前安装包未进行代码签名。Windows SmartScreen 可能显示保护提示，请核对下载来源和 Release 中的 SHA256 后再运行。

## 核心功能

| 能力 | 说明 |
| --- | --- |
| 批量导入 | 支持添加图片、文件夹、拖放导入和子文件夹扫描。 |
| 目标大小压缩 | JPEG/WebP 按目标文件大小搜索质量参数，并可在允许范围内降低分辨率。 |
| PNG 优化 | 优先执行无损压缩，可选择有损量化，仍不达标时再自动缩放。 |
| 尺寸与比例 | 指定宽高、锁定原始比例、按目标比例裁剪或使用背景补边。 |
| 背景处理 | 支持白色、黑色、透明和自定义颜色；输出 JPEG 时安全合成背景。 |
| 格式转换 | 可保持原格式，或输出 JPEG、PNG、WebP、BMP。 |
| 元数据管理 | 可保留普通 EXIF 与 ICC 色彩配置；GPS 信息默认删除。 |
| 实时预览 | 显示原图和处理后效果，参数变化时自动取消过期预览。 |
| 批任务控制 | 支持暂停、继续、安全取消和失败项重试。 |
| 输出详情 | 展示输出大小、最终尺寸、质量等级、路径和未达标原因。 |
| 外观设置 | 支持浅色、深色、跟随系统主题、字体大小和工作区背景设置。 |

## 格式支持

| 格式 | 输入 | 输出 | 备注 |
| --- | :---: | :---: | --- |
| JPEG / JPG | 是 | 是 | 支持目标大小压缩、背景合成和质量搜索。 |
| PNG | 是 | 是 | 支持透明通道、无损压缩和可选有损量化。 |
| WebP | 是 | 是 | 支持质量搜索和格式转换。 |
| BMP | 是 | 是 | 适合无损中间文件或兼容性输出。 |
| TIFF | 是 | 有限 | 单页 TIFF 可保持原格式；多页 TIFF 不允许覆盖原文件。 |
| HEIC / AVIF | 否 | 否 | 当前版本暂不支持。 |

## 工作流程

### 1. 导入

通过文件选择器、文件夹扫描或拖放添加图片。队列会显示文件名、尺寸、大小、状态和处理结果。

### 2. 设置与预览

可组合设置压缩、目标大小、尺寸、比例、裁剪锚点、背景、元数据和输出位置。选择队列图片后会自动生成处理预览。

### 3. 批量处理

任务运行期间可以暂停、继续或安全取消。失败项可单独重试，程序关闭时会等待后台任务退出。

### 4. 获取结果

支持三种输出模式：

- 在原目录创建自动避让重名的新文件。
- 输出到指定目录。
- 经二次确认后覆盖原文件。

## 隐私与安全

### 本地隐私

- 图片读取、解码、处理和编码均在当前电脑完成。
- 不上传图片、文件路径、配置或日志。
- GPS 信息默认删除，仅在用户主动开启后保留。
- 日志保存在 `%LOCALAPPDATA%\ImageToolkit\Logs`，最多保留 14 个按日文件。

### 输出安全

- 新文件输出时自动处理重名，避免意外覆盖。
- 覆盖模式必须保持原格式并再次确认。
- 覆盖原图时先写入同目录临时文件，校验可读性后再进行原子替换。
- 取消任务不会保留未完成的空占位文件。
- 多页 TIFF 不允许覆盖原文件，避免页面丢失。

## 安装与卸载

![图批处理安装向导](docs/images/installer-wizard.png)

### 安装

运行 Release 中的安装包并按向导继续。默认仅为当前 Windows 用户安装，可选择是否创建桌面快捷方式。

### 卸载

可使用以下任一方式：

- Windows“设置” -> “应用” -> “已安装的应用” -> 搜索“图批处理” -> “卸载”。
- Windows 开始菜单 -> “图批处理” -> 运行卸载入口。

卸载不会删除用户生成或导出的图片。配置和日志可能继续保留在 `%LOCALAPPDATA%\ImageToolkit`，确认不再需要后可手动删除。

## 运行环境

- 已验证：Windows 11 x64。
- ARM64：实验性支持，尚未完成正式验证。
- 安装包：自包含运行环境，无需单独安装 .NET。
- 开发环境：.NET 10 SDK、PowerShell、Inno Setup 6。

## 已知限制

- HEIC、AVIF 暂不支持。
- AI 抠图和本地 AI 模型不属于当前版本范围。
- 多页 TIFF 可以作为输入生成新文件，但不会覆盖原文件。
- 安装包尚未代码签名，SmartScreen 可能显示提示。

## 项目结构

```text
src/
  ImageToolkit.App/               WPF 桌面应用和视图模型
  ImageToolkit.Application/       用例、验证和批任务协调
  ImageToolkit.Domain/            领域模型、枚举和接口
  ImageToolkit.Infrastructure/    图片处理、文件、配置和日志
  ImageToolkit.Infrastructure.AI/ AI 扩展边界
tests/
  ImageToolkit.App.Tests/
  ImageToolkit.Application.Tests/
  ImageToolkit.Domain.Tests/
  ImageToolkit.Infrastructure.Tests/
installer/                         Inno Setup 安装脚本
scripts/                           构建、测试、发布和打包脚本
```

## 开发与构建

要求安装 .NET 10 SDK。仓库使用中央包版本管理。

```powershell
dotnet restore ImageToolkit.sln
dotnet build ImageToolkit.sln -c Release -m:1 -p:UseSharedCompilation=false
dotnet test ImageToolkit.sln -c Release -m:1 -p:UseSharedCompilation=false
dotnet run --project src/ImageToolkit.App
```

也可以使用仓库脚本：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -ExecutionPolicy Bypass -File scripts/test.ps1 -IncludeUserAssets
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
powershell -ExecutionPolicy Bypass -File scripts/package.ps1 -Version 1.0.0
```

发布输出：

- 自包含程序：`artifacts/publish/win-x64`
- 安装包：`artifacts/installer/ImageToolkitSetup.exe`

## 测试

正式安装包发布前已完成：

- 74 项 Domain、Application、Infrastructure 和 App 自动化测试。
- 413 个发布文件与安装后文件逐项 SHA256 比对。
- Windows 安装、启动、主窗口加载、正常退出和卸载冒烟测试。

## 版本记录

参见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

本项目使用 [MIT License](LICENSE)。
