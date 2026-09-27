# Veil 人形骨骼映射规范

版本：第一版；核对日期：2026-09-28。

## 适用范围与基准

本规范描述 Mixamo 人形骨骼到 Veil 项目骨骼名称、层级及 Unity Humanoid Avatar 槽位的映射。当前基准为同级 `Humanoid.fbx`，角色 Prefab 为 `PV_Humanoid.prefab`，后者是该 FBX 的变体。

当前模型来自原始 `Ch36_nonPBR.fbx`，原骨骼前缀为 `mixamorig1:`。原始模型有 65 根骨骼；项目新增 `Bone_Root` 后共 66 根。本表以实际模型为准，不假定所有 Mixamo 下载资源具有完全相同的骨骼数量。

项目骨骼名称与 Unity Humanoid 槽位名称是两层概念。例如项目的 `LeftArm` 对应 Unity 的 `LeftUpperArm`，不必将项目骨骼改名为 Unity 枚举名称。Avatar 是人形映射资源，实际 Transform 骨骼层级保留在模型及 Prefab 中。

## 命名转换规则

1. 去掉 Mixamo 命名空间前缀。本模型移除 `mixamorig1:`；其他来源若使用 `mixamorig:` 或带其他数字的同类前缀，也应先识别前缀再处理。不得无条件删除任意命名空间。
2. 保留部位名称和 `Left` / `Right` 标识，名称大小写严格按下表。
3. 同组编号与名称之间使用一个下划线。脊柱 `Spine`、`Spine1`、`Spine2` 分别映射为 `Spine_0`、`Spine_1`、`Spine_2`。
4. 手指保留原有 1–4 编号，例如 `LeftHandIndex1` → `LeftHandIndex_1`。不为原本没有零号的手指额外生成 `_0`。
5. 保留已有末端标记：`HeadTop_End`、`LeftToe_End`、`RightToe_End`。手指 `_4` 是末端节点，不映射到 Humanoid 的第四指节。
6. 已符合规范的名称不得重复加下划线。若转换后发生重名，应停止并检查，不用自动数字后缀掩盖冲突。
7. 新增骨骼名为 `Bone_Root`，只有一个下划线，不使用 `BoneRoot`。

## 根骨骼与层级

```text
Humanoid                     模型容器，不计入 66 根骨骼
├─ Bone_Root                 新增骨骼，模型原点
│  └─ Hips                   保留原髋骨位置
│     ├─ LeftUpLeg → LeftLeg → LeftFoot → LeftToeBase → LeftToe_End
│     ├─ RightUpLeg → RightLeg → RightFoot → RightToeBase → RightToe_End
│     └─ Spine_0 → Spine_1 → Spine_2
│        ├─ Neck → Head → HeadTop_End
│        ├─ LeftShoulder → LeftArm → LeftForeArm → LeftHand → 左手指链
│        └─ RightShoulder → RightArm → RightForeArm → RightHand → 右手指链
└─ SkinMesh_LOD0             蒙皮节点，不是骨骼
```

`Bone_Root` 在模型局部空间的位置为 `(0, 0, 0)`，旋转为 `(0, 0, 0)`，缩放为 `(1, 1, 1)`。它是 `Hips` 的父节点，不替代 `Hips` 的 Humanoid 映射。不把髋骨强行移到原点，也不改变其他原有骨骼的层级或绑定姿势；新增根骨骼时补齐绑定姿势数据。

当前 Unity 的 `SkinnedMeshRenderer.rootBone` 仍为 `Hips`。这个渲染器引用与层级最上方的 `Bone_Root` 是不同概念，本次没有把它改成 `Bone_Root`。新增根节点本身也不代表已经配置了 Root Motion。

## 完整骨骼映射

下表的源名称按本次原始模型列出。Unity 槽位列来自当前有效 Avatar 的实际读取结果；“不映射”表示骨骼保留，但不占用 Humanoid 槽位，不表示应该删除该骨骼。父节点列是项目中的名称。

| Mixamo 源骨骼 | Veil 骨骼 | Veil 父节点 | Unity Humanoid 槽位 |
| --- | --- | --- | --- |
| 新增，无源骨骼 | `Bone_Root` | `Humanoid` | 不映射 |
| `mixamorig1:Hips` | `Hips` | `Bone_Root` | `Hips` |
| `mixamorig1:LeftUpLeg` | `LeftUpLeg` | `Hips` | `LeftUpperLeg` |
| `mixamorig1:LeftLeg` | `LeftLeg` | `LeftUpLeg` | `LeftLowerLeg` |
| `mixamorig1:LeftFoot` | `LeftFoot` | `LeftLeg` | `LeftFoot` |
| `mixamorig1:LeftToeBase` | `LeftToeBase` | `LeftFoot` | `LeftToes` |
| `mixamorig1:LeftToe_End` | `LeftToe_End` | `LeftToeBase` | 不映射 |
| `mixamorig1:RightUpLeg` | `RightUpLeg` | `Hips` | `RightUpperLeg` |
| `mixamorig1:RightLeg` | `RightLeg` | `RightUpLeg` | `RightLowerLeg` |
| `mixamorig1:RightFoot` | `RightFoot` | `RightLeg` | `RightFoot` |
| `mixamorig1:RightToeBase` | `RightToeBase` | `RightFoot` | `RightToes` |
| `mixamorig1:RightToe_End` | `RightToe_End` | `RightToeBase` | 不映射 |
| `mixamorig1:Spine` | `Spine_0` | `Hips` | `Spine` |
| `mixamorig1:Spine1` | `Spine_1` | `Spine_0` | `Chest` |
| `mixamorig1:Spine2` | `Spine_2` | `Spine_1` | `UpperChest` |
| `mixamorig1:LeftShoulder` | `LeftShoulder` | `Spine_2` | `LeftShoulder` |
| `mixamorig1:LeftArm` | `LeftArm` | `LeftShoulder` | `LeftUpperArm` |
| `mixamorig1:LeftForeArm` | `LeftForeArm` | `LeftArm` | `LeftLowerArm` |
| `mixamorig1:LeftHand` | `LeftHand` | `LeftForeArm` | `LeftHand` |
| `mixamorig1:LeftHandIndex1` | `LeftHandIndex_1` | `LeftHand` | `Left Index Proximal` |
| `mixamorig1:LeftHandIndex2` | `LeftHandIndex_2` | `LeftHandIndex_1` | `Left Index Intermediate` |
| `mixamorig1:LeftHandIndex3` | `LeftHandIndex_3` | `LeftHandIndex_2` | `Left Index Distal` |
| `mixamorig1:LeftHandIndex4` | `LeftHandIndex_4` | `LeftHandIndex_3` | 不映射 |
| `mixamorig1:LeftHandMiddle1` | `LeftHandMiddle_1` | `LeftHand` | `Left Middle Proximal` |
| `mixamorig1:LeftHandMiddle2` | `LeftHandMiddle_2` | `LeftHandMiddle_1` | `Left Middle Intermediate` |
| `mixamorig1:LeftHandMiddle3` | `LeftHandMiddle_3` | `LeftHandMiddle_2` | `Left Middle Distal` |
| `mixamorig1:LeftHandMiddle4` | `LeftHandMiddle_4` | `LeftHandMiddle_3` | 不映射 |
| `mixamorig1:LeftHandPinky1` | `LeftHandPinky_1` | `LeftHand` | `Left Little Proximal` |
| `mixamorig1:LeftHandPinky2` | `LeftHandPinky_2` | `LeftHandPinky_1` | `Left Little Intermediate` |
| `mixamorig1:LeftHandPinky3` | `LeftHandPinky_3` | `LeftHandPinky_2` | `Left Little Distal` |
| `mixamorig1:LeftHandPinky4` | `LeftHandPinky_4` | `LeftHandPinky_3` | 不映射 |
| `mixamorig1:LeftHandRing1` | `LeftHandRing_1` | `LeftHand` | `Left Ring Proximal` |
| `mixamorig1:LeftHandRing2` | `LeftHandRing_2` | `LeftHandRing_1` | `Left Ring Intermediate` |
| `mixamorig1:LeftHandRing3` | `LeftHandRing_3` | `LeftHandRing_2` | `Left Ring Distal` |
| `mixamorig1:LeftHandRing4` | `LeftHandRing_4` | `LeftHandRing_3` | 不映射 |
| `mixamorig1:LeftHandThumb1` | `LeftHandThumb_1` | `LeftHand` | `Left Thumb Proximal` |
| `mixamorig1:LeftHandThumb2` | `LeftHandThumb_2` | `LeftHandThumb_1` | `Left Thumb Intermediate` |
| `mixamorig1:LeftHandThumb3` | `LeftHandThumb_3` | `LeftHandThumb_2` | `Left Thumb Distal` |
| `mixamorig1:LeftHandThumb4` | `LeftHandThumb_4` | `LeftHandThumb_3` | 不映射 |
| `mixamorig1:Neck` | `Neck` | `Spine_2` | `Neck` |
| `mixamorig1:Head` | `Head` | `Neck` | `Head` |
| `mixamorig1:HeadTop_End` | `HeadTop_End` | `Head` | 不映射 |
| `mixamorig1:RightShoulder` | `RightShoulder` | `Spine_2` | `RightShoulder` |
| `mixamorig1:RightArm` | `RightArm` | `RightShoulder` | `RightUpperArm` |
| `mixamorig1:RightForeArm` | `RightForeArm` | `RightArm` | `RightLowerArm` |
| `mixamorig1:RightHand` | `RightHand` | `RightForeArm` | `RightHand` |
| `mixamorig1:RightHandIndex1` | `RightHandIndex_1` | `RightHand` | `Right Index Proximal` |
| `mixamorig1:RightHandIndex2` | `RightHandIndex_2` | `RightHandIndex_1` | `Right Index Intermediate` |
| `mixamorig1:RightHandIndex3` | `RightHandIndex_3` | `RightHandIndex_2` | `Right Index Distal` |
| `mixamorig1:RightHandIndex4` | `RightHandIndex_4` | `RightHandIndex_3` | 不映射 |
| `mixamorig1:RightHandMiddle1` | `RightHandMiddle_1` | `RightHand` | `Right Middle Proximal` |
| `mixamorig1:RightHandMiddle2` | `RightHandMiddle_2` | `RightHandMiddle_1` | `Right Middle Intermediate` |
| `mixamorig1:RightHandMiddle3` | `RightHandMiddle_3` | `RightHandMiddle_2` | `Right Middle Distal` |
| `mixamorig1:RightHandMiddle4` | `RightHandMiddle_4` | `RightHandMiddle_3` | 不映射 |
| `mixamorig1:RightHandPinky1` | `RightHandPinky_1` | `RightHand` | `Right Little Proximal` |
| `mixamorig1:RightHandPinky2` | `RightHandPinky_2` | `RightHandPinky_1` | `Right Little Intermediate` |
| `mixamorig1:RightHandPinky3` | `RightHandPinky_3` | `RightHandPinky_2` | `Right Little Distal` |
| `mixamorig1:RightHandPinky4` | `RightHandPinky_4` | `RightHandPinky_3` | 不映射 |
| `mixamorig1:RightHandRing1` | `RightHandRing_1` | `RightHand` | `Right Ring Proximal` |
| `mixamorig1:RightHandRing2` | `RightHandRing_2` | `RightHandRing_1` | `Right Ring Intermediate` |
| `mixamorig1:RightHandRing3` | `RightHandRing_3` | `RightHandRing_2` | `Right Ring Distal` |
| `mixamorig1:RightHandRing4` | `RightHandRing_4` | `RightHandRing_3` | 不映射 |
| `mixamorig1:RightHandThumb1` | `RightHandThumb_1` | `RightHand` | `Right Thumb Proximal` |
| `mixamorig1:RightHandThumb2` | `RightHandThumb_2` | `RightHandThumb_1` | `Right Thumb Intermediate` |
| `mixamorig1:RightHandThumb3` | `RightHandThumb_3` | `RightHandThumb_2` | `Right Thumb Distal` |
| `mixamorig1:RightHandThumb4` | `RightHandThumb_4` | `RightHandThumb_3` | 不映射 |

当前共有 52 个 Humanoid 槽位映射；剩余 14 根为新增根骨骼及末端节点。蒙皮 Renderer 的 bones 数量也为 52，但“蒙皮引用骨骼”与“Humanoid 槽位”是不同集合概念，不应仅凭数量相等推断两者相同。

## 模型与导入约定

| 项目 | 当前约定 |
| --- | --- |
| FBX | `Humanoid.fbx`，保留模型和骨骼，引用独立 Avatar |
| Prefab | `PV_Humanoid.prefab`，与 FBX 同级，保持模型变体关联 |
| 蒙皮节点 / Mesh 名称 | `SkinMesh_LOD0` |
| 默认蒙皮顶点色 | RGBA `(0, 1, 0, 1)` |
| Animation Type | Humanoid |
| Avatar Definition | Copy From Other Avatar |
| Source Avatar | `Avatar/Humanoid_Avatar.asset` |
| Avatar 名称 | `Humanoid_Avatar` |
| Import Animation | 关闭，仅指当前默认角色模型 |
| Material Creation Mode | None |
| Import BlendShapes | 关闭 |
| Import Vertex Colors | 开启 |

Avatar 已从原模型生成的有效人形 Avatar 复制为 `Avatar/Humanoid_Avatar.asset`，FBX 使用 Copy From Other Avatar 引用它。当前设置下 FBX 不再生成 Avatar 子资源和 Animator；`PV_Humanoid.prefab` 变体上保留独立的 Animator 组件，并引用同一份独立 Avatar。

独立 Avatar 是复制时的骨架定义，不会自动随 FBX 骨骼修改而更新。后续改变骨骼名称、层级或参考姿势时，应重新验证并按需要重新生成 Avatar，不能让 FBX 长期引用过期定义。

Mesh 仍保留在 FBX 内，不保留 `Rig`、`Skins`、`Prefabs` 子目录。动画资源以后平铺在 `Animation` 中，使用“作用范围_动作名称”的命名方式；`Controllers` 保留。动画源 FBX 需要另行设置动画导入，不能照搬本默认模型的关闭动画选项。

## 后续模型和动画接入

动画转换入口位于 Character Utilities 包的 `Editor/Module/CharacterImporter`，使用方式见该模块 README。C# 调用包内 Python 转换骨骼动画，最终提取独立片段并删除临时 FBX。当前 Idle 为 `Animation/Ani_FullBody_Idle.anim`。转换会移除蒙皮网格，角色模型本体不要用它替代保留蒙皮的处理流程。

- 转换前保留原始文件；先识别骨骼，再执行名称转换，不能对 FBX 的所有字符串盲目替换。
- 对角色模型改名时保留网格、蒙皮权重、绑定矩阵和骨骼变换，不能遗漏绑定关系。下面的动画转换工具则有意移除网格和蒙皮，只保留骨骼与动画；两种处理范围不同。
- 对需要统一 Transform 路径的动画，也应执行相同命名与根节点处理。名称相同不是动画兼容性的充分条件；需要检查层级、参考姿势、坐标轴和单位。
- 当前角色以 T-Pose 为基准。新模型应检查参考姿势和 Avatar 映射，不能仅因来自 Mixamo 就认定有效，也不能对不同骨架直接盲目复用同一个 Avatar。
- 采用 Humanoid 重定向时检查动作结果；涉及 Transform 路径绑定、附加骨骼或根运动时另行核对。动画路径绑定可能受新增 `Bone_Root` 和重命名影响。

## 动画转换工具使用

工具现位于 `Packages/com.veil.character-utilities/Editor/Module/CharacterImporter`，是纯代码编辑器模块，没有窗口。Python、原生 FBX SDK 桥接和测试已迁入模块下 `PythonScripts~`；此目录不导入 Unity。原 Tools 下的转换器不再作为入口。

调用方向：C# Execute → 包内 Python → SDK 转换骨骼 FBX → C# 配置 Unity 导入 → 独立 AnimationClip。Python 不生成 Unity 动画资源，也不再反向调用 Unity CLI。

打开 UnityProject，确保 Unity CLI 可连接，从仓库根目录运行：

```powershell
$code = 'return CharacterUtilities.Editor.Module.CharacterImporter.CharacterImporter.Execute(@"C:\Animations\Idle.fbx", "FullBody_Idle");'
unity command eval $code --project-path .\UnityProject --json
$code = 'return CharacterUtilities.Editor.Module.CharacterImporter.CharacterImporter.GetStatus();'
unity command eval $code --project-path .\UnityProject --json
```

Execute 启动任务后返回，继续查询状态直到 completed / failed；running 不代表成功。不要阻塞 Unity 主线程等待任务。默认使用已有 Python 运行环境、FBX SDK 和本目录的 Avatar/Humanoid_Avatar.asset；入口可指定这些路径及输出目录。完整参数见包内 CharacterImporter/README.md。

名称前缀是 `Ani_`，不是 `Any_`。上述命令得到 `Animation/Ani_FullBody_Idle.anim`，内部片段同名，已有输出拒绝覆盖。当前 Idle 已存在，不要再次同名导入。

临时 FBX 设置 Humanoid / Copy From Other Avatar；片段提取后清理临时 FBX，再核对人形标记、时长、曲线数量和不存在 FBX 依赖。源文件保留，建议将下载的 FBX 放在 Assets 外。角色 Humanoid.fbx 继续保留。

**暂不提供循环、Root Motion 等 Clip 配置，使用 Unity 默认值，导入后自行调整。** 工具不创建 Controller、不播放、不修改角色或 Prefab，不保存场景。

当前独立 Idle 约 2.933 秒；原转换核对了 66 根骨骼与 159 条源曲线，Unity 片段有 130 条曲线，已确认无 FBX 依赖。成功导入不替代动作观感和根运动的播放验证。

## 验收与当前验证范围

1. 骨骼名称唯一，无 Mixamo 前缀；本基准为 66 根骨骼，层级和上表一致。
2. `Bone_Root` 位于模型原点，`Hips` 及原有网格位置未改变。
3. 映射后回读核对网格、骨骼层级、蒙皮权重、绑定矩阵及内嵌贴图；浮点序列化的极小误差不按字符串完全一致判断。
4. Unity 当前实际验证 `Avatar.isValid == true`、`Avatar.isHuman == true`；变体的源资源为 `Humanoid.fbx`。
5. 共用动画播放、变形观感和 Root Motion 尚未完成验收。后续至少用一段共用动画验证，不能以 Avatar 有效代替播放验证。

## 文档目录

本文件位于 `Documents~`。末尾的 `~` 使 Unity 资源导入器忽略该目录；这不限制操作系统或脚本直接访问，也不等于 Git 忽略。该目录及文档不手工生成 `.meta`。

参考：[Unity 手册：特殊文件夹名称 / 隐藏的资源](https://docs.unity3d.com/cn/2022.3/Manual/SpecialFolders.html)。
