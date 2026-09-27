# Mixamo 骨骼转换脚本

此目录由原 Tools/CharacterTools/MixamoBoneRenamer 迁入 Character Importer 模块。Python 只生成经过验证的骨骼 FBX；所有 Unity 导入与独立 AnimationClip 创建均在上级模块 C# 中完成，不再包含动态 C# 模板或反向调用 Unity CLI 的逻辑。

通常通过上级目录 README 中的 C# Execute 入口使用。此目录末尾的波浪线使 Unity 忽略其中内容，但 C# 仍可调用脚本。需要 Windows x64、Python 3.9+、C++ FBX SDK 和 Visual Studio x64 C++ 编译工具。默认查找上级仓库的 Tools/SDK/FBX，也可显式 --sdk。

## 离线转换与预检

在仓库根目录运行：

```powershell
$script = '.\UnityProject\Packages\com.veil.character-utilities\Editor\Module\CharacterImporter\PythonScripts~\convert.py'
& .\Tools\Python39\python.exe $script "C:\Animations\Idle.fbx" --dry-run
& .\Tools\Python39\python.exe $script "C:\Animations\Idle.fbx" --output "C:\Animations\Converted_Idle.fbx"
```

必须明确指定 --output（或使用 --dry-run / --in-place）。已有输出拒绝覆盖；仅 --in-place 允许替换输入，先备份 FBX 和 meta 至 `.backups`，保留原 meta。--no-import 是保留的兼容参数，现在 Python 始终只做转换。输入支持绝对路径、当前目录相对路径与 Assets/... 项目相对路径；--project 指定项目。缓存 `.build`、备份 `.backups` 和 Python 缓存不纳入 Git。

## 转换内容

1. 只修改 SDK 识别为 Skeleton 的节点，移除 `mixamorig:`、`mixamorig1:` 等前缀。
2. `Spine` → `Spine_0`；`Spine1` / `Spine2` → `Spine_1` / `Spine_2`。末尾数字加下划线，手指保留 1–4 编号；已有下划线不重复添加。末端 `_End` 名称保留。
3. 在 `Hips` 上方补入局部变换为单位矩阵的 `Bone_Root`。已有合规根骨骼则复用，不重复添加。
4. 保留骨骼节点连接及其动画曲线；保留 FBX 坐标轴、单位和动画时间范围，不重采样、不转单位，不把 Hips 位移自动搬到根骨骼。
5. 移除网格、蒙皮变形器、Blend Shapes、材质、贴图、相机、灯光和非骨骼节点；骨骼动画不需要的 Pose 资源也移除。**此工具用于动画输入，不用于保留角色蒙皮的模型改名。**
6. 导出后重新读取，核对骨骼路径、静态变换、骨骼动画绑定、关键帧时间/值、插值类型及常用切线数据，并确认输出只有骨骼节点、没有几何和材质。浮点比较容许 SDK 序列化误差。

## 支持范围与拒绝条件

当前针对一个 Mixamo 人形骨架、以 Hips 为主体根、动画已经烘焙在骨骼曲线上的输入。重名、多骨架、骨骼间夹有非骨骼节点、有约束、带动画或非单位变换的外围容器，以及带动画的已有 Bone_Root，会明确报错，避免静默改变动作。只允许移除未动画且变换为单位矩阵的外围容器。

名称齐全和 Unity Humanoid 导入通过不等于已经验证播放观感。不同骨架比例、参考姿势、特殊动画层或动画属性仍需人工检查。当前验证不替代逐帧播放测试；特殊 FBX 扩展属性不属于完整保真承诺。

独立 Avatar 不自动随模型骨架修改更新。修改项目骨骼规范后，应同时审查工具规则和 Avatar。

## 回归测试

```powershell
$tests = '.\UnityProject\Packages\com.veil.character-utilities\Editor\Module\CharacterImporter\PythonScripts~\tests\test_converter.py'
& .\Tools\Python39\python.exe $tests --sample "C:\Animations\Idle.fbx" --model .\UnityProject\Assets\Resources\Pawn\Humanoid\Humanoid.fbx
```

测试包含导出误差/损坏检测、覆盖保护、缺失输入、重复转换、中文路径、网格移除与骨骼保留、原位替换的备份和 meta 保留。未指定 sample/model 时跳过对应集成测试。
