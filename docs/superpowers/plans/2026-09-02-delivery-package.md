# 图片批处理工具正式交付包 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 生成只包含安装包、产品介绍 HTML/PDF、安装说明 HTML/PDF 的正式交付目录。

**Architecture:** 使用一个临时 Python 生成器维护两份文档的结构化内容，输出自包含 HTML，并通过 Edge 无头打印生成同版 PDF。界面配图优先捕获实际 WPF 主窗口，捕获失败时生成与真实布局一致且明确标注的界面示意图。所有临时脚本、截图和 PDF 渲染页存放在 `tmp/delivery/`，验证通过后删除。

**Tech Stack:** PowerShell、Python、HTML/CSS、Microsoft Edge headless、Poppler、Pillow。

---

### Task 1: 准备交付工作区和界面配图

**Files:**
- Create: `tmp/delivery/capture-window.ps1`
- Create: `tmp/delivery/app-interface.png`

- [ ] **Step 1: 校验安装包来源**

Run:

```powershell
Get-FileHash artifacts\installer\ImageToolkitSetup.exe -Algorithm SHA256
```

Expected: 文件存在并输出 SHA256。

- [ ] **Step 2: 捕获实际主窗口**

创建 `capture-window.ps1`，启动 `artifacts/publish/win-x64/ImageToolkit.App.exe`，等待主窗口响应，通过 Win32 `PrintWindow` 将客户区保存为 `app-interface.png`，随后关闭程序。

- [ ] **Step 3: 验证配图**

Run:

```powershell
Get-Item tmp\delivery\app-interface.png
```

Expected: PNG 尺寸不小于 1024x680，文件非空。若实际窗口捕获不可用，生成带“界面示意”标注且结构与 `MainWindow.xaml` 一致的位图。

### Task 2: 生成正式 HTML

**Files:**
- Create: `tmp/delivery/generate_delivery.py`
- Create: `图片批处理工具/产品介绍.html`
- Create: `图片批处理工具/安装说明.html`

- [ ] **Step 1: 编写单文件文档生成器**

生成器读取界面 PNG 并转为 Base64 data URI。产品介绍包含定位、界面、核心功能、三步工作流、隐私和支持范围；安装说明包含安装、首次启动、卸载、SmartScreen 和 SHA256。

- [ ] **Step 2: 生成 HTML**

Run:

```powershell
python tmp\delivery\generate_delivery.py
```

Expected: 两份 HTML 均存在，且不包含 `http://`、`https://` 或外部图片路径。

- [ ] **Step 3: 浏览器检查**

在浏览器中分别打开两份 HTML，检查 1440x900 和 390x844 视口，确认无横向溢出、文字重叠、图片丢失或打印分页异常。

### Task 3: 生成和验证 PDF

**Files:**
- Create: `图片批处理工具/产品介绍.pdf`
- Create: `图片批处理工具/安装说明.pdf`
- Create: `tmp/delivery/rendered/*.png`

- [ ] **Step 1: 生成 PDF**

使用 Edge headless 的 `--print-to-pdf` 将两份本地 HTML 分别输出为对应 PDF，启用背景图形并使用 CSS `@page` 控制 A4 页边距。

- [ ] **Step 2: 内容校验**

使用 `pdfinfo` 和 `pdftotext` 确认 PDF 可读取，产品介绍包含“核心功能”和“三步完成批处理”，安装说明包含“安装步骤”和“卸载方式”。

- [ ] **Step 3: 逐页渲染检查**

Run:

```powershell
pdftoppm -png 图片批处理工具\产品介绍.pdf tmp\delivery\rendered\product
pdftoppm -png 图片批处理工具\安装说明.pdf tmp\delivery\rendered\install
```

Expected: 每页生成清晰 PNG，无空白页、裁切、重叠、黑块或不可读中文。

### Task 4: 组装和清理正式交付目录

**Files:**
- Create: `图片批处理工具/图片批处理工具-安装包.exe`
- Delete: `tmp/delivery/`

- [ ] **Step 1: 复制安装包**

将 `artifacts/installer/ImageToolkitSetup.exe` 复制为 `图片批处理工具/图片批处理工具-安装包.exe`。

- [ ] **Step 2: 校验安装包一致性**

对源安装包和交付安装包计算 SHA256，要求完全一致。

- [ ] **Step 3: 清理中间文件**

删除 `tmp/delivery/`，不得删除仓库源码、构建产物或用户文件。

- [ ] **Step 4: 最终清单**

Run:

```powershell
Get-ChildItem 图片批处理工具 -File | Select-Object Name,Length
```

Expected: 目录恰好只有设计规定的 5 个文件。
