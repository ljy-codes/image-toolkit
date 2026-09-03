# 苏影枢配置导入导出与品牌更名实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为“苏影枢”增加版本化完整配置包导入导出能力，并统一所有用户可见产品名称。

**Architecture:** 使用单个 `.syconfig` JSON 文件保存格式版本、产品标识、导出时间、当前应用配置和命名预设。文件服务负责序列化及结构校验，主窗口负责业务校验、失效路径修正、确认覆盖、双存储回滚和即时应用；内部程序集、可执行文件及 `%LOCALAPPDATA%\ImageToolkit` 数据目录保持不变。

**Tech Stack:** .NET 8、WPF、CommunityToolkit.Mvvm、System.Text.Json、xUnit

---

### Task 1: 配置包格式与文件服务

**Files:**
- Create: `src/ImageToolkit.Infrastructure/Config/ConfigurationPackage.cs`
- Create: `src/ImageToolkit.Infrastructure/Config/JsonConfigurationPackageService.cs`
- Create: `tests/ImageToolkit.Infrastructure.Tests/JsonConfigurationPackageServiceTests.cs`

- [x] **Step 1: Write the failing tests**

覆盖完整往返、错误产品标识、不支持版本、损坏 JSON、空配置和空预设集合。

- [x] **Step 2: Run tests to verify RED**

Run: `dotnet test tests/ImageToolkit.Infrastructure.Tests/ImageToolkit.Infrastructure.Tests.csproj --filter JsonConfigurationPackageServiceTests`

Expected: FAIL because configuration package types do not exist.

- [x] **Step 3: Implement minimal package service**

使用 camelCase、字符串枚举和缩进 JSON；导出通过临时文件原子替换，导入先完整反序列化并验证，不修改本机持久化配置。

- [x] **Step 4: Run tests to verify GREEN**

Run: `dotnet test tests/ImageToolkit.Infrastructure.Tests/ImageToolkit.Infrastructure.Tests.csproj --filter JsonConfigurationPackageServiceTests`

Expected: PASS.

### Task 2: 主窗口导入导出工作流

**Files:**
- Modify: `src/ImageToolkit.App/Services/DesktopFilePicker.cs`
- Modify: `src/ImageToolkit.App/ViewModels/ProcessingPresetViewModel.cs`
- Modify: `src/ImageToolkit.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ImageToolkit.App/App.xaml.cs`
- Modify: `tests/ImageToolkit.App.Tests/MainWindowViewModelTests.cs`
- Modify: `tests/ImageToolkit.App.Tests/ProcessingPresetViewModelTests.cs`

- [x] **Step 1: Write the failing tests**

覆盖导出完整配置、导入即时应用、指定目录失效时恢复默认、取消覆盖不修改配置、第二个存储失败时回滚。

- [x] **Step 2: Run tests to verify RED**

Run: `dotnet test tests/ImageToolkit.App.Tests/ImageToolkit.App.Tests.csproj --filter "Configuration|Imported"`

Expected: FAIL because commands and picker APIs do not exist.

- [x] **Step 3: Implement minimal workflow**

导入前完成结构与处理参数校验；用户确认后依次保存配置和预设，任一步失败时恢复快照；成功后刷新当前参数、外观和预设，并显示路径修正数量及原因。

- [x] **Step 4: Run tests to verify GREEN**

Run: `dotnet test tests/ImageToolkit.App.Tests/ImageToolkit.App.Tests.csproj --filter "Configuration|Imported"`

Expected: PASS.

### Task 3: WPF 操作入口与“苏影枢”品牌

**Files:**
- Modify: `src/ImageToolkit.App/MainWindow.xaml`
- Modify: `src/ImageToolkit.App/Resources/Strings.zh-CN.xaml`
- Modify: `src/ImageToolkit.App/Services/UserDialogService.cs`
- Modify: `src/ImageToolkit.App/App.xaml.cs`
- Modify: `installer/ImageToolkit.iss`
- Modify: `README.md`
- Modify: `CHANGELOG.md`

- [x] **Step 1: Add import/export controls**

在参数方案区域增加“导入配置”和“导出配置”按钮，运行批任务时禁用，操作结果在页面中清晰显示。

- [x] **Step 2: Rename user-visible product text**

将窗口、侧栏、消息框、安装器显示名称、快捷方式及当前产品文档中的“图批处理”统一为“苏影枢”；技术路径和代码标识保持兼容。

- [x] **Step 3: Build to validate XAML and dependency injection**

Run: `dotnet build ImageToolkit.sln -c Release`

Expected: PASS with 0 warnings and 0 errors.

### Task 4: 验收与问题记录

**Files:**
- Modify: `docs/releases/v1.1.0-issue-tracking.md`
- Modify: `docs/releases/v1.1.0-test-report.md`

- [x] **Step 1: Run complete automated verification**

Run: `dotnet test ImageToolkit.sln -c Release --no-build`

Expected: all tests pass.

- [x] **Step 2: Run targeted acceptance and real-image suites**

运行现有验收、真实图片及 AI 冒烟脚本，记录实际通过数量和限制。

- [x] **Step 3: Record conclusions and remaining issues**

新增配置导入导出、品牌更名验收项，记录自动验证结论、未执行的人工 UI 场景和剩余风险。

- [x] **Step 4: Confirm repository boundaries**

确认未生成安装包、未复制正式交付物、未提交、未推送、未创建 GitHub Release。
