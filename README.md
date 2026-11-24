# 皇牌空战8 · PXN 2119 Pro 键位工具

Windows 原生 WPF 桌面应用，专门支持 PXN 2119 Pro（游戏设备名 PXN-F19，DeviceID 0x084711ff）。便携版不需要另装 Python 或 .NET，也不需要管理员权限。

![作战按键界面](docs/images/01-overview.png)

**[完整图文使用说明：六张界面截图、操作步骤与常见问题](docs/user-guide.md)**

## 快速使用

1. 连接摇杆与油门，使用 MODE 0，将 PXN 的 T 连发保持 OFF。
2. 完全退出游戏，打开工具，自动查找或手动选择 FlightStick.ini。
3. 选择游戏功能对应的实体输入，核对右侧“待保存的更改”。
4. 点“保存到游戏”，重新启动游戏，在训练中确认。

常用布局：**1 机炮、2 导弹、3 切换锁定、4 切换副武器**。8 号键对应暂停与手柄菜单键（≡），可在支持跳过的剧情中长按试用。

## 功能

工具提供作战按键、菜单导航、飞行轴与反向、POV1/POV2 分工、环顾方向、修改对比、自动备份与恢复，以及可放入 Obsidian 的 Markdown 键位表。

自动定位读取 Windows 注册表中的 Steam 路径、libraryfolders.vdf 与游戏安装清单；实际游戏绑定优先从当前 Windows 用户的 LocalAppData 配置目录中验证。多个有效候选文件必须由用户选择。也可手动选择 FlightStick.ini。

仅写入匹配设备 ID 的设备区段。保留其他设备、未知键、未编辑的绑定、注释、换行与编码。保存前检测外部改动和游戏进程，先备份再原子替换；备份恢复先进入预览，再由用户保存。菜单与飞行允许共用实体键；同一场景内的共用输入会提示。

开发要求：.NET SDK 9.0.203，Windows x64。无第三方 NuGet 依赖。离线打包从本机 .NET 9 Windows Desktop 运行库复制所需文件，并通过 SDK 的 AppHostRelativeDotNet 定位同目录 runtime 子文件夹；不会修改系统运行库。

```powershell
dotnet build src/AceCombat8.Desktop/AceCombat8.Desktop.csproj -c Release
dotnet run --project tests/AceCombat8.Tests/AceCombat8.Tests.csproj -c Release
.\tools\build.ps1
```

移动或分享便携版时，需要保留程序旁的 DLL、配置文件和整个 runtime 文件夹。实现依据：[Microsoft AppHostRelativeDotNet 文档](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#apphostrelativedotnet)。

测试使用真实格式的匿名控制器配置副本。UI 测试在应用自己的离屏窗口中运行并通过 WPF 内部渲染生成页面 PNG，不捕获桌面、不发送系统鼠标或键盘输入，也不写入真实游戏配置。测试工具保留临时夹具以便检查，不自动删除用户目录。

参见 [图文使用说明](docs/user-guide.md) 和 [开发与截图重现](docs/development.md)。说明截图均为应用渲染的示例配置，不包含私人桌面或本机用户名。
