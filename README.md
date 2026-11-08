<div align="center">
  <img src="src/SnapEdge.App/Assets/SnapEdge.svg" width="104" alt="SnapEdge 标志" />
  <h1>SnapEdge</h1>
  <p>为 Windows 掌机、触摸屏设备与 GalGame / 视觉小说玩家设计的轻量级原生悬浮截图工具</p>
  <p><strong>轻触悬浮按钮，立即截取并保存当前游戏窗口。</strong></p>
  <p>
    <a href="https://github.com/100pangci/SnapEdge/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/100pangci/SnapEdge?label=release" /></a>
    <a href="https://github.com/100pangci/SnapEdge/blob/main/LICENSE"><img alt="License: MPL-2.0" src="https://img.shields.io/github/license/100pangci/SnapEdge?label=license" /></a>
    <a href="https://github.com/100pangci/SnapEdge/releases/latest"><img alt="Windows 10 2004+ x64" src="https://img.shields.io/badge/platform-Windows%2010%202004%2B%20%7C%20x64-0078D4" /></a>
    <a href="https://github.com/100pangci/SnapEdge/actions/workflows/ci.yml"><img alt="Build status" src="https://img.shields.io/github/actions/workflow/status/100pangci/SnapEdge/ci.yml?branch=main&label=build" /></a>
  </p>
</div>

SnapEdge 把截图按钮常驻在屏幕边缘。玩 GalGame 或视觉小说时，无需找键盘快捷键、框选区域或切换到其他工具，轻触按钮即可保存游戏画面。应用以原生 WPF 窗口运行，点击时不会主动抢走游戏焦点。

## 核心功能

- **一触即截**：轻触悬浮按钮，捕获当前前台可用窗口并保存为 PNG。
- **为触屏设计**：轻触截图、长按约 500 毫秒打开设置、按住拖动调整位置。
- **贴边与唤出**：按钮可吸附到显示器工作区左右边缘；启用半隐藏后，空闲时缩入屏幕边缘，鼠标移入或触碰按钮露出部分即可滑出。若任务栏或相邻显示器占用边缘，按钮可能保持完整显示。
- **专注游戏画面**：可分别启用窗口客户区裁剪和保守的近黑边裁剪，让截图尽可能只保留画面内容。
- **原生、轻量、本地**：使用 C#、WPF、Win32 和 Windows Graphics Capture，不依赖 Electron。截图和设置保存在本机，不上传用户数据。

### 开发者实测环境

已在联想拯救者 Windows 掌机上验证触摸操作，并在游戏全屏状态及开启 Magpie 的环境中验证截图。以上是特定设备与使用环境的实测结果，不代表所有游戏、独占全屏模式、显卡驱动或缩放方案都兼容。

## 软件截图与演示

仓库目前没有可用的软件界面截图或 GIF，因此暂不展示截图；后续可在此补充真实运行画面。

## 下载与安装

**[前往 GitHub Releases 下载 SnapEdge](https://github.com/100pangci/SnapEdge/releases/latest)**

| 项目 | 要求与说明 |
|---|---|
| 系统 | Windows 10 版本 2004（Build 19041）或更新版本；Windows 11 |
| 架构 | x64 |
| 运行时 | 需要安装 **.NET 10 Windows Desktop Runtime x64**；[前往 Microsoft .NET 10 下载页](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) |
| 发布文件 | `SnapEdge.exe` 是框架依赖型单文件，只包含应用发布文件，不包含 .NET 运行时 |
| 安装与权限 | 无需安装程序；下载 EXE 后即可运行。应用以当前用户权限启动，不要求管理员权限 |

首次运行前，如果设备尚未安装 .NET 10 Windows Desktop Runtime，请先从 Microsoft 下载页安装对应的 **Windows Desktop Runtime x64**，再运行 `SnapEdge.exe`。GitHub Release 附件中只有一个 EXE，但这不代表它内置了运行时。

## 使用说明

| 操作 | 效果 |
|---|---|
| 鼠标左键单击 / 触屏轻触 | 截取当前前台可用窗口并保存 PNG |
| 鼠标右键 / 触屏长按约 500 毫秒 | 打开设置 |
| 按住悬浮按钮拖动 | 移动按钮；启用自动吸附时，松开后吸附到最近显示器工作区的左侧或右侧 |
| 托盘图标双击 | 打开设置 |
| 托盘图标右键 | 打开菜单，可进入设置或退出程序 |

截图默认保存到系统“图片”目录下的 `SnapEdge` 文件夹，文件名包含拍摄时间；可以在设置中更改目录。设置文件位于 `%LOCALAPPDATA%\SnapEdge\settings.json`。

## 设置选项

| 设置 | 说明 | 默认值 |
|---|---|---|
| 窗口客户区裁剪 | 裁去标题栏和窗口装饰；无法可靠映射窗口边界时会提示失败且不保存错误裁剪结果 | 开启 |
| 自动裁剪近黑边 | 检测画面边缘连续的近黑区域；与客户区裁剪独立开关 | 开启 |
| 黑边阈值与颜色容差 | 调整近黑色判断；阈值越低，裁剪越保守 | 亮度 18，容差 20 |
| 自动吸附与半隐藏 | 拖动后贴到显示器工作区边缘；空闲约 1.6 秒后尝试半隐藏，鼠标移入或触碰按钮露出部分时唤出。任务栏或相邻显示器占用边缘时可能不半隐藏；此功能需要同时开启自动吸附 | 均开启 |
| 按钮大小与透明度 | 调整悬浮按钮外观；尺寸使用 WPF 逻辑单位并随显示缩放 | 62 DIP，90% |
| 截图后复制到剪贴板 | 保存 PNG 后同时复制图像 | 关闭 |
| 截图成功提示 | 控制截图保存成功后的提示；失败提示仍会显示 | 开启 |
| 登录时启动 | 登录 Windows 后自动运行 | 关闭 |

## 裁剪方式

**窗口客户区裁剪**根据 Win32 客户区与窗口边界映射，移除标题栏和外侧窗口装饰。遇到无法可靠匹配的特殊窗口时，SnapEdge 会报告失败，不会保存未经正确裁剪的结果。

**智能黑边裁剪**逐边检查连续的近黑区域，并采用保守规则处理：

- 不缩放或重采样图像，只保留原始像素区域。
- 对纯黑、暗场或证据不足的画面优先保留原图，避免激进裁剪。
- 客户区裁剪和黑边裁剪可以分别开关；启用时先裁客户区，再检测黑边。
- 算法依据边缘像素判断，不理解游戏画面内容，无法保证所有场景都准确识别。

## 技术实现

- **悬浮交互**：WPF 窗口配合 Win32 `WS_EX_NOACTIVATE`、`WM_MOUSEACTIVATE`，尽量避免按钮输入激活悬浮窗或切走游戏焦点。
- **窗口跟踪**：跟踪前台窗口 HWND，作为截图目标。
- **窗口捕获**：使用 Windows Graphics Capture 按 HWND 捕获单帧；通过 D3D11 staging 纹理读回 BGRA8，并按 `RowPitch` 整理像素。
- **显示适配**：启用 PerMonitorV2 DPI 感知，支持多显示器坐标与缩放环境。
- **图像保存**：以 PNG 无损编码保存；按时间命名，文件名冲突时使用新名称，不覆盖已有截图。
- **黑边检测**：逐边统计近黑像素比例与颜色差异；画面或检测结果不明确时保留原图。

## 兼容性与已知限制

- 当前只使用 Windows Graphics Capture，没有 GDI 或 `PrintWindow` 备用捕获路径。最小化窗口、受保护内容、部分旧游戏引擎、独占全屏或特定显卡驱动可能无法提供可用帧；SnapEdge 会提示失败，不会通过隐藏、最小化或激活游戏窗口来重试。
- 开发者已在上述特定掌机与游戏环境中实测成功，但不同硬件、游戏、驱动和缩放组合仍需分别验证。
- 当前捕获格式为 BGRA8，不提供 HDR 色调映射，也不缩放截图。
- 自动化测试覆盖纯图像裁剪逻辑；触屏交互、窗口焦点、WGC 游戏兼容性和多显示器行为仍依赖真实 Windows 环境验证。

## 本地构建与测试

需要 Windows x64 和 .NET 10 SDK。首次还原 NuGet 依赖需要网络。

```powershell
dotnet restore SnapEdge.sln
dotnet build SnapEdge.sln --configuration Release
dotnet test SnapEdge.sln --configuration Release
```

生成 Windows x64 框架依赖型单文件发布文件：

```powershell
.\scripts\publish-release.ps1
```

输出位于 `artifacts\release\SnapEdge.exe`。脚本不会打包 .NET 运行时，也不会生成安装程序。

GitHub Actions 会在分支推送和 Pull Request 上构建并运行测试。推送 `v*` 标签会运行发布流程，将 `SnapEdge.exe` 附加到 GitHub Release；标签中包含连字符时会标记为预发布版。

## 项目结构

```text
.github/workflows/          GitHub Actions 构建、测试与发布流程
src/SnapEdge.App/           WPF 界面、Win32 互操作、窗口捕获与文件保存
src/SnapEdge.Core/          像素裁剪与近黑边检测
tests/SnapEdge.Core.Tests/  图像处理单元测试
scripts/                    Windows x64 发布脚本
```

## 开源协议

SnapEdge 使用 [Mozilla Public License 2.0（MPL-2.0）](LICENSE)。
