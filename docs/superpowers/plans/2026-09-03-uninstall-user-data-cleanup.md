# Uninstall User Data Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 卸载苏影枢时默认保留用户数据，并允许用户明确确认后彻底删除 AI 模型、配置、参数方案和日志。

**Architecture:** 使用 Inno Setup 的卸载事件记录用户选择，在 `usPostUninstall` 阶段条件调用 `DelTree`。通过源码级 xUnit 测试约束默认按钮、目标目录和条件删除，避免后续安装脚本回退为无提示清理。

**Tech Stack:** Inno Setup Pascal Script、xUnit、PowerShell

---

### Task 1: 添加卸载脚本约束测试

**Files:**
- Create: `tests/ImageToolkit.App.Tests/InstallerScriptTests.cs`

- [ ] 编写测试，验证脚本包含确认消息、`MB_DEFBUTTON2`、`{localappdata}\ImageToolkit` 和条件 `DelTree`。
- [ ] 运行测试并确认因功能尚未实现而失败。

### Task 2: 实现可选用户数据清理

**Files:**
- Modify: `installer/ImageToolkit.iss`

- [ ] 在卸载开始阶段询问是否删除用户数据，并将默认按钮设为“否”。
- [ ] 在 `usPostUninstall` 阶段仅按确认结果删除固定用户数据目录。
- [ ] 删除失败时显示警告但不阻断卸载。
- [ ] 运行脚本约束测试并确认通过。

### Task 3: 更新文档与交付物

**Files:**
- Modify: `README.md`
- Modify: `交付产品/安装说明.html`
- Modify: `进度.md`

- [ ] 更新卸载说明，明确默认保留和确认后删除范围。
- [ ] 运行完整 Release 构建与测试。
- [ ] 生成 win-x64 自包含安装包并复制到交付目录。
- [ ] 校验构建产物与交付安装包 SHA256 一致。
- [ ] 更新进度和交付清单，不执行 GitHub 同步。
