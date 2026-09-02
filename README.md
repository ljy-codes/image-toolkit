# 图批处理

图批处理是一款面向 Windows 11 x64 的本地桌面图片批处理工具。图片在本机完成读取、预览、压缩和导出，不上传到远程服务。

## 功能

- 批量导入图片或文件夹，支持拖放和子文件夹扫描。
- 支持 JPEG、PNG、WebP、BMP、TIFF 输入。
- 按目标文件大小压缩，JPEG 和 WebP 默认最低质量为 45。
- 调整宽高、保持比例、按比例裁剪或补边。
- 白色、黑色、透明和自定义背景。
- 保留普通 EXIF 和 ICC，默认删除 GPS 信息。
- 原图与处理后预览，批任务支持暂停、继续和安全取消。
- 原目录新文件、指定目录或确认后覆盖原文件。
- 浅色、深色、跟随系统主题，支持字体和工作区背景设置。

## 支持范围

- 已验证：Windows 11 x64。
- ARM64：实验性，未验证。
- HEIC/AVIF：MVP 暂不支持。
- 多页 TIFF 可作为输入生成新文件，但为避免页面丢失，不允许覆盖原文件。
- AI 抠图和 AI 模型：不属于当前 MVP，代码仅保留扩展边界。

## 安装与卸载

运行 `ImageToolkitSetup.exe`，按向导安装。默认仅为当前用户安装，可选创建桌面快捷方式。

可从 Windows“已安装的应用”中卸载。卸载不会删除用户生成的图片，也不会删除 `%LOCALAPPDATA%\ImageToolkit` 下的配置和日志；如不再需要，可由用户手动清理该目录。

## 隐私

- 图片处理在本机完成。
- 程序不上传图片、文件路径、配置或日志。
- 日志保存在 `%LOCALAPPDATA%\ImageToolkit\Logs`，最多保留 14 个按日文件。
- GPS 元数据默认删除，只有用户主动开启后才保留。

## 开发

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

自包含发布输出到 `artifacts/publish/win-x64`。安装包输出到 `artifacts/installer/ImageToolkitSetup.exe`，打包需要 Inno Setup 6。

## 配置与输出安全

配置文件位于 `%LOCALAPPDATA%\ImageToolkit\config.json`。新文件采用自动避让命名；覆盖原文件时先写入同目录临时文件、校验可读性，再进行原子替换。取消任务不会保留未完成的空占位文件。
