# Local AI Model Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在保留在线模型下载的同时，增加经过大小和 SHA256 校验的本地 `.onnx` 模型导入能力。

**Architecture:** `IAiModelManager` 和 `LocalAiModelManager` 负责识别、校验和原子安装；`IDesktopFilePicker` 负责选择模型文件；`AiBackgroundRemovalViewModel` 负责确认、状态和取消；XAML 只增加一个全宽入口。

**Tech Stack:** .NET 10、WPF、CommunityToolkit.Mvvm、xUnit、Inno Setup

---

### Task 1: 模型管理器本地导入

**Files:**
- Modify: `src/ImageToolkit.Domain/Interfaces/IAiModelManager.cs`
- Modify: `src/ImageToolkit.Infrastructure.AI/LocalAiModelManager.cs`
- Modify: `tests/ImageToolkit.Infrastructure.Tests/LocalAiModelManagerTests.cs`

- [x] 编写失败测试：有效模型可按内容识别并导入。
- [x] 编写失败测试：错误大小或 SHA256 被拒绝，已有模型不被覆盖。
- [x] 在接口增加本地模型识别和导入方法。
- [x] 复用模型清单执行大小、SHA256 校验和临时文件原子替换。
- [x] 运行 `dotnet test tests/ImageToolkit.Infrastructure.Tests -c Release --no-restore --filter LocalAiModelManagerTests`，全部通过。

### Task 2: 文件选择和 ViewModel 流程

**Files:**
- Modify: `src/ImageToolkit.App/Services/DesktopFilePicker.cs`
- Modify: `src/ImageToolkit.App/ViewModels/AiBackgroundRemovalViewModel.cs`
- Modify: `tests/ImageToolkit.App.Tests/AiBackgroundRemovalViewModelTests.cs`
- Modify: interface fakes in existing test projects

- [x] 编写失败测试：取消文件选择时不调用模型管理器。
- [x] 编写失败测试：识别模型后确认安装或替换，并刷新状态。
- [x] 增加 `PickAiModelPathAsync`，过滤 `.onnx`。
- [x] 增加 `ImportLocalModelCommand`，共用忙碌状态、进度和取消令牌。
- [x] 将取消提示改为“模型操作”。
- [x] 运行 App 定向测试，全部通过。

### Task 3: 界面入口

**Files:**
- Modify: `src/ImageToolkit.App/MainWindow.xaml`
- Modify: `tests/ImageToolkit.App.Tests/MainWindowLayoutTests.cs`

- [x] 编写失败测试：AI 区存在本地导入按钮和通用取消文案。
- [x] 在两个模型操作区之后增加全宽“从本地文件导入模型”按钮。
- [x] 运行布局测试，已通过。

### Task 4: 文档、构建和交付

**Files:**
- Modify: `README.md`
- Modify: `交付产品/安装说明.html`
- Modify: `进度.md`
- Replace: `交付产品/安装说明.pdf`
- Replace: `交付产品/苏影枢-安装包.exe`

- [x] 更新在线下载与本地导入说明。
- [x] 运行 Release 全量测试和 `git diff --check`。
- [x] 重新执行 `scripts/package.ps1` 生成 v1.1.0 安装包。
- [x] 更新 HTML/PDF 内安装包 SHA256，并逐页检查 PDF。
- [x] 复制安装包到交付目录，核对构建产物与交付文件 SHA256 一致。
- [x] 更新 `进度.md`，记录修改、兼容性、验证、风险和未执行人工验收。
- [x] 按用户要求不提交、不推送、不更新 GitHub Release。
