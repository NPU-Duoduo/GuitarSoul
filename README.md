# GuitarSoul

**一只陪你待在音乐里的像素猫。**

GuitarSoul 是一款 Windows 桌面吉他陪伴原型。它把实时音频反馈、本地伴奏和像素猫放进同一个排练空间，尝试让独自弹琴时也有一个安静的听众。

当前源码对应 **0.15.3 预览版**，开发者 **Duoduo_127**。代码采用 [MIT 许可证](LICENSE)，欢迎下载、体验、修改和分享；保留原版权和许可声明即可。

欢迎通过 [Issue 和 Pull Request](CONTRIBUTING.md) 反馈体验或改进代码。

## 功能

- 用户选择输入设备并主动开始倾听；界面显示输入电平和声音变化。
- 播放本地伴奏，猫根据声音活动作出反馈。
- 显示和弦识别草稿、重复顺序提示，并可保存文本记录。
- 像素猫与排练室场景呈现不同状态和本地时间驱动的光影变化。

## 已知限制

和弦可能漏记、错报或延迟。重复顺序提示不会循环播放录音，也不会生成伴奏。猫的表现不代表曲风、歌曲或演奏者情绪识别。真实吉他接线、不同驱动、长时间运行与完整现场体验仍需验证。

## 从源码运行

需要 Windows、.NET 10 SDK 和可访问 NuGet 的网络环境。构建脚本优先使用项目内的 `.tools/dotnet/dotnet.exe`；没有该目录时使用系统 `dotnet`。

```powershell
.\scripts\build.ps1
.\scripts\run.ps1
```

如需运行核心行为检查，可执行 `.\scripts\build.ps1 -RunChecks`。这组检查包含本地回环服务，部分受限环境可能阻止其启动。

双击 `start-guitar-soul.cmd` 也可启动。运行时不会自动采集声音；需要在应用中选择输入并点击开始倾听。操作见[用户指南](USER-GUIDE.md)。

## 目录

| 路径 | 内容 |
|---|---|
| `src/GuitarSoul.App` | WPF 界面、猫与场景 |
| `src/GuitarSoul.Core` | 音频特征、共演状态与本地数据规则 |
| `src/GuitarSoul.Audio.Windows` | Windows 音频采集与播放 |
| `tests/GuitarSoul.Core.Tests` | 核心行为检查 |
| `scripts` | 构建、运行和打包脚本 |
| `installer` | Windows 安装器定义 |
| `docs` | [架构概览](docs/ARCHITECTURE.md)、[当前状态](docs/STATUS.md)与[素材来源](docs/ASSET-SOURCES.md) |

## 发布状态

该目录只包含准备公开的源码与必要素材，不包含现成安装包、宣传视频、用户录音、模型权重或历史实验材料。项目所有者确认随仓库提供的图像素材均为原创；本项目原创代码、文档和图像素材使用 MIT 许可，来源见[素材说明](docs/ASSET-SOURCES.md)。NuGet 等第三方依赖遵循各自的许可证。
