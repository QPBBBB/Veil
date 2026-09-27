# Veil 工程的 FBX SDK 使用说明

本目录提供 Autodesk FBX SDK **2020.3.11，Windows VS2022 发行版**的精简文件，供 Veil 的原生 C++ 工具读取、检查和处理 FBX。当前保留 **Windows x64 / Release / 动态链接**配置，已在 Intel CPU 的 Windows 11 上验证。

这不是 Unity 插件，也不是 Python 模块。把 SDK 放在本目录不会自动接入 Unity 或 `Tools/Python39`。C# 或 Python 如需调用，仍需另外实现原生接口或安装匹配的绑定。

## 保留内容与限制

| 路径 | 用途 |
| --- | --- |
| `include/` | C++ 头文件 |
| `lib/x64/release/libfbxsdk.lib` | DLL 对应的导入库，编译链接时使用；不是完整静态库 |
| `lib/x64/release/libfbxsdk.dll` | 程序运行时需要的动态库 |
| `License.rtf` | Autodesk 许可条款 |
| `README.md` | 本工程的使用说明 |

SDK 约 15.4 MiB。ARM64、Debug 库、静态库、示例和安装附带文件已清理，Git 忽略规则也会排除重新安装后出现的这些内容。需要其他配置时，应从官方发行包补齐并调整忽略规则。

## C++ 工程配置

以工程根目录为基准配置以下项目：

- 目标平台：`x64`；使用本 SDK 的示例工具按 `Release` 构建。
- 包含目录：`Tools/SDK/FBX/include`。
- 预处理器定义：`FBXSDK_SHARED`。
- 库目录：`Tools/SDK/FBX/lib/x64/release`。
- 链接输入：`libfbxsdk.lib`。
- MSVC 运行库：`/MD`；示例启用 `/EHsc`。
- 将 `libfbxsdk.dll` 复制到生成的 EXE 同一目录，或将 DLL 所在目录加入该进程的 `PATH`。

当前仅保留 Release SDK。不要把 Debug 运行库 `/MDd` 当成本配置的默认搭配；需要完整 Debug 集成时重新补齐相应库。运行环境也需要匹配的 x64 MSVC 运行时。

## 最小读取示例

将下面代码保存为临时目录中的 `inspect_fbx.cpp`。它只把 FBX 读入当前进程的内存，不会导入 Unity 工程，也不会保存或覆盖源模型；同时关闭内嵌数据提取，避免在模型旁自动写出贴图。

```cpp
#include <fbxsdk.h>
#include <cstdio>

int main(int argc, char** argv) {
    if (argc != 2) {
        std::fprintf(stderr, "Usage: inspect_fbx <file.fbx>\n");
        return 1;
    }

    FbxManager* manager = FbxManager::Create();
    if (!manager) return 2;

    FbxIOSettings* settings = FbxIOSettings::Create(manager, IOSROOT);
    manager->SetIOSettings(settings);
    settings->SetBoolProp(IMP_FBX_EXTRACT_EMBEDDED_DATA, false);

    FbxImporter* importer = FbxImporter::Create(manager, "");
    if (!importer->Initialize(argv[1], -1, settings)) {
        std::fprintf(stderr, "Initialize: %s\n",
                     importer->GetStatus().GetErrorString());
        manager->Destroy();
        return 3;
    }

    FbxScene* scene = FbxScene::Create(manager, "Inspection");
    if (!importer->Import(scene)) {
        std::fprintf(stderr, "Import: %s\n",
                     importer->GetStatus().GetErrorString());
        manager->Destroy();
        return 4;
    }
    importer->Destroy();

    std::printf("SDK %s: read succeeded; materials=%d, animation stacks=%d\n",
                FBXSDK_VERSION_STRING, scene->GetMaterialCount(),
                scene->GetSrcObjectCount<FbxAnimStack>());
    manager->Destroy(); // 释放场景及其所属对象。
    return 0;
}
```

在 Visual Studio 的 **x64 Native Tools Command Prompt** 中切换到保存示例的临时目录，执行以下命令。这里的绝对路径对应当前机器；其他机器应改为各自的 Veil 路径。

```bat
cl /nologo /utf-8 /EHsc /MD /DFBXSDK_SHARED /I"D:\Projects\Veil\Tools\SDK\FBX\include" inspect_fbx.cpp /link /LIBPATH:"D:\Projects\Veil\Tools\SDK\FBX\lib\x64\release" libfbxsdk.lib
set "PATH=D:\Projects\Veil\Tools\SDK\FBX\lib\x64\release;%PATH%"
inspect_fbx.exe "C:\Users\HZH\Downloads\Ch36_nonPBR.fbx"
```

编译失败时先排查编译器环境、平台和路径；缺少 DLL 时检查 EXE 同目录或进程 `PATH`。上述 `set` 仅影响当前命令提示符，不修改系统环境变量。示例输入路径为当前机器上的测试文件，未纳入仓库；其他使用者需替换为自己的 FBX。

## 读取模型后可访问的信息

- 从 `scene->GetRootNode()` 递归遍历子节点；用 `GetMesh()`、`GetSkeleton()` 识别网格和骨骼。
- 网格控制点数使用 `GetControlPointsCount()`，多边形数使用 `GetPolygonCount()`。控制点数不等于最终渲染顶点数，多边形也不一定是三角形。
- 使用网格的 `GetDeformerCount(FbxDeformer::eSkin)` 获取蒙皮变形器，再读取 `FbxSkin` 的簇和权重。
- 使用 `FbxAnimStack` 读取动画栈及时间范围。存在动画栈并不等于已经验证了动画曲线或播放效果。
- 材质和纹理引用可以从场景中读取；原始纹理路径可能来自其他机器。是否存在内嵌图像、如何提取以及材质如何显示，需要另行处理。
- 实际转换或渲染前，应确认坐标轴、单位、节点变换和蒙皮变换。本示例不自动转换坐标、单位或三角化网格。

## 已完成的验证

2026-09-28，使用本目录的 x64 Release DLL 与导入库编译并运行 C++ 检查程序，成功读取本机 `Ch36_nonPBR.fbx`：

| 项目 | 读取结果 |
| --- | --- |
| FBX 文件版本 | 7.7.0 |
| 网格 | 1 个 |
| 控制点 / 多边形 | 14,442 / 14,466 |
| 骨骼节点 | 65 个 |
| 蒙皮变形器 / 绑定簇 | 1 / 52 |
| 材质 / 纹理引用 | 1 / 4 |
| 动画栈 | `Take 001`（9.080 秒）、`mixamo.com`（0.033 秒） |

验证范围为编译、DLL 加载、FBX 解析及数据访问；不包含贴图显示、动画播放、渲染效果或 Unity 集成。测试模型未导入工程。

## 获取与维护

官方发行包来源：[Autodesk FBX SDK 下载页](https://aps.autodesk.com/developer/overview/fbx-sdk)。版本更新时应成套替换头文件、导入库和 DLL，并重新执行读取验证。许可证以本目录 `License.rtf` 为准。
