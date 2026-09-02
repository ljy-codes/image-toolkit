# Image Toolkit 设计说明

**日期：** 2026-09-02  
**状态：** 已确认  
**需求基线：** `image-toolkit-codex-requirements.md` v4  
**第一阶段范围：** Windows 11 x64 非 AI 图片批处理 MVP  
**第二阶段范围：** 本地 ONNX AI 智能抠图

## 1. 目标

构建一个面向普通 Windows 11 用户的本地图片批处理桌面应用：

- 不要求用户安装 .NET Runtime、SDK、ImageMagick、Python 或 Node.js。
- 普通图片处理全部在本机完成。
- AI 模型按需安装，推理在本机完成。
- 支持单图、多图、文件夹和拖拽导入。
- 支持尺寸、比例、裁剪、补边、格式转换和目标文件大小压缩。
- 支持安全覆盖原文件、批量进度、取消、暂停、失败重试和明确的未达标状态。
- 最终交付可编译、可测试、可发布、可安装。

## 2. 已确认产品决策

### 2.1 界面语言

第一版仅提供简体中文界面。

用户可见文本集中到 WPF 资源字典，不增加语言切换入口，但避免将中文散落在 ViewModel 和业务服务中。

### 2.2 主工作流

采用单窗口工作台：

```text
顶部：添加图片 / 添加文件夹 / 清空 / 设置
左侧：文件、尺寸、比例与裁剪、背景、压缩、输出、AI
中部上方：原图与处理后预览
中部下方：可调整高度的批量文件列表
右侧：当前功能参数
底部：汇总状态、进度、暂停/取消、开始处理
```

不采用分步向导，因为重复批处理时操作步骤过多。

不采用独立功能标签页，因为尺寸、裁剪、格式与压缩必须组合成同一处理流水线。

### 2.3 工程架构

采用分层模块化单体。

不采用单 WPF 工程堆叠业务服务的方式。

不采用每个功能各自维护图片编解码流程的纵向切片方式。

### 2.4 PNG 量化

PNG 无损优化默认启用。

PNG 有损颜色量化放入高级设置，默认关闭。未主动开启量化时，如果 PNG 无法满足目标大小，则返回“未达标”，不静默损失颜色。

## 3. 技术栈

```text
C#
.NET 10 LTS
WPF
MVVM
Magick.NET-Q8-x64
CommunityToolkit.Mvvm
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Logging.Abstractions
ONNX Runtime（第二阶段）
xUnit
Inno Setup
win-x64 Self-contained
```

约束：

- 不使用 `System.Drawing.Common`。
- 不启用单文件发布。
- 不要求用户安装系统级图片 Codec。
- 不将在线 AI API 作为基础功能依赖。
- NuGet 版本统一锁定在 `Directory.Packages.props`。

## 4. Solution 结构

```text
ImageToolkit.sln
├── Directory.Build.props
├── Directory.Packages.props
├── src/
│   ├── ImageToolkit.App/
│   ├── ImageToolkit.Application/
│   ├── ImageToolkit.Domain/
│   ├── ImageToolkit.Infrastructure/
│   └── ImageToolkit.Infrastructure.AI/
├── tests/
│   ├── ImageToolkit.Domain.Tests/
│   ├── ImageToolkit.Application.Tests/
│   ├── ImageToolkit.Infrastructure.Tests/
│   └── ImageToolkit.App.Tests/
├── installer/
├── scripts/
└── docs/
```

### 4.1 ImageToolkit.App

职责：

- WPF Views、Controls 和 ResourceDictionary。
- ViewModel 与命令绑定。
- 中文文案与主题资源。
- 应用启动、依赖注入和 Composition Root。
- 文件选择、文件夹选择、颜色选择及确认弹窗等 UI 适配。

限制：

- `MainWindow.xaml.cs` 不承载业务逻辑。
- ViewModel 不直接调用 Magick.NET。
- ViewModel 不直接写配置文件、日志或目标图片。

### 4.2 ImageToolkit.Application

职责：

- 导入文件和文件夹用例。
- 预览协调。
- 批量任务调度。
- 暂停、取消和失败重试。
- 将 UI 草稿配置验证并编译为不可变处理请求。
- 调用图片处理、命名、输出和 AI 接口。

建议核心类型：

```text
ImportImagesUseCase
BuildPreviewUseCase
RunBatchUseCase
RetryFailedItemsUseCase
ImageProcessingPipeline
BatchTaskCoordinator
ProcessingRequestValidator
```

### 4.3 ImageToolkit.Domain

职责：

- 不可变配置模型。
- 压缩质量与分辨率边界。
- 裁剪、补边和尺寸规则。
- 任务状态和结果。
- 业务错误码。
- 基础设施接口。

建议核心类型：

```text
ProcessingRequest
CompressionOptions
ResizeOptions
AspectRatioOptions
BackgroundOptions
MetadataOptions
OutputOptions
ImageProcessingResult
BatchItem
BatchRunState
IImageProcessor
IOutputPathResolver
IAtomicFileWriter
IConfigurationStore
IBackgroundRemovalEngine
IAiModelManager
```

Domain 不引用 WPF、Magick.NET、ONNX Runtime、文件系统实现或网络实现。

### 4.4 ImageToolkit.Infrastructure

职责：

- Magick.NET 图片读取、像素处理和编码。
- EXIF Orientation、GPS、普通 EXIF 和 ICC 处理。
- 文件命名与同名冲突处理。
- 临时文件、安全写入和原子替换。
- JSON 配置持久化。
- 滚动日志实现。

### 4.5 ImageToolkit.Infrastructure.AI

职责：

- ONNX Runtime Session 管理。
- 模型 Manifest 解析。
- 模型下载、校验、安装、更新和删除。
- 模型 Adapter 的预处理、推理、后处理和 Mask 生成。

第一阶段不引用或发布该项目的 ONNX Runtime 依赖。第二阶段接入后，模型文件仍不进入基础安装包。

### 4.6 依赖方向

```text
ImageToolkit.App
    ├── ImageToolkit.Application
    ├── ImageToolkit.Infrastructure
    └── ImageToolkit.Infrastructure.AI（第二阶段）

ImageToolkit.Application
    └── ImageToolkit.Domain

ImageToolkit.Infrastructure
    └── ImageToolkit.Domain

ImageToolkit.Infrastructure.AI
    └── ImageToolkit.Domain
```

Domain 不反向依赖任何实现层。

## 5. 图片处理流水线

每张图片使用同一条不可变流水线：

```text
读取文件信息
→ Decode 一次
→ EXIF 自动旋正并将 Orientation 设为 1
→ AI 抠图（仅第二阶段且用户启用时）
→ 比例处理：裁剪或补边
→ 尺寸调整
→ Alpha / 背景与输出格式协调
→ EXIF、GPS、ICC 处理
→ 搜索编码参数
→ 安全写入
→ 校验结果并释放资源
```

### 5.1 Decode 与 Encode 约束

- 原文件只 Decode 一次。
- 裁剪、补边、缩放和背景合成在同一个内存像素对象上完成。
- 压缩参数搜索可以将同一份处理后像素多次编码到临时内存流或临时缓冲区。
- 压缩搜索不得重新读取原文件或重复执行像素变换。
- 最终结果只向目标路径持久化一次。
- 不生成中间 JPG 后重新读取并二次有损编码。

## 6. 比例、裁剪、补边和尺寸

### 6.1 比例模式

比例处理为互斥模式：

- 原始比例。
- 裁剪到目标比例。
- 补边到目标比例。

裁剪位置：

- 居中。
- 顶部。
- 底部。
- 左侧。
- 右侧。

补边背景：

- 白色。
- 黑色。
- 透明。
- 自定义颜色。

### 6.2 尺寸规则

- 未启用尺寸修改时，保持比例处理后的尺寸。
- 仅指定宽度或高度时，按锁定比例计算另一边。
- 同时指定宽高且锁定比例时，必须校验目标尺寸是否与当前比例兼容。
- 同时指定宽高且不锁定比例时，允许拉伸，但 UI 必须明确展示该结果。
- 不为了满足压缩目标放大原图。

处理顺序固定为：

```text
比例处理 → 尺寸调整 → 文件大小压缩
```

用户明确指定最终尺寸后，该尺寸是压缩阶段的硬约束。

## 7. 文件大小压缩

### 7.1 通用结果

压缩结果区分：

```text
成功
未达标
失败
已取消
```

“未达标”是正常业务结果，不作为未捕获异常处理。

结果至少记录：

- 实际输出大小。
- 最终尺寸。
- 实际编码质量（适用时）。
- 是否发生自动降分辨率。
- 是否发生 PNG 颜色量化。
- 未达标原因。

### 7.2 JPEG 与 WebP

算法：

1. 以较高 Quality 开始。
2. 在起始 Quality 与最低 Quality 之间进行二分查找。
3. 每次编码到临时缓冲区并获取长度。
4. 选择不超过目标大小的最高 Quality。
5. 默认最低 Quality 为 45。
6. 高级设置允许范围为 20 至 95。

如果最低 Quality 仍超过目标：

- 用户指定最终尺寸：返回“未达标”。
- 用户未指定最终尺寸：进入自动降分辨率流程。

### 7.3 自动降分辨率

约束：

- 保持当前宽高比例。
- 不低于进入最终压缩阶段尺寸的 25%。
- 短边不低于 320 px。
- 原图短边本身低于 320 px 时不放大。
- 到达任一边界后停止，不无限降低质量或尺寸。

每次降低尺寸后重新执行质量搜索，直到：

- 找到满足目标的结果。
- 达到最低质量。
- 达到最低尺寸。
- 收到取消请求。

### 7.4 PNG

顺序：

1. 执行无损压缩优化。
2. 判断是否满足目标。
3. 用户主动启用颜色量化时，执行保守的调色板优化并保留 Alpha。
4. 用户未指定最终尺寸时，允许进入自动降分辨率流程。
5. 达到边界仍无法满足目标时返回“未达标”。

PNG 不复用 JPEG/WebP 的 Quality 语义。

## 8. Metadata 与色彩管理

### 8.1 EXIF Orientation

- Decode 后立即按 Orientation 旋正像素。
- 输出时将 Orientation 设为 1。
- 测试旋转和镜像方向，防止二次旋转。

### 8.2 GPS 与普通 Metadata

默认：

- 保留普通 EXIF。
- 删除 GPS 位置相关字段。
- 不记录敏感 Metadata 到日志。

后续的一键清除 Metadata 不能直接破坏 ICC 色彩语义。

### 8.3 ICC

默认策略：

```text
源图存在 ICC
→ 目标格式和编码路径可可靠嵌入
→ 保留源 ICC
```

无法可靠保留时：

```text
按源 ICC 将像素转换到 sRGB
→ 使用 sRGB 像素输出
→ 根据输出策略嵌入 sRGB Profile 或移除 Profile
```

禁止只删除 Adobe RGB 或 Display P3 Profile 而保持原像素数值。

测试素材至少包含：

- sRGB。
- Adobe RGB。
- Display P3（能获得合法测试素材时）。
- 无 ICC Profile。

## 9. 输出与数据安全

### 9.1 输出模式

- 原目录生成新文件。
- 覆盖原文件。
- 指定目录。

### 9.2 文件命名

默认后缀：

```text
-已处理
```

冲突序列：

```text
photo-已处理.jpg
photo-已处理-2.jpg
photo-已处理-3.jpg
```

路径解析和冲突检查必须由统一服务完成。

### 9.3 安全覆盖

覆盖流程：

```text
在原文件同目录生成 GUID 临时文件
→ 写入并 Flush
→ 重新读取并验证格式、尺寸和非零长度
→ 使用 File.Replace 替换原文件并保留短期备份
→ 成功后删除备份
```

约束：

- 临时文件与原文件保持同一卷。
- 替换失败时原文件必须保持完整。
- 不使用“删除原文件再移动临时文件”的降级方案。
- 文件系统不支持安全替换时任务失败，并给出中文原因。
- 取消或异常后清理未使用的临时文件。

## 10. 批量任务

### 10.1 状态

文件状态：

```text
等待
处理中
已完成
未达标
失败
已取消
```

批量状态：

```text
Idle
Running
Paused
Cancelling
Completed
CompletedWithIssues
```

状态转换：

```text
Idle → Running ⇄ Paused
Running / Paused → Cancelling → Completed
Running → CompletedWithIssues
```

同一窗口不能同时运行两个批量任务。

### 10.2 调度

- 使用有界任务队列。
- 默认并发数根据 CPU 自动计算，并设置保守上限。
- 允许用户选择自动、1、2、4。
- 不一次加载所有原图像素。
- 单张完成后立即释放 Magick.NET 对象和临时缓冲区。
- 单张失败或未达标不终止整个批量任务。

### 10.3 暂停和取消

- 暂停只阻止新任务开始。
- 已进入编码阶段的单张任务完成后进入暂停。
- 取消通过 `CancellationToken` 传播。
- 收到取消后不再启动新文件。
- 当前文件在安全边界响应取消，不能留下半成品目标文件。

批量任务开始时复制一份不可变配置快照。运行期间 UI 参数修改只影响下一次任务。

## 11. 预览

- 预览使用独立的低分辨率流水线。
- 不复用批量任务队列。
- 参数变化使用防抖。
- 新预览请求取消旧请求。
- 不为批量列表全部图片生成高分辨率预览。
- AI 预览显示加载状态。
- 预览不能修改或写入原文件。

## 12. WPF 与 MVVM

### 12.1 ViewModel 拆分

建议：

```text
MainWindowViewModel
FileQueueViewModel
PreviewViewModel
ResizeSettingsViewModel
AspectRatioSettingsViewModel
BackgroundSettingsViewModel
CompressionSettingsViewModel
OutputSettingsViewModel
BatchProgressViewModel
ApplicationSettingsViewModel
AiSettingsViewModel（第二阶段）
```

`MainWindowViewModel` 只协调子 ViewModel 和页面状态。

### 12.2 配置编辑

- UI 编辑的是可验证草稿。
- 草稿通过校验后生成不可变 `ProcessingRequest`。
- 预览和批量任务都使用不可变请求。
- ViewModel 不直接修改运行中的任务配置。

### 12.3 界面尺寸和高 DPI

- 初始窗口建议 1280×800 DIP。
- 使用 Grid、共享尺寸组和自适应列。
- 不使用依赖固定物理像素的绝对定位。
- 启用 `PerMonitorV2`。
- 图标使用 Windows 11 自带 Segoe Fluent Icons 字形或项目内矢量资源。
- 验收覆盖 100%、125%、150%、200%。
- 检查跨不同 DPI 显示器拖动窗口后的重新布局。

## 13. 用户反馈与错误处理

### 13.1 行级结果

用于：

- 图片读取失败。
- 压缩未达标。
- 图片保存失败。
- 图片已取消。

每行展示简短中文原因，并允许失败重试。

### 13.2 非阻塞提示

用于：

- JPG 不支持透明背景，自动切换 PNG。
- 输出目录不可写。
- 部分文件导入失败。
- 当前格式不支持某项 Metadata。

### 13.3 阻塞弹窗

仅用于：

- 覆盖原文件全局确认。
- AI 模型安装确认。
- 应用无法继续启动。

### 13.4 日志

日志目录：

```text
%LOCALAPPDATA%\ImageToolkit\logs\
```

记录：

- 错误码。
- 当前处理阶段。
- 异常栈。
- 为定位文件错误所需的路径。
- 模型下载、校验、加载和推理失败。

不记录：

- 图片内容。
- 图片二进制。
- 敏感 Metadata。

## 14. 配置持久化

配置目录：

```text
%LOCALAPPDATA%\ImageToolkit\config.json
```

配置写入使用临时文件和替换方式，损坏时回退默认配置并保留损坏副本供诊断。

保存：

- 目标文件大小。
- 压缩最低 Quality。
- PNG 量化开关。
- 默认比例与尺寸。
- 输出格式、路径和后缀。
- 主题、工作区背景和字体档位。
- GPS 保留设置。
- 并发数。
- AI 默认模型和更新偏好。

AI 模型是否可用不能只依赖配置值。

## 15. AI 设计

### 15.1 接口

```text
BackgroundRemovalService
→ IBackgroundRemovalEngine
→ IBackgroundRemovalModelAdapter
→ 具体模型 Adapter
```

建议接口职责：

```text
IAiModelManager
├── IsInstalledAsync
├── DownloadAsync
├── VerifyAsync
├── InstallAsync
├── CheckUpdateAsync
├── UpdateAsync
└── UninstallAsync

IBackgroundRemovalModelAdapter
├── PreProcess
├── RunInference
├── PostProcess
└── BuildMask
```

### 15.2 安装流程

```text
读取远端 Manifest
→ 展示模型大小、磁盘占用、许可证和隐私说明
→ 用户确认
→ 下载到 .download 临时文件
→ SHA-256 校验
→ ONNX Session 试加载
→ 原子移动到版本目录
→ 更新当前版本指针
```

下载、校验或试加载失败时，不产生“已安装”状态。

### 15.3 Manifest

必须包含：

- 模型 ID。
- 显示名称。
- 版本。
- 模型文件名。
- 输入宽高。
- 预处理参数或 Adapter ID。
- 下载 URL。
- SHA-256。
- 许可证标识。
- 许可证 URL。
- 权重来源。
- 模型大小。

### 15.4 默认模型候选

U²-Net 系列作为通用主体抠图候选。

上线门禁：

- 必须确认具体权重的许可证。
- 必须确认允许重新分发或应用内自动下载。
- 必须记录权重来源和 SHA-256。
- 必须完成 CPU 性能和效果测试。
- 任何一项证据不足时，不发布模型下载入口。

MODNet 可作为后续人物专用候选。

不采用：

- BRIA RMBG-2.0：公开模型卡限制非商业使用。
- Robust Video Matting：GPL-3.0 与当前默认分发目标不匹配。

### 15.5 推理 Provider

- 正式基线为 CPU ONNX Runtime。
- DirectML 只预留扩展点，不承诺首版 GPU 加速。
- AI 模型删除后基础功能继续工作。

## 16. 格式支持边界

MVP 读取：

```text
JPG
JPEG
PNG
WebP
BMP
TIFF
```

MVP 输出：

```text
保持原格式
JPG
PNG
WebP
BMP
```

HEIC 和 AVIF 不进入 MVP，不显示为已支持格式。

以后启用前必须同时满足：

- Magick.NET 实际构建包含所需 delegate。
- Native delegate 和 codec 随应用自包含发布。
- 干净 Windows 11 无额外 Codec 时可运行。
- 解码和编码许可证允许目标分发方式。
- 具备自动化或干净机回归测试。

## 17. 测试策略

### 17.1 Domain.Tests

覆盖：

- 默认最低 Quality 45。
- Quality 配置范围 20 至 95。
- 用户尺寸硬约束。
- 自动降尺寸 25% 下限。
- 短边 320 px 下限。
- 小图不放大。
- 裁剪几何。
- 补边几何。
- 文件命名冲突。
- 批量状态转换。

### 17.2 Application.Tests

覆盖：

- 流水线顺序。
- 配置草稿验证。
- 任务配置快照不可变。
- 单张失败隔离。
- 未达标不终止批量。
- 暂停不启动新任务。
- 取消传播。
- 失败重试仅处理失败项。
- 预览请求取消旧请求。

### 17.3 Infrastructure.Tests

使用真实临时目录和合法生成的测试图片覆盖：

- JPG、PNG、WebP 读取和输出。
- EXIF Orientation 旋正。
- GPS 删除。
- 普通 EXIF 保留。
- ICC 保留。
- 无法保留 ICC 时转 sRGB。
- PNG Alpha 保留。
- 覆盖成功。
- 写入失败时原文件保持完整。
- 同名冲突。
- 临时文件清理。

### 17.4 App.Tests

覆盖：

- 命令启用状态。
- 中文校验信息。
- 覆盖确认。
- 透明背景与 JPG 的自动格式切换。
- 运行期间参数只影响下一次任务。

### 17.5 DPI 与发布验收

手工或 UI 自动化矩阵：

```text
100%
125%
150%
200%
```

检查：

- 文本不截断。
- 弹窗、下拉框和列表不偏移。
- 预览不模糊。
- 跨屏移动后布局正确。

干净机：

- Windows 11 x64。
- 无 .NET SDK。
- 无 .NET Runtime。
- 无 Visual Studio。
- 无 ImageMagick。
- 无额外图片 Codec。
- 无 Python、Node.js 和 AI API Key。

### 17.6 AI.Tests

第二阶段覆盖：

- Fake Adapter。
- Manifest 校验。
- SHA-256 成功和失败。
- 下载中断。
- 安装原子性。
- Session 试加载失败。
- CPU 实际推理。
- 透明 PNG 输出。
- 模型删除后基础功能可用。

## 18. 发布设计

命令：

```powershell
dotnet build
dotnet test
dotnet publish -c Release -r win-x64 --self-contained true
ISCC.exe installer\ImageToolkit.iss
```

产物：

```text
artifacts/
├── publish/win-x64/
└── installer/ImageToolkitSetup.exe
```

安装包：

- 开始菜单快捷方式。
- 可选桌面快捷方式。
- 卸载入口。
- 版本信息。
- 隐私说明和第三方许可证。
- 为 Authenticode 签名预留脚本参数。

不启用 `PublishSingleFile`。

第一版官方支持 Windows 11 x64。ARM64 只保留实验性说明，不声明原生支持。

## 19. 第三方许可策略

需要在仓库中维护：

```text
THIRD-PARTY-NOTICES.txt
docs/licenses/
```

当前判断：

- .NET 10：Microsoft 发布支持策略。
- Magick.NET：Apache-2.0。
- CommunityToolkit.Mvvm：MIT。
- ONNX Runtime：MIT。
- Inno Setup：开发阶段可用，商业发布前确认并购买适用许可证。
- AI：代码仓库许可证与具体模型权重许可证分别记录。

任何依赖升级都必须重新检查许可证和 Native 分发内容。

## 20. 开发阶段

### 阶段 0：工程与验证基线

- 初始化 Solution。
- 建立项目引用和中央包管理。
- 建立测试项目。
- 建立构建、测试、发布脚本。
- 建立第三方许可证记录。

### 阶段 1：非 AI 核心

- Domain 配置和结果模型。
- 文件导入。
- 比例、裁剪、补边和尺寸。
- JPEG、WebP 和 PNG 压缩。
- Metadata 和 ICC。
- 输出命名和安全写入。
- 批量队列、暂停、取消和失败重试。
- 单窗口 WPF 工作台。
- 配置、主题、日志和预览。

### 阶段 2：安装与干净机验收

- win-x64 Self-contained 发布。
- Inno Setup。
- DPI 验收。
- 干净 Windows 11 x64 验收。

### 阶段 3：AI

- 模型许可证定案。
- Model Manager。
- ONNX Engine 和 Adapter。
- 按需安装。
- CPU 推理。
- 透明和纯色背景。
- AI 设置页面。

## 21. 风险与处置

### 21.1 当前开发环境缺少 SDK

当前机器未检测到 `dotnet` 和 `ISCC.exe`。

处置：实施前安装 .NET 10 SDK；安装包阶段再安装 Inno Setup。安装后记录版本并执行最小验证。

### 21.2 压缩目标与画质冲突

处置：最低 Quality、最低分辨率和“未达标”是硬边界，不能以目标大小为唯一成功标准。

### 21.3 ICC 偏色

处置：ICC 保留和 sRGB 转换属于核心基础设施能力，并使用真实 Profile 测试。

### 21.4 覆盖原图损坏

处置：同目录临时文件、重新读取校验、原子替换和备份；不使用删除原图再移动的降级方式。

### 21.5 大批量内存压力

处置：有界队列、保守并发、按张释放、不同时生成全部预览。

### 21.6 AI 权重许可不明确

处置：许可证据是发布门禁；不能确认时不提供模型下载入口。

### 21.7 HEIC/AVIF 自包含与许可

处置：MVP 不支持；后续必须完成 Native delegate、许可和干净机验证。

### 21.8 Inno Setup 商业许可

处置：开发和内部测试阶段保留 Inno Setup；商业发布前完成许可确认和采购。

## 22. 外部依据

- .NET 支持策略：<https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core>
- Magick.NET：<https://github.com/dlemstra/Magick.NET>
- Magick.NET 许可证：<https://github.com/dlemstra/Magick.NET/blob/main/License.txt>
- ONNX Runtime C#：<https://onnxruntime.ai/docs/get-started/with-csharp.html>
- ONNX Runtime 许可证：<https://github.com/microsoft/onnxruntime/blob/main/LICENSE>
- DirectML Execution Provider：<https://onnxruntime.ai/docs/execution-providers/DirectML-ExecutionProvider.html>
- CommunityToolkit.Mvvm：<https://github.com/CommunityToolkit/dotnet>
- U²-Net：<https://github.com/xuebinqin/U-2-Net>
- MODNet：<https://github.com/ZHKKKe/MODNet>
- BRIA RMBG-2.0 模型卡：<https://huggingface.co/briaai/RMBG-2.0>
- Robust Video Matting：<https://github.com/PeterL1n/RobustVideoMatting>
- Inno Setup：<https://jrsoftware.org/isinfo.php>

## 23. 验收结论

本设计满足需求 v4 的核心边界：

- 基础功能与 AI 解耦。
- 图片处理本地完成。
- 单次 Decode、单像素处理链路和单次最终持久化。
- 压缩质量和分辨率存在硬下限。
- 用户尺寸不可被压缩逻辑擅自修改。
- Metadata、GPS 和 ICC 有明确策略。
- 覆盖原文件有安全边界。
- WPF MVVM 和高 DPI 有明确结构。
- 发布目标为 Windows 11 x64 Self-contained 安装包。

下一步是在本设计通过书面审阅后，编写分阶段、测试优先的实施计划。
