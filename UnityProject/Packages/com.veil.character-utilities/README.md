# Character Utilities

Reusable character tools. Package ID: `com.veil.character-utilities`, version `0.1.0`. Requires Unity 6. Skeleton visualization has no dependency on Veil assets, bone names, or rendering pipelines. The animation importer uses a configurable Avatar and external Python / FBX SDK dependencies.

## Structure

```text
Runtime/
  CharacterUtilities.Runtime.asmdef
  Module/SkeletonVisualization/Mono/SkeletonVisualizer.cs
Editor/
  CharacterUtilities.Editor.asmdef
  Module/SkeletonVisualization/SkeletonVisualizerEditor.cs
  Module/CharacterImporter/
    CharacterImporter.cs
    CharacterAnimationImporter.cs
    PythonScripts~/
```

The runtime MonoBehaviour stores visualization settings. It has no Update loop or player rendering code. The Editor assembly contains the Inspector and Gizmo drawing and is excluded from player builds.

## Skeleton visualization

1. On a scene instance or in Prefab Mode, select the skeleton root itself (for example, Bone_Root).
2. Add **Character Utilities > Skeleton Visualizer** to that root, not to the character container.
3. Enable **Gizmos** in the Scene view. Bones remain visible without selecting the character while the component is enabled. Disable the component to hide them.
4. Adjust color and **Joint Size** in the Inspector. The default color is red (RGBA 1, 0, 0, 1). Bones always show through the mesh; there is no occlusion toggle. Parent-child segments are lines; joints are view-scaled spheres. Joint Size has a minimum and default of **0.025**, with a maximum of 0.15. Serialized values are clamped to this range for drawing and picking. Bone names are not drawn.

The component's own Transform is always the skeleton root. There is no root-reference field, root search, visibility toggle or selection-mode toggle. Every Transform below it is drawn, including unweighted end joints and any attachments. No bone naming convention is required.

**Show In Play Mode** defaults to **off**. During Editor Play mode, a component with this toggle off skips skeleton traversal, picking registration and drawing in the Scene view. Enable it when debugging animation in Play mode. This does not enable player-build rendering: all visualization and input code remains Editor-only. The Scene callback still checks for eligible components; disabling Play mode visualization removes the per-bone work, not the callback itself.

## Hover and click selection

- Move the pointer over a joint or bone line in the Scene view to highlight that bone in **yellow**. It returns to the configured color when the pointer leaves.
- Left-click a joint to select its Transform GameObject in the Hierarchy and Inspector.
- Left-click a line to select its **child bone**, the endpoint farther down the hierarchy. The root joint selects the root itself.
- Selection works without first selecting the character. The component must be enabled and Scene view Gizmos must be on.
- Alt navigation, middle/right mouse navigation and Unity transform handles retain their normal interaction. Clicking a bone selects it; it does not move or rotate it.
- Hidden objects and objects with scene picking disabled are excluded from picking. Only skeletons in the current editing stage participate, including Prefab Mode.

Drawing and input run in the Editor module; the runtime MonoBehaviour only holds settings. Hovering and selecting do not apply scene-instance overrides to the Prefab asset.

Drawing does not alter transforms, meshes, Avatar mappings or animation settings. Adding or configuring the component is a normal serialized scene/Prefab edit with Unity Undo support. No component is automatically added to project assets. Copy this package directory into another project's Packages directory to reuse it.

## Character Importer

Code-only animation import lives in `Editor/Module/CharacterImporter`; there is no window or menu. Call `CharacterImporter.Execute` through Unity CLI, then query `GetStatus` until completion. C# invokes the package-local Python converter in `PythonScripts~`, imports its temporary skeleton FBX, extracts an independent `Ani_*.anim`, verifies it and removes the intermediate FBX. Unity ignores the Python directory because its name ends with `~`.

Animation names are configurable. Loop, Root Motion and other clip settings use Unity defaults for the user to adjust after import. Source files and existing outputs are preserved. Python and FBX SDK remain configurable shared dependencies; no player code is added.

See [Character Importer usage](Editor/Module/CharacterImporter/README.md) for exact commands, dependencies, defaults and limitations.
