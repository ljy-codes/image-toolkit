# image-toolkit — Codex 开发需求说明（v4）

> 本文档是 `image-toolkit` 的正式开发需求基线，整合了原始需求、工程审阅意见以及后续技术修订。  
> v4 在 v3 基础上新增：压缩质量/分辨率下限、ICC Color Profile 处理、HEIC/AVIF 自包含约束、高 DPI 适配、ARM64 支持边界与对应验收标准。  
> Codex 必须优先完整阅读本文档，再进行架构设计、编码、测试与发布。

---

# 1. 项目名称

```text
image-toolkit
```

软件显示名称建议：

```text
Image Toolkit
```

目标：开发一个面向普通 Windows 11 用户的现代化本地图片批处理桌面工具。

---

# 2. 目标用户与最终交付要求

目标用户不是开发人员，因此最终交付必须满足：

- 不需要安装 Python
- 不需要安装 Node.js
- 不需要安装 .NET Runtime
- 不需要安装 Visual Studio / .NET SDK
- 不需要配置任何 AI API Key
- 不需要安装任何第三方开发环境
- 普通用户双击程序即可运行
- 所有必要运行时依赖随程序一起发布
- 普通图片处理全部在本地完成
- AI 图片处理优先在本地完成
- 用户图片不得上传到第三方服务

最终交付目标：

```text
Windows 11
+
Image Toolkit 安装包 / 发布目录
+
无需额外环境
=
直接可用
```

---

# 3. 固定技术选型

本项目不再让 Codex自由摇摆核心技术栈，第一版固定采用：

```text
C#
.NET 10 LTS
WPF
MVVM
Magick.NET-Q8-x64
ONNX Runtime
Inno Setup
win-x64 Self-contained
```

## 3.1 UI

采用：

```text
WPF + MVVM
```

要求：

- UI 和业务逻辑分离
- 不把主要逻辑堆在 `MainWindow.xaml.cs`
- 使用命令、绑定、ViewModel 管理交互
- 现代 Windows 11 风格
- 稳定和易维护优先于追逐新 UI 框架

## 3.2 图片处理核心

主图片处理库采用：

```text
Magick.NET-Q8-x64
```

用途包括：

- JPG / JPEG
- PNG
- WebP
- BMP
- TIFF
- Resize
- Crop
- Canvas / 补边
- 格式转换
- EXIF 处理
- Metadata 处理
- Alpha Channel
- JPEG/WebP 压缩
- PNG 优化

禁止使用：

```text
System.Drawing.Common
```

不要把 `ImageSharp = MIT` 写入文档或代码注释。若未来重新评估 ImageSharp，必须重新检查当时官方许可证和商业使用条件。

## 3.3 AI 推理

采用：

```text
ONNX Runtime
```

要求：

- CPU 必须可以运行
- NVIDIA GPU 不是硬性要求
- GPU 加速属于可选增强
- 可评估 DirectML 作为 Windows GPU 加速方案
- AI 推理不得依赖 Python
- AI 模型不得成为基础图片功能的强依赖

---

# 4. 产品定位

这是一个类似 Canva 部分本地图片处理能力的轻量 Windows 桌面工具。

核心功能：

```text
图片压缩
修改尺寸
修改宽高比例
裁剪
补边
格式转换
批量处理
透明背景
AI 智能抠图
```

产品重点：

```text
本地处理
简单易用
批量能力
普通用户可直接使用
无需 AI 账号
无需 API Key
```

---

# 5. 核心图片处理功能

## 5.1 文件大小压缩

支持将图片压缩到指定最终文件大小。

默认：

```text
≤ 1 MB
```

用户可配置：

```text
500 KB
800 KB
1 MB
2 MB
5 MB
自定义
```

UI 示例：

```text
☑ 限制文件大小

目标大小：
[ 1 ] [ MB ▼ ]
```

默认开启。

### 5.1.1 核心原则

文件大小限制是：

```text
最终输出文件大小限制
```

不是：

```text
仅修改图片像素
```

例如：

```text
原图：4000×3000，5.8MB
↓
Resize：1600×1200，1.7MB
↓
目标：≤1MB
↓
继续优化编码
↓
最终：950KB
```

### 5.1.2 JPEG / WebP

JPEG / WebP 优先通过编码质量控制。

推荐逻辑：

```text
高质量
↓
二分查找最佳 Quality
↓
找到满足目标大小的最高可接受质量
```

例如：

```text
Quality 95
Quality 90
Quality 85
...
```

要求：

- 尽量接近目标大小
- 不得超过用户设定目标
- 在满足大小的情况下尽量保留最高画质

### 5.1.3 PNG

PNG 不能简单照搬 JPEG 的 Quality 逻辑。

处理策略：

```text
PNG
↓
无损压缩优化
↓
如果仍超过目标
↓
允许可选颜色量化 / Palette 优化
↓
如果仍超过目标
↓
根据用户尺寸约束决定是否允许继续降低分辨率
```

不得假装 PNG 存在和 JPEG 完全一致的质量压缩机制。

### 5.1.4 手动尺寸是硬约束

如果用户启用了：

```text
☑ 修改图片尺寸
```

并明确设置：

```text
1200 × 900
```

那么 `1200×900` 是硬约束。

文件大小压缩阶段：

```text
可以降低编码质量
可以做合理无损优化
不允许擅自改成 800×600
```

如果在最低可接受质量 / 优化策略下仍无法达到 ≤1MB：

```text
状态：未达标 / 失败
原因：在保持 1200×900 的前提下无法压缩到 1MB
```

只有用户没有手动指定最终尺寸时，才允许：

```text
逐步降低分辨率
```

作为压缩兜底策略。

### 5.1.5 压缩质量与自动降分辨率下限

压缩目标不能以“数值达标”为唯一目标。

程序必须设置最低可接受画质边界，避免为了强行达到 `≤1MB` 而输出严重失真的图片。

默认约束：

```text
JPEG 最低 Quality：45
WebP 有损最低 Quality：45
```

这两个值作为第一版默认安全下限，并在高级设置中允许用户修改。

建议可配置范围：

```text
20 ～ 95
```

但默认用户无需理解 Quality 参数。

压缩算法：

```text
从较高 Quality 开始
↓
二分查找
↓
达到目标大小
    → 成功
触及最低 Quality 仍无法达标
    → 根据尺寸规则判断是否允许继续降分辨率
```

如果用户手动指定最终尺寸：

```text
不得继续缩小分辨率
→ 标记“未达标”
```

如果用户没有手动指定最终尺寸，可以继续自动降低分辨率，但必须设置下限。

默认自动降分辨率下限：

```text
不得低于进入最终压缩阶段时宽高的 25%
且短边不得低于 320 px
```

若原图本身短边小于 320px：

```text
不得为了满足该规则而放大原图
```

自动降分辨率应保持当前宽高比例。

当达到以下任一条件仍无法满足目标大小：

```text
最低允许 Quality
最低允许分辨率
```

必须：

```text
状态：未达标
```

并说明原因，例如：

```text
在最低画质和最低分辨率限制下，无法压缩到 1MB。
```

要求：

- 不允许无限降低 Quality
- 不允许无限降低分辨率
- “未达标”是正常、可接受的业务结果
- 批量任务中单张未达标不得阻塞其他文件
- UI 应允许用户看到实际输出大小、实际 Quality（适用时）和最终尺寸

---

# 6. 更改图片宽高比例

默认比例：

```text
4:3
```

支持：

```text
原始比例
1:1
4:3
3:4
16:9
9:16
3:2
2:3
自定义
```

自定义：

```text
宽：[5]
高：[4]
```

## 6.1 比例处理模式

比例调整采用互斥单选：

```text
○ 裁剪
○ 补边
```

同一次处理不能同时执行“裁剪 + 补边”。

### 模式 A：裁剪

支持：

```text
居中
顶部
底部
左侧
右侧
```

后续 AI 可扩展：

```text
智能主体裁剪
主体自动居中
```

### 模式 B：补边

不裁剪内容，通过增加 Canvas 达到目标比例。

背景支持：

```text
白色
黑色
透明
自定义颜色
```

---

# 7. 修改图片宽高

默认：

```text
保持原始宽高
```

用户可开启：

```text
☑ 修改图片尺寸

宽度：[1200] px
高度：[900] px

☑ 锁定宽高比例
```

支持：

```text
仅修改宽度
仅修改高度
同时指定宽高
保持比例缩放
```

处理顺序：

```text
比例处理
↓
尺寸调整
↓
文件大小压缩
```

---

# 8. 图片格式

至少支持读取：

```text
JPG
JPEG
PNG
WebP
BMP
TIFF
```

如技术条件允许，可扩展：

```text
HEIC
AVIF
```

### 8.1 HEIC / AVIF 自包含约束

HEIC / AVIF 不属于第一版 MVP 的硬性范围。

如果 Codex 决定启用 HEIC / AVIF，必须在“第一步：分析”阶段确认：

```text
1. 实际使用的 Magick.NET / ImageMagick 构建是否具备对应编解码能力
2. 所需 native delegate / codec 是否会随应用一起发布
3. 不需要用户单独安装 Windows Store 扩展或系统级 Codec
4. 对应 native 库及 codec 的许可证允许当前分发方式
5. 在一台干净的 Windows 11 测试机上可以直接解码 / 编码
```

禁止通过以下方式“伪支持”：

```text
要求用户手动安装 HEIF Image Extensions
要求用户安装第三方 codec pack
要求用户安装 ImageMagick
要求用户配置 PATH / DLL
```

如果不能满足：

```text
应用自包含
+
许可证明确
+
干净 Windows 11 可直接运行
```

则：

```text
HEIC / AVIF 保留为后续扩展
不进入 MVP
UI 中不得宣称已支持
```

输出至少支持：

```text
保持原格式
JPG
PNG
WebP
BMP
```

默认：

```text
保持原格式
```

如果启用了透明背景但用户选择 JPG：

```text
JPG 不支持透明背景，已自动切换为 PNG。
```

---

# 9. AI 智能抠图

## 9.1 核心原则

AI 抠图采用：

```text
本地 ONNX 模型
```

禁止将基础 AI 抠图做成必须依赖：

```text
OpenAI API
Claude API
Gemini API
Remove.bg API
其他在线 AI Key
```

用户无需：

```text
AI 账号
API Key
Python
GPU
```

## 9.2 AI 必须是可选组件

基础安装包不得强制包含大型 AI 模型。

基础功能：

```text
图片压缩
Resize
Crop
比例调整
补边
格式转换
批量处理
```

必须可以在完全没有 AI 模型的情况下使用。

---

# 10. AI 按需安装流程

第一次点击：

```text
AI 智能抠图
```

程序执行：

```text
检查本地模型
↓
模型不存在
↓
弹出安装提示
↓
用户确认
↓
下载模型
↓
校验
↓
安装
↓
加载
↓
开始抠图
```

不得：

```text
用户一点击就静默下载大型模型
```

必须先提示：

```text
此功能需要安装本地 AI 模型。

模型大小：xxx MB
预计磁盘占用：xxx MB
安装后支持离线使用。
图片不会上传到第三方服务器。

[安装 AI 组件] [取消]
```

安装完成以后：

```text
后续可离线运行
无需 API Key
无需再次下载
```

---

# 11. AI 模型管理

建议保存目录：

```text
%LOCALAPPDATA%\ImageToolkit\models\
```

例如：

```text
%LOCALAPPDATA%\ImageToolkit\
├── config.json
├── logs\
└── models\
    └── background-removal\
        ├── model.onnx
        ├── manifest.json
        └── checksum.json
```

## 11.1 模型状态判断

不要仅靠：

```json
"installed": true
```

判断模型已安装。

应检查：

```text
模型目录
+
manifest
+
model.onnx
+
SHA-256
```

确保模型真正完整可用。

## 11.2 模型管理接口

建议：

```text
IAiModelManager
├── IsInstalledAsync()
├── DownloadAsync()
├── VerifyAsync()
├── InstallAsync()
├── CheckUpdateAsync()
├── UpdateAsync()
└── UninstallAsync()
```

职责：

```text
下载
校验
安装
版本
更新
删除
```

禁止把模型下载逻辑直接写进 UI。

---

# 12. AI 推理解耦

AI 模型不得和 `BackgroundRemovalService` 强绑定。

错误设计：

```text
BackgroundRemovalService
↓
写死某个模型
↓
写死输入尺寸
↓
写死 mean/std
```

正确设计：

```text
BackgroundRemovalService
        ↓
IBackgroundRemovalEngine
        ↓
IBackgroundRemovalModelAdapter
        ↓
具体模型 Adapter
```

建议：

```text
IBackgroundRemovalModelAdapter
├── PreProcess()
├── RunInference()
├── PostProcess()
└── BuildMask()
```

不同模型可拥有自己的：

```text
输入尺寸
mean/std
预处理
后处理
输出张量解析
```

未来更换模型时：

```text
新增 Adapter
```

而不是重写整个业务服务。

---

# 13. AI 模型 Manifest

每个模型应包含描述文件，例如：

```json
{
  "id": "background-removal-default",
  "name": "Background Removal",
  "version": "1.0.0",
  "file": "model.onnx",
  "sha256": "xxx",
  "downloadUrl": "https://...",
  "license": "xxx",
  "inputWidth": 1024,
  "inputHeight": 1024,
  "runtime": "onnx"
}
```

允许 Adapter 中保存更复杂的预处理参数。

---

# 14. AI 模型选型要求

Codex 第一步必须对默认抠图模型给出明确评估：

```text
模型名称
模型来源
模型权重来源
模型大小
ONNX 输入尺寸
预处理参数
推理速度
CPU 可用性
效果
许可证
是否允许重新分发
是否允许商业使用
```

原则：

```text
许可证明确
>
效果
>
模型体积
>
新颖程度
```

不要默认使用许可证不清晰的权重。

如果模型许可证无法确认：

```text
不得直接打包或自动下载上线
```

需要选择许可更清晰的替代模型。

第一版只需要实现一个稳定默认模型，但架构必须支持后续增加：

```text
轻量模型
高质量模型
人物专用模型
商品专用模型
```

---

# 15. AI 抠图结果

支持：

## 15.1 透明背景

输出：

```text
PNG
WebP
```

必须保留 Alpha Channel。

## 15.2 纯色背景

支持：

```text
白色
黑色
红色
蓝色
自定义颜色
```

颜色选择器：

```text
背景颜色：[ ■ #FFFFFF ]
```

## 15.3 保留原背景

可作为后续扩展，不阻塞第一版。

---

# 16. 文件选择

支持：

```text
单张图片
多张图片
整个文件夹
拖拽文件
拖拽文件夹
```

多选支持系统原生：

```text
Ctrl
Shift
```

文件夹支持：

```text
☑ 包含子文件夹
```

---

# 17. 输出位置

支持三种模式。

## 模式 1：原目录生成新文件

默认：

```text
原文件：
photo.jpg

输出：
photo-已处理.jpg
```

## 模式 2：覆盖原文件

开启时全局确认一次：

```text
覆盖原文件后可能无法恢复。
建议提前备份。

是否继续？
```

批量任务不需要逐张确认。

### 覆盖安全要求

禁止直接打开原文件流并覆写。

正确流程：

```text
生成临时文件
↓
校验写入成功
↓
原子替换原文件
```

例如：

```text
photo.jpg
photo.jpg.tmp
```

只有新文件成功完成后才替换原文件。

任何单张处理失败：

```text
原文件必须保持完整
```

## 模式 3：指定目录

例如：

```text
D:\Pictures\Processed
```

---

# 18. 文件命名

默认：

```text
-已处理
```

示例：

```text
abc.jpg
↓
abc-已处理.jpg
```

支持用户自定义：

```text
-已处理
-compressed
-new
```

需要处理同名冲突，例如：

```text
abc-已处理.jpg
abc-已处理-2.jpg
```

不得静默覆盖非目标文件。

---

# 19. 批量处理

列表至少显示：

| 文件 | 尺寸 | 大小 | 状态 |
|---|---:|---:|---|
| a.jpg | 4000×3000 | 5.2MB | 等待 |
| b.png | 1920×1080 | 2.1MB | 等待 |

状态至少：

```text
等待
处理中
已完成
未达标
失败
已取消
```

显示：

```text
已完成：80 / 100
失败：2
未达标：3
```

支持：

```text
开始处理
暂停
取消
失败重试
```

单张失败不得中止整个批量任务。

---

# 20. 图片处理流水线

逻辑顺序：

```text
读取文件
↓
Decode
↓
处理 EXIF Orientation
↓
AI 抠图（如果开启）
↓
比例处理
↓
尺寸调整
↓
背景处理
↓
选择最终输出 Encoder
↓
寻找最终压缩参数
↓
Encode 一次
↓
安全写入文件
```

关键要求：

```text
Decode 一次
像素处理一次链路
最终 Encode 一次
```

不要：

```text
先输出 JPG
↓
再次读取
↓
再次压缩
↓
再次输出
```

避免重复有损编码造成质量下降。

“格式转换”在流程中主要表示：

```text
选择最终 Encoder
```

而不是提前生成中间文件。

---

# 21. EXIF 与 Metadata

必须正确处理：

```text
EXIF Orientation
```

像素旋正后：

```text
Orientation = 1
```

避免二次旋转。

默认策略：

```text
保留普通 EXIF
默认删除 GPS 位置信息
```

例如：

```text
拍摄时间：保留
相机型号：保留
GPS：默认删除
```

设置页面可提供：

```text
☐ 保留 GPS 位置信息
```

默认关闭。

后续扩展：

```text
一键清除全部 Metadata
```

## 21.1 ICC Color Profile / 色彩管理

必须显式处理 ICC Color Profile，避免压缩或格式转换后出现明显偏色、发灰、发暗。

默认策略：

```text
源图包含 ICC Profile
↓
目标格式支持嵌入 ICC
↓
默认保留原 ICC Profile
```

以下处理不得无意丢失 ICC Profile：

```text
Resize
Crop
补边
JPEG / WebP 压缩
PNG 优化 / 量化
格式转换
AI 抠图后的最终合成
```

如果目标格式或具体编码路径无法可靠保留原 ICC Profile：

```text
必须先根据源 ICC Profile 将像素正确转换到 sRGB
↓
再使用 sRGB 输出
```

不得：

```text
直接删除 Adobe RGB / Display P3 等 Profile
但保持原像素数值不变
```

否则可能造成明显颜色变化。

默认行为：

```text
ICC Profile：保留
GPS：删除
普通 EXIF：保留
```

用户选择“一键清除全部 Metadata”时：

- EXIF / XMP 等可移除
- ICC 不应简单视为普通隐私 Metadata 直接丢弃
- 若用户要求去除 ICC，应先转换到 sRGB，再移除 Profile

Codex 必须为以下情况增加测试图片或自动化验证：

```text
sRGB
Adobe RGB
Display P3（如测试素材可获得）
无 ICC Profile
```

至少确保处理前后不存在明显的非预期色彩偏移。

---

# 22. 图片预览

选择图片后显示：

```text
原图预览 | 处理后预览
```

调整：

```text
比例
尺寸
裁剪
补边
背景
AI 抠图
```

时尽量刷新处理效果。

要求：

- 预览使用缩略图 / 降采样版本
- 不为批量列表所有图片同时生成高分辨率预览
- 不因预览导致大内存占用
- AI 预览可增加短暂 loading 状态

---

# 23. UI 要求

界面风格参考：

```text
Canva
现代 Windows 11 桌面应用
```

避免传统老旧工具软件风格。

## 23.1 高 DPI / 多显示器缩放适配

Windows 11 常见：

```text
100%
125%
150%
175%
200%
```

缩放比例，因此 UI 必须按高 DPI 桌面应用设计。

要求：

```text
app.manifest：
PerMonitorV2
```

至少需要：

- 启用 Per-Monitor DPI Awareness V2
- 使用 WPF 的 DIP 布局，不使用大量依赖固定物理像素的定位方式
- 图标优先使用矢量资源或高 DPI 友好的资源
- 字体和控件在 125% / 150% / 200% 下不得截断
- 弹窗、下拉框、文件列表、预览区域不得明显错位
- 从一个 DPI 比例显示器拖到另一个 DPI 比例显示器时，窗口应正常重新布局
- 图片预览区域的 UI 不得因 DPI 缩放而模糊或拉伸错误

MVP 验收至少测试：

```text
100%
125%
150%
200%
```

如有多显示器条件，增加：

```text
100% 显示器 ↔ 150% / 200% 显示器
```

跨屏拖动验证。

推荐布局：

```text
┌─────────────────────────────────────────────┐
│ Image Toolkit                               │
├────────────┬────────────────────────────────┤
│ 文件       │                                │
│ 尺寸       │        图片预览                │
│ 比例       │                                │
│ 裁剪       │                                │
│ 抠图       │                                │
│ 压缩       │                                │
│ 输出       │                                │
├────────────┴────────────────────────────────┤
│              开始处理                       │
└─────────────────────────────────────────────┘
```

---

# 24. 外观配置

支持：

```text
浅色模式
深色模式
跟随系统
```

工作区背景：

```text
系统默认
白色
浅灰
深灰
黑色
自定义
```

字体大小统一使用档位：

```text
小     = 12
标准   = 14（默认）
大     = 16
超大   = 18
```

UI 展示：

```text
小
标准
大
超大
```

内部保存对应数值。

设置修改后立即生效并持久化。

---

# 25. 配置持久化

建议：

```text
%LOCALAPPDATA%\ImageToolkit\config.json
```

保存：

```text
目标文件大小
默认比例
尺寸
输出格式
输出路径
文件名后缀
主题
背景色
字体大小
默认 AI 模型
AI 更新偏好
GPS 保留设置
```

不要把 AI 模型是否真实安装仅写死在配置中。

模型是否可用：

```text
以文件 + manifest + checksum 实际检查结果为准
```

---

# 26. 性能要求

批量处理：

```text
500～1000 张
```

时要求：

- UI 不得卡死
- 图片处理放后台任务
- 支持取消
- 支持进度通知
- 控制并发
- 不一次加载全部原始图片
- 单张处理完成后及时释放资源
- 稳定优先于极限并发

并发数可配置，例如：

```text
自动
1
2
4
```

默认：

```text
自动
```

---

# 27. 日志

目录：

```text
%LOCALAPPDATA%\ImageToolkit\logs\
```

记录：

```text
程序启动
异常
图片读取失败
图片保存失败
压缩未达标
AI 模型下载失败
AI 模型校验失败
AI 模型加载失败
AI 推理失败
```

不得记录：

```text
用户图片内容
完整图片二进制
敏感 Metadata
```

---

# 28. 推荐工程结构

```text
ImageToolkit
│
├── ImageToolkit.App
│   ├── Views
│   ├── ViewModels
│   ├── Controls
│   └── Resources
│
├── ImageToolkit.Application
│   ├── ImageProcessing
│   ├── Batch
│   ├── Compression
│   ├── Resize
│   ├── Crop
│   ├── Background
│   └── AI
│
├── ImageToolkit.Domain
│   ├── Models
│   ├── Enums
│   ├── Results
│   └── Interfaces
│
├── ImageToolkit.Infrastructure
│   ├── Imaging
│   ├── ONNX
│   ├── Download
│   ├── Storage
│   ├── Config
│   └── Logging
│
└── ImageToolkit.Tests
```

禁止：

```text
MainWindow.xaml.cs
```

承载核心业务逻辑。

---

# 29. AI 推荐工程结构

```text
Application/AI/
├── BackgroundRemovalService.cs
└── AiModelManager.cs

Domain/Interfaces/
├── IAiModelManager.cs
├── IBackgroundRemovalEngine.cs
└── IBackgroundRemovalModelAdapter.cs

Infrastructure/ONNX/
├── OnnxBackgroundRemovalEngine.cs
├── ModelManifest.cs
└── Adapters/
    ├── DefaultBackgroundRemovalAdapter.cs
    └── FutureModelAdapter.cs
```

这样未来替换 AI 模型时：

```text
新增 Adapter
```

而不是重写整个应用。

---

# 30. Windows 发布要求

固定：

```text
win-x64
Self-contained
完整发布目录
安装包
```

不追求：

```text
PublishSingleFile
```

原因：

- ONNX Runtime 包含 Native DLL
- DirectML 可能包含额外 DLL
- 单文件会增加 Native 解包与维护复杂度
- 更容易增加杀毒软件误报风险
- 对普通用户没有明显必要

建议：

```text
dotnet publish
+
Inno Setup
```

最终：

```text
ImageToolkitSetup.exe
```

安装后用户通过：

```text
开始菜单
桌面快捷方式
```

启动程序。

## 30.1 CPU 架构支持边界

第一版官方支持：

```text
Windows 11 x64
win-x64
```

这是 MVP 的正式验收平台。

对于 Windows 11 ARM64：

```text
第一版不承诺 ARM64 原生运行
```

由于当前图片处理和 ONNX 等依赖可能包含 x64 native library，ARM64 设备可能通过 Windows 11 的 x64 模拟层运行，但：

```text
在未经实际测试前不得将 ARM64 标记为“官方支持”
```

README 和 Release Notes 应明确：

```text
官方支持：Windows 11 x64
ARM64：实验性 / 未验证（如实际可运行）
```

后续如要正式支持 ARM64，需要单独评估：

```text
win-arm64
Magick.NET ARM64 对应包
ONNX Runtime ARM64
AI 推理 Provider
安装包架构检测
完整回归测试
```

---

# 31. Windows 代码签名预留

第一版内部测试可以不购买代码签名证书。

但项目必须为未来：

```text
Authenticode Code Signing
```

预留发布流程。

正式公开发布时应考虑：

```text
EXE 签名
安装包签名
```

减少：

```text
未知发布者
SmartScreen 警告
```

CI / 发布脚本不要设计成无法加入签名步骤。

---

# 32. AI 设置页面

```text
AI 组件

智能抠图模型：
已安装 / 未安装

版本：
1.0.0

磁盘占用：
xxx MB

[安装]
[检查更新]
[重新下载]
[删除 AI 组件]
```

删除模型后：

```text
基础图片处理功能必须继续正常工作
```

---

# 33. AI 后续扩展

整个 AI 架构必须支持未来加入：

```text
AI 智能抠图
AI 超分辨率
AI 图片降噪
AI 人脸增强
AI 智能裁剪
AI 主体自动居中
```

模型目录：

```text
models/
├── background-removal/
├── super-resolution/
├── denoise/
└── face-enhancement/
```

全部：

```text
按需安装
```

---

# 34. 第一阶段 MVP

第一阶段优先完成非 AI 功能：

```text
1. 单图选择
2. 多图选择
3. 文件夹选择
4. 拖拽导入
5. 批量处理
6. 修改尺寸
7. 修改宽高比例
8. 裁剪
9. 补边
10. JPG / PNG / WebP
11. 格式转换
12. 文件大小限制，默认 ≤1MB
13. 原目录输出
14. 指定目录输出
15. 覆盖原文件
16. 原文件名-已处理
17. 图片预览
18. 处理进度
19. 失败 / 未达标状态
20. 配置持久化
21. 浅色 / 深色 / 跟随系统
22. 背景颜色配置
23. 字体大小配置
24. EXIF Orientation
25. 默认移除 GPS
26. 原文件安全替换
```

---

# 35. 第二阶段 AI

实现：

```text
1. AI 智能抠图
2. 透明背景
3. 纯色背景替换
4. AI 模型按需安装
5. 模型下载
6. SHA-256 校验
7. 模型版本管理
8. 模型删除
9. 本地离线推理
10. CPU 运行
11. 可插拔 Model Adapter
```

AI 组件不得成为基础软件启动和使用的强依赖。

---

# 36. 后续扩展

后续可以加入：

```text
AI 超分辨率
AI 降噪
AI 人脸增强
智能裁剪
主体自动居中
水印
圆角
旋转
镜像
批量重命名
Metadata 全清除
证件照背景替换
```

---

# 37. 与现有连点器项目关系

我已经有自己开发完成的连点器项目。

本项目可以参考现有连点器的：

```text
Windows 桌面应用形态
配置化思路
UI 交互
```

但：

```text
image-toolkit
```

必须作为独立、模块化项目开发。

未来可以整合为 Windows 工具箱，但当前不做强耦合。

---

# 38. Codex 开发顺序

Codex 必须按以下顺序执行。

## 第一步：分析

先完整阅读本文档。

输出：

```text
1. 最终技术架构
2. .NET 10 + WPF 项目结构
3. MVVM 实现方式
4. Magick.NET 使用方案
5. 第三方许可证检查
6. AI 默认模型候选
7. AI 模型许可证检查
8. ONNX Runtime 实现方案
9. Model Adapter 设计
10. 模型下载 / 校验 / 版本方案
11. Inno Setup 打包方案
12. HEIC / AVIF 是否满足自包含发布的核实结果
13. ICC Profile / 色彩管理实现方案
14. JPEG / WebP 最低 Quality 与自动降分辨率下限实现方案
15. PerMonitorV2 / 高 DPI 适配方案
16. x64 / ARM64 支持边界
17. 开发阶段计划
18. 风险项
```

在第一步未完成之前：

```text
不要直接大规模写 UI 和业务代码
```

## 第二步：创建工程骨架

建立完整 Solution 和项目结构。

不要只创建 Demo。

## 第三步：实现基础 MVP

优先实现：

```text
非 AI 图片处理
```

## 第四步：实现测试

至少测试：

```text
Resize
Crop
比例
文件命名
文件大小约束
JPEG / WebP 最低 Quality 下限
自动降分辨率下限
PNG / JPEG / WebP
压缩无法达标时返回“未达标”
覆盖原文件安全逻辑
EXIF Orientation
GPS Metadata 处理
ICC Profile 保留 / sRGB 转换
```

UI / 集成测试至少覆盖：

```text
100% DPI
125% DPI
150% DPI
200% DPI
```

如有条件，验证不同 DPI 显示器之间拖动窗口。

如果实现 HEIC / AVIF：

```text
必须在没有额外 codec / ImageMagick / 开发环境的干净 Windows 11 上验证
```

## 第五步：实现 AI 模块

采用：

```text
用户首次点击
↓
提示
↓
确认
↓
下载
↓
校验
↓
安装
↓
本地运行
```

## 第六步：Build

必须执行：

```powershell
dotnet build
```

并修复全部编译错误。

## 第七步：Test

必须执行：

```powershell
dotnet test
```

核心测试不得长期保持失败状态。

## 第八步：Publish

完成：

```text
win-x64
self-contained
Release
```

## 第九步：安装包

生成 Inno Setup 安装包。

## 第十步：README

更新：

```text
README.md
```

包含：

```text
项目介绍
主要功能
截图占位
普通用户安装方法
开发环境
Build
Test
Publish
安装包生成
AI 模型安装机制
隐私说明
许可证说明
```

---

# 39. Codex 执行权限

开发过程中可以直接：

```text
创建文件
修改文件
删除无用文件
安装 NuGet 依赖
运行 Restore
运行 Build
运行 Test
运行 Publish
修复错误
```

不需要每一步询问。

存在多个方案时优先级：

```text
稳定
>
易维护
>
数据安全
>
Windows 11 用户体验
>
性能
>
技术新颖程度
```

不要为了使用新技术增加部署复杂度。

---

# 40. 隐私要求

基础图片：

```text
全部本地处理
```

AI：

```text
模型下载可以联网
图片推理必须本地执行
```

不得上传用户图片到模型下载服务器或其他第三方服务。

UI 可显示：

```text
图片处理在本机完成。
AI 模型安装后支持离线运行。
```

---

# 41. 最终验收环境

测试机：

```text
Windows 11 x64

没有 Python
没有 Node.js
没有 Visual Studio
没有 .NET SDK
没有 .NET Runtime
没有 AI API Key
没有额外安装图片库
没有额外安装 ImageMagick
没有额外安装 HEIC / AVIF Codec（若宣称支持对应格式）
```

显示缩放至少分别验证：

```text
100%
125%
150%
200%
```

安装：

```text
运行 ImageToolkitSetup.exe
↓
安装
↓
启动 Image Toolkit
```

---

# 42. 最终基础功能验收

用户：

```text
打开 Image Toolkit
↓
选择单张 / 多张 / 文件夹
↓
设置 ≤1MB
↓
设置 4:3
↓
选择裁剪或补边
↓
可选设置 1200×900
↓
选择 PNG / JPG / WebP
↓
选择输出目录
↓
开始处理
↓
获得最终图片
```

要求：

```text
不需要命令行
不需要编辑配置
不需要开发环境
```

---

# 43. AI 功能验收

首次：

```text
点击 AI 智能抠图
↓
检测模型不存在
↓
展示模型大小 / 隐私 / 离线说明
↓
用户点击安装
↓
下载模型
↓
SHA-256 校验
↓
安装
↓
执行本地 AI 抠图
↓
得到透明 PNG
```

以后：

```text
无需 API Key
无需重新下载
支持离线运行
```

删除 AI 模型后：

```text
基础图片功能继续正常使用
```

---

# 44. 数据安全验收

必须验证：

```text
覆盖原文件时程序异常
↓
原图不损坏
```

批量中某张处理失败：

```text
其他图片继续
+
失败图片原文件完整
```

用户指定尺寸：

```text
1200×900
```

且压缩无法达到目标：

```text
不得偷偷改尺寸
必须显示未达标
```

压缩触及：

```text
最低 Quality
或
最低自动分辨率
```

仍无法达到目标时：

```text
不得继续无底线降质
必须显示未达标
```

带 ICC Profile 的图片处理后：

```text
不得因 Profile 丢失造成明显非预期偏色
```

---

# 45. v4 新增硬性要求摘要

本版本新增以下硬性要求，Codex 不得遗漏：

```text
1. JPEG / WebP 默认最低 Quality = 45
2. 自动降分辨率存在下限，默认不得低于压缩阶段尺寸的 25%，短边不得低于 320px
3. 达到质量 / 分辨率下限仍无法满足目标时，返回“未达标”
4. 默认保留 ICC Color Profile
5. 无法保留 ICC 时，先正确转换到 sRGB 再输出
6. HEIC / AVIF 只有在可以完全自包含分发时才能进入支持列表
7. 不允许要求用户安装系统 Codec / ImageMagick / 第三方编解码包
8. WPF 启用 PerMonitorV2
9. UI 验收覆盖 100% / 125% / 150% / 200% DPI
10. 第一版官方平台为 Windows 11 x64
11. ARM64 不作为第一版原生支持承诺
```

---

# 46. 最终交付标准

最终产物不是：

```text
Demo
UI 原型
只能在开发电脑运行的工程
```

而是：

```text
可编译
可测试
可发布
可安装
普通 Windows 11 用户可直接使用
```

的完整桌面应用。

Codex 在实现过程中必须持续以这一交付目标为准。
