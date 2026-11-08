# SnapEdge

SnapEdge 是面向 Windows 掌机、触摸屏设备和视觉小说玩家的原生悬浮截图工具。轻触屏幕边缘按钮即可捕获当前前台窗口，并保存为 PNG。

## 当前功能

- WPF 原生悬浮按钮，使用 `WS_EX_NOACTIVATE` 和 `WM_MOUSEACTIVATE` 避免主动取得焦点；启动、失焦和后台巡检时都会用非激活方式恢复置顶。
- 鼠标左键或触屏轻触截图；鼠标右键或触屏长按约 500 ms 打开设置；按住拖动可移动按钮。
- 拖动结束后可吸附至当前显示器工作区的左侧或右侧，保留垂直位置并进行短暂缓动；贴边半隐藏默认开启，空闲 1.6 秒后只露出屏内一半，鼠标或触摸靠近即可滑出；若该边有相邻显示器或侧边任务栏，则保持完整显示；支持多显示器、负坐标和 PerMonitorV2 DPI。
- 通过 `Windows.Graphics.Capture` 和目标 `HWND` 捕获单帧；D3D11 staging 读回按 `RowPitch` 逐行整理为紧密 BGRA8 缓冲区。
- 独立开关客户区裁剪与保守的近黑边裁剪；无法可靠映射窗口边界时会报告失败，不保存未经设置的错误裁剪结果。
- PNG 无损保存，文件名带本地时间并使用 `CreateNew` 避免覆盖；可选复制到剪贴板。
- 触屏友好的设置窗口、贴边半隐藏与靠近唤出、用户配置持久化、系统托盘和单实例运行。
- 配置、截图和窗口跟踪均在本地处理；不上传截图或收集用户数据。

## 许可证

本项目采用 Mozilla Public License 2.0（MPL-2.0）。完整条款见 [LICENSE](LICENSE)，官方文本见 [Mozilla MPL 2.0](https://www.mozilla.org/en-US/MPL/2.0/)。

## 操作

| 操作 | 结果 |
|---|---|
| 左键单击 / 触屏轻触 | 截取当前前台有效窗口 |
| 右键单击 / 触屏长按 | 打开设置 |
| 左键或触屏按住拖动 | 移动悬浮按钮，松开后按设置吸附 |
| 托盘双击 | 打开设置 |
| 托盘右键 | 打开设置或退出程序 |

设置会自动保存到 `%LOCALAPPDATA%\SnapEdge\settings.json`。默认 PNG 目录为“图片\SnapEdge”。

## 构建与测试

需要 Windows x64 和 .NET 10 SDK。WGC API 投影由 Windows 目标框架提供；D3D11 互操作使用 `Vortice.Direct3D11` 3.8.3。首次还原 NuGet 包需要网络。

```powershell
dotnet restore SnapEdge.sln
dotnet build SnapEdge.sln -c Release
dotnet test SnapEdge.sln -c Release
```

若 .NET 10 SDK 未加入 `PATH`，可以直接使用已安装路径：

```powershell
& 'D:\SDK\DotNet\dotnet-sdk-10.0.403\dotnet.exe' test SnapEdge.sln -c Release
```

生成 Windows x64 自包含单文件 EXE：

```powershell
.\scripts\publish-release.ps1
```

输出位于 `artifacts\release\SnapEdge.exe`。发布不启用裁剪；单文件启用托管程序集压缩以减小体积，Native 库打包在 EXE 中并由 .NET 在运行时按需解压。

GitHub Actions 会在分支推送和 Pull Request 上运行 Windows 构建与测试。推送 `v*` 版本标签（例如 `v1.0.0`）会再次运行测试、生成单文件 EXE 并创建 GitHub Release；标签包含连字符时会标记为预发布版。发布示例：`git tag v1.0.0`，然后 `git push origin v1.0.0`。

## 手工验证清单

纯图像算法由 `SnapEdge.Core.Tests` 覆盖；WGC、WPF 输入和显示器行为需要在真实 Windows 桌面上检查：

1. 在窗口化、无边框和不同 DPI 缩放下分别验证游戏前台焦点不会因点击悬浮按钮而改变。
2. 用触屏快速轻触、保持超过 500 ms、越过拖动阈值移动，以及长按后再移动；每次轻触最多产生一张截图。
3. 拖到主屏和副屏左右两侧，包含位于主屏左方/上方的屏幕；检查吸附位置、垂直位置和触摸命中区域。
4. 对比打开/关闭客户区裁剪后的标题栏、DWM 边框和客户区尺寸。
5. 用纯黑、近黑、黑边加亮内容、渐变边缘和暗场 CG 检查黑边裁剪；纯黑或证据不足的图像应保留原图。
6. 最小化、关闭或切换目标窗口后截图，再恢复窗口连续截图；检查失败提示和后续恢复。
7. 连续快速截图并检查时间戳冲突不会覆盖文件；分别验证 PNG、剪贴板和关闭成功提示选项。
8. 检查托盘打开设置/退出、单实例启动、用户设置持久化和开机启动开关。

## 已知限制

- 当前只实现 Windows Graphics Capture，没有自动切换到 GDI/PrintWindow 备用路径。独占全屏、受保护视频、部分旧引擎或特定 GPU 驱动可能返回黑帧或失败；失败时显示提示，不会通过隐藏、最小化或激活游戏窗口重试。
- 最小化窗口通常没有可用帧。捕获依赖 WGC 的 HWND 支持；本项目的 Windows 编译目标是 Windows 10 2004（build 19041）及更新版本。
- 当前读回格式为 BGRA8；不进行 HDR 色调映射、缩放或重采样。黑边检测是保守的边缘统计规则，不理解游戏内容语义；暗场、纯色或边缘噪点可能导致保留原图。
- 窗口客户区映射根据 Win32 客户区、普通窗口矩形和 DWM 扩展边界计算。遇到无法可靠对应的特殊窗口会报告失败；尚未在不同游戏引擎、独占全屏、HDR 和多 GPU 设备上完成兼容性实测。
- 当前自动化测试覆盖纯图像裁剪逻辑；触摸硬件、焦点保持、WGC 游戏兼容性和多显示器 DPI 仍需按上方清单手工验证。

## 实现参考

- [WPF / WinForms 使用 Windows SDK WinRT 投影](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps)
- [Windows.Graphics.Capture HWND 捕获接口](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow)
- [WinRT COM 互操作与 CsWinRT ABI 封送](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-com-interop-csharp)
- [D3D11 staging 资源和 CPU 读回](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_usage)
- [Vortice.Direct3D11 3.8.3](https://www.nuget.org/packages/Vortice.Direct3D11/3.8.3)

## 项目结构

```text
.github/workflows/      GitHub Actions CI 与标签发布
src/SnapEdge.Core/       纯像素裁剪和黑边检测逻辑
src/SnapEdge.App/        WPF 窗口、Win32 互操作、WGC、PNG、设置和托盘
tests/SnapEdge.Core.Tests/   图像处理单元测试
scripts/                 Windows x64 单文件发布脚本
```
