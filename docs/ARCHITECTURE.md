# 架构概览

GuitarSoul 是 .NET 10 WPF 应用，主要由三个项目组成。

| 项目 | 职责 |
|---|---|
| `src/GuitarSoul.App` | 窗口、桌面猫、场景绘制、用户交互和状态展示 |
| `src/GuitarSoul.Core` | 音乐特征处理、陪伴与共演规则、和弦草稿、记忆与保存逻辑 |
| `src/GuitarSoul.Audio.Windows` | Windows 音频设备枚举、采集、解码、录音和播放适配 |

`GuitarSoul.App` 引用 Core 和 Audio.Windows；Audio.Windows 引用 Core。`tests/GuitarSoul.Core.Tests` 直接检查核心规则。场景所需图像在 `src/GuitarSoul.App/Assets`，设备配置在 `src/GuitarSoul.Core/profiles/devices.json`。

## 主要数据流

1. 用户选择输入设备并点击开始倾听后，Windows 音频层才交付采样。
2. Core 从输入中提取声音活动和其他有限特征；陪伴与共演逻辑据此更新状态。
3. App 展示电平、猫的动作和和弦草稿。文本草稿与偏好保存在用户本地目录。
4. 本地伴奏由独立播放控制管理；电脑回放声音不能自动视为玩家的吉他贡献。

可选的本地语言层只根据已经确认的结构化状态生成短句，失效时回退固定句。研究用音频模型不在应用运行链路中，也未包含在此发布候选。

## 边界

- 启动和恢复草稿不自动开始采集。
- 和弦结果是可错的草稿；顺序提示不保存可回放的音频循环。
- 只有「一起弹」状态使用持琴造型；其他状态保持各自外形。
- 工程检查不代表真实设备兼容或演奏体验已获验收。
