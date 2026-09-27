# Character Importer

纯编辑器代码模块，没有窗口或菜单。调用方向为 **C# → Python → FBX SDK → C# / Unity 导入器 → 独立 AnimationClip**。

## 目录与依赖

- `CharacterImporter.cs`：Unity CLI 可调用的 `Execute` / `GetStatus` 入口。
- `CharacterAnimationImporter.cs`：调用 Python、配置临时 FBX 导入、提取片段、验证与清理。
- `PythonScripts~/convert.py`：只转换和验证骨骼 FBX，不调用 Unity，不生成 `.anim`。
- `PythonScripts~/native/converter.cpp`：调用已有 C++ FBX SDK。
- `PythonScripts~/tests`：离线转换回归测试。

`PythonScripts~` 以波浪线结尾，不作为 Unity 资源导入。C# 通过 PackageInfo 定位包的实际路径，再用操作系统进程调用脚本。旧的 Tools/CharacterTools/MixamoBoneRenamer 代码已迁入此目录，不再维护两份。

Python 运行环境与 FBX SDK 是共享依赖，继续使用仓库的 `Tools/Python39/python.exe` 和 `Tools/SDK/FBX`。首次构建原生桥接需要 Windows x64 Visual Studio C++ 编译工具。复制到其他工程时，可通过入口参数指定 Python、SDK、Avatar 和输出路径，不要求同名目录。桥接缓存及原位转换备份位于 PythonScripts~ 的 `.build` / `.backups`，不纳入 Git。

## Unity CLI 调用

先打开目标 Unity 工程并启用 Pipeline。在仓库根目录运行：

```powershell
# source 是下载的动画 FBX；建议放在 Assets 外。
$code = 'return CharacterUtilities.Editor.Module.CharacterImporter.CharacterImporter.Execute(@"C:\Animations\Idle.fbx", "FullBody_Idle");'
unity command eval $code --project-path .\UnityProject --json

# Execute 返回 running 后，继续查询，直到 completed 或 failed。
$code = 'return CharacterUtilities.Editor.Module.CharacterImporter.CharacterImporter.GetStatus();'
unity command eval $code --project-path .\UnityProject --json
```

`Execute` 是面向已打开编辑器的 Unity CLI 方法，不是独立终端命令；不需要新增窗口。它启动异步转换并立即返回，避免阻塞 Unity 主线程。不要在主线程对返回任务执行 Wait/Result；不支持通过 `-quit` 立即关闭编辑器后仍等待导入完成。`GetStatus` 返回状态、目标资源路径、时长或错误；`running` 不代表成功。同一入口同一时间只允许一次任务。

完整签名：

```csharp
CharacterImporter.Execute(
    string source,
    string clipName,
    string outputFolder = "Assets/Resources/Pawn/Humanoid/Animation",
    string avatarPath = "Assets/Resources/Pawn/Humanoid/Avatar/Humanoid_Avatar.asset",
    string pythonPath = "",
    string sdkPath = "",
    string take = "");
```

代码内也可在编辑器主线程 `await CharacterAnimationImporter.ImportAsync(...)`。`take` 只用于多 Take 文件选择，不是循环或 Root Motion 设置；单片段无需填写。

## 输出约定

- 名称采用 `Ani_` 前缀，A 大写，ni 小写；`FullBody_Idle` 自动生成 `Ani_FullBody_Idle.anim`，已带前缀不会重复添加，资源内部名称与文件名一致。
- 输出是独立的人形 AnimationClip，默认放在 Humanoid/Animation；存在同名文件或 meta 时拒绝覆盖。
- C# 在系统临时目录调用 Python，Python只生成骨骼动画 FBX。随后 C# 在 Assets 创建唯一临时目录，使用 Humanoid / Copy From Other Avatar 导入，复制片段后删除临时 FBX。
- 清理后重新加载片段，检查人形标记、时长、曲线数量和无 FBX 依赖；失败清理临时资源及已创建的片段。
- 源 FBX 保留。若输入在 Assets 中，它仍会被 Unity 导入；建议把下载原文件放在工程外。
- **循环、Root Motion、裁剪和压缩等 Clip 选项使用 Unity 默认值，本工具不提供这些配置。** 导入后自行调整。不创建 Controller，不播放动画，不修改或保存角色 Prefab 与场景。
- 现有 `Ani_FullBody_Idle.anim` 保持不变；本次入口改造用临时动画样本验证。

Python 只处理一个 Mixamo 人形骨架。命名规则、SDK 验证及离线运行说明见 [PythonScripts~/README.md](PythonScripts~/README.md)。

## 当前验证

编辑器编译通过。完整 Execute → Python → Unity 导入流程用临时的一秒骨骼动画验证，输出是独立人形 Clip，无 FBX 依赖。覆盖保护及不存在 Take 的失败清理通过，临时目录和失败片段没有残留。Python 五项回归测试通过；Unity 资源列表未导入 PythonScripts~ 中任何文件。测试资源已清理，未修改现有 Idle、Prefab 或场景。
