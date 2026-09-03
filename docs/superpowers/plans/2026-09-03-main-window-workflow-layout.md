# 苏影枢主操作页工作流布局 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将主窗口改为左侧批次管理、中间预览与队列、右侧分类参数、底部唯一主操作的工作流布局。

**Architecture:** 保留现有 WPF MVVM 和全部业务调用链，仅在 `MainWindow.xaml` 重组视觉层级，并在 `MainWindowViewModel` 增加可测试的主操作显示状态。发布脚本继续负责自包含构建和 Inno Setup 打包。

**Tech Stack:** C#、.NET 10、WPF、CommunityToolkit.Mvvm、xUnit、PowerShell、Inno Setup

---

### Task 1: 固化主操作状态

**Files:**
- Modify: `tests/ImageToolkit.App.Tests/MainWindowViewModelTests.cs`
- Modify: `src/ImageToolkit.App/ViewModels/MainWindowViewModel.cs`

- [x] 增加测试：初始状态显示“开始处理”，完成批次后显示“新建批次”，执行新建批次后恢复。
- [x] 运行定向测试并确认测试因属性不存在而失败。
- [x] 增加 `ShowStartAction` 和 `ShowNewBatchAction` 只读属性，并在 `HasCompletedBatch` 变化时通知。
- [x] 重新运行定向测试并确认通过。

### Task 2: 重组主窗口布局

**Files:**
- Modify: `src/ImageToolkit.App/MainWindow.xaml`
- Modify: `src/ImageToolkit.App/Resources/Controls.xaml`

- [x] 左侧移除静态流程说明，改为队列摘要、移除和清空。
- [x] 顶部保留导入操作，并将“包含子文件夹”移动到添加文件夹旁。
- [x] 中间预览改为“处理后/左右对比”页签，下方保留文件队列。
- [x] 右侧将现有设置控件原样迁移到五个分类页签。
- [x] 底部使用状态属性控制“开始处理/新建批次”互斥显示。
- [x] 批处理期间禁用右侧参数面板。
- [x] 补充 TabControl/TabItem 样式，保持浅色和深色资源兼容。

### Task 3: 编译与自动化回归

**Files:**
- Verify: `ImageToolkit.sln`

- [x] 执行 `scripts/build.ps1`，确认 Release 构建 0 错误。
- [x] 执行 `scripts/test.ps1`，确认自动化测试无失败。
- [x] 不执行人工鼠标交互和目视验证。

### Task 4: 本地发布和交付

**Files:**
- Run: `scripts/publish.ps1`
- Run: `scripts/package.ps1`
- Update: `D:\专用工具\图批处理\交付产品`
- Modify: `D:\专用工具\图批处理\进度.md`

- [x] 生成 win-x64 自包含发布目录。
- [x] 生成 v1.1.0 安装包。
- [x] 更新交付目录中的安装包和产品文档。
- [x] 计算并记录交付文件 SHA256。
- [x] 在 `进度.md` 记录修改文件、兼容性、验证结果、风险和用户验收事项。
- [x] 不执行 GitHub 提交、推送或 Release 操作。
