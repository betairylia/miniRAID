# 快速上手

[手册目录](index.md) · [项目结构](Project-Structure.md)

## 打开项目

1. 准备 **Unity 6000.3.15f1**，版本依据是 [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt)。
2. 在 Unity Hub 中添加仓库根目录，即包含 `Assets`、`Packages` 和 `ProjectSettings` 的目录。
3. 使用对应编辑器打开，等待 Package Manager 解析依赖和资源导入。
4. 查看 Console，先处理包恢复或编译错误，再进入 Play Mode。

Unity 生成的 `.sln`、`.csproj`、`Library/` 和 `obj/` 已被 [.gitignore](../.gitignore) 排除。代码与资源修改应保留对应的 `.meta` 文件。

## 依赖从哪里来

| 来源 | 主要内容 | 核对位置 |
| --- | --- | --- |
| Unity Package Manager | URP 17.3.0、Cinemachine 2.10.7、Input System 1.19.0、Localization 1.5.8、Test Framework 1.6.0 | [manifest.json](../Packages/manifest.json) |
| 间接 Unity 包依赖 | Addressables，锁文件当前解析为 2.9.1 | [packages-lock.json](../Packages/packages-lock.json) |
| NuGet for Unity | C5 2.5.3、ClosedXML 0.102.1，以及 XML、字体等依赖 | [packages.config](../Assets/packages.config)、[NuGet.config](../Assets/NuGet.config) |
| 仓库内插件 | Odin Inspector / Serializer、DOTween、XLua 原生库等 | [Plugins](../Assets/Plugins/)、[DOTween](../Assets/DOTween/)、[NuGet](../Assets/NuGet/) |

这些是当前仓库记录的版本。Odin Inspector / Serializer 的使用情况应按你已有的授权配置处理；旧 README 中关于 Keyless claim 的猜测已移除。

NuGet 恢复目录为 `Assets/Packages/`，该目录被 Git 忽略。首次导入若报告缺少 C5、ClosedXML 等程序集，检查 NuGet 恢复结果、配置中的包源和 Console 错误。包源使用 HTTPS v2。2026-09-10 已在本机恢复 11 个 NuGet 依赖；新检出仍需恢复，直接退出 Safe Mode 不能证明依赖已恢复成功。

仓库中仍有 [Assets/SODatabase](../Assets/SODatabase/)，但当前 UPM manifest 没有旧 README 所说的 SODatabase Git 依赖。排查依赖时以 manifest 和锁文件为准。

## 选择场景

[EditorBuildSettings.asset](../ProjectSettings/EditorBuildSettings.asset) 当前启用了以下场景，顺序如下：

| 场景 | 用途 |
| --- | --- |
| [Title](../Assets/Scenes/Title.unity) | 标题入口；收到按键输入后加载 `CombatBase` |
| [CombatBase](../Assets/Scenes/CombatBase.unity) | 标题场景配置的战斗目标 |
| [SlimeKing-All](../Assets/Scenes/SlimeKing/SlimeKing-All.unity) | Slime King 战斗场景；配置地图名为 `default` |
| [AlphaWolf](../Assets/Scenes/OpenTest/AlphaWolf.unity) | Alpha Wolf 战斗场景；配置地图名为 `opentest-alphawolf` |

检查标题流程时打开 `Title`，进入 Play Mode 后给 Game 视图输入焦点，再按键。研究战斗内容时请打开 `CombatBase`，它会自动 additive 加载 `AlphaWolf`。单独打开 `AlphaWolf` 会缺少战斗服务，出现空引用。标题切场景逻辑见 [OpeningSequence.cs](../Assets/Scenes/Title/OpeningSequence.cs)。

战斗场景中的 [SceneConfig](../Assets/Scripts/Utils/SceneConfig.cs) 提供地图名、地图加载起点和动画等待设置；没有配置时，后端使用 `default` 地图与零坐标起点。`playerStartPosition` 用于选择初始地图块，不代表自动生成玩家单位。

地图块位于 [GameContent/MapChunks](../Assets/GameContent/MapChunks/)，地址登记在 [Addressables 默认组](../Assets/AddressableAssetsData/AssetGroups/Default%20Local%20Group.asset)。若地图与预期不符，检查 `SceneConfig.mapName` 和 `mapchunk_{地图名}_{x}_{y}_{z}` 地址。加载失败时源码会生成默认地面，因此出现地面不等于正确地图已加载。

## 首次运行检查

2026-09-10 已在 macOS Apple Silicon / Unity 6000.3.15f1 完成编译与启动冒烟验证：从 `CombatBase` 进入 Play Mode，确认 `AlphaWolf` 加载、角色/Boss/战斗 UI 渲染，并通过 CLI 查询运行状态、截图和退出 Play Mode。启动仍有旧版同样出现的 `Timestamp is not correct`，尚未验证完整战斗及平台构建。以下交互检查仍需后续完成。

- 导入完成后 Console 没有阻止运行的错误。
- `Title` 能进入 `CombatBase`；通过 `CombatBase` 自动加载 `AlphaWolf` 后，地图、角色与战斗 UI 能显示。
- 玩家可选择单位、移动或使用技能，战斗调度能继续到后续行动。
- 观察地图提示、伤害显示与敌人行动，并检查 Console 是否出现异常。
- 通过 `miniRAID > Mob Monitor` 和 `miniRAID > Combat Monitor` 查看单位与战斗数据；停止 Play Mode 后检查工作区变化。

项目安装了 Unity Test Framework，但本次在自有游戏代码目录中未找到 NUnit / UnityTest 测试方法或专用测试程序集。`RenderingTests/` 是渲染实验资料，不能替代游戏回归测试。

打包前核对上述构建场景、Addressables 内容和目标平台需要的插件。本文未验证任何平台构建，也未建立自动构建流程。

## CLI 开发环境

本分支使用 Unity CLI beta.9 + `com.unity.pipeline@0.6.0-exp.1`。已移除与 Pipeline 自带 CodeAnalysis 程序集冲突的旧 `com.unity.ai.assistant@1.0.0-pre.12`；不要直接重新添加该版本。Polybrush 升至 1.2.1，修复旧包在新编译器下的属性标注错误。项目的 `SequentialRootAgent.seqData` 同样移除了无效的 `[SerializeField]`（计算属性并不参与 Unity 字段序列化）。

先用 6000.3.15f1 打开项目，等待资源导入完成并关闭模态提示。在项目根目录运行：

```sh
unity command eval 'return UnityEngine.Application.unityVersion;'
unity command eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CombatBase.unity"); return "Loaded";'
unity command editor_play
unity command eval 'return UnityEngine.Time.frameCount;'
unity command eval 'UnityEngine.ScreenCapture.CaptureScreenshot("Temp/miniRAID-play.png"); return "Queued";'
unity command editor_stop
```

截图会在后续渲染帧写入，请在退出 Play Mode 前等待文件生成。`ScreenCapture` 能保留完整 UI；Pipeline 的 `screenshot` 命令在本项目中只捕获摄像机画面，未包含 overlay UI。在其他目录执行时，为每条命令传入 `--project-path /absolute/path/to/project`，避免连到其他编辑器。导入期间的 503/busy 或模态对话框造成的超时，应先等编辑器就绪再执行。

升级也保留了 Unity 自动生成的 URP/项目设置迁移；平台最低系统版本随 6.3 提高（Android API 25、iOS/tvOS 15、macOS 12）。目前验证范围是本机 Editor，并未完成各平台构建验证。
