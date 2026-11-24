# 开发、验证与截图重现

## 项目结构

| 目录 | 职责 |
| --- | --- |
| `src/AceCombat8.Core` | 配置解析、PXN 输入、编辑会话、Steam 定位、备份保存 |
| `src/AceCombat8.Desktop` | WPF 界面、Windows 服务、界面测试、截图渲染 |
| `tests/AceCombat8.Tests` | 无第三方测试框架依赖的回归测试入口 |
| `tests/Fixtures/FlightStick.ini` | 仅包含控制器定义的测试样本 |
| `tools/build.ps1` | 回归测试、离线便携打包、发布后界面验证 |
| `tools/create_icon.py` | 可选：用 Python 标准库重新生成图标 |

## 构建与测试

需要 Windows x64 和 .NET SDK 9.0.203，没有第三方 NuGet 包依赖。

```powershell
dotnet build src/AceCombat8.Desktop/AceCombat8.Desktop.csproj -c Release
dotnet run --project tests/AceCombat8.Tests/AceCombat8.Tests.csproj -c Release
.\tools\build.ps1
```

默认便携输出是 `artifacts/release`。脚本从本机 .NET 9 安装复制 Core 与 Windows Desktop 运行库到 `runtime` 子目录，通过 `AppHostRelativeDotNet` 定位，不修改系统运行库。移动时保留整个目录。

实现依据：[Microsoft AppHostRelativeDotNet 文档](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#apphostrelativedotnet)。

## 重现界面测试与截图

```powershell
dotnet run --project src/AceCombat8.Desktop/AceCombat8.Desktop.csproj -c Release -- --smoke-test tests/Fixtures/FlightStick.ini artifacts/ui
dotnet run --project src/AceCombat8.Desktop/AceCombat8.Desktop.csproj -c Release -- --render-docs tests/Fixtures/FlightStick.ini artifacts/documentation
```

两种模式均使用应用自己的离屏窗口和配置副本，不采集桌面，不注入系统鼠标键盘输入，不修改真实游戏配置。说明模式显示通用路径与“示例配置”，生成六张 PNG；PNG 保留界面文字的无损清晰度。

## 保存行为

配置值是嵌套 JSON，写入保留游戏使用的引号转义。仅替换目标设备区段中实际修改的绑定行，保留未知字段、其他设备、原换行和 BOM。

保存前检测原文件哈希与游戏进程，校验完整输出，创建逐字节备份，再以同目录临时文件原子替换；写入后重新读取核对。备份恢复先形成编辑会话，供用户核对差异，点击保存才会写入。

## 提交范围

源码、控制器测试样本、图标和图文文档进入仓库。`bin`、`obj`、`artifacts`、运行库、备份、凭据与个人 Obsidian 自动化脚本不提交。
