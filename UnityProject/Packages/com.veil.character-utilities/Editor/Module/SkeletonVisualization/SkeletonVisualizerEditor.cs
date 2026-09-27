using System.Collections.Generic;
using CharacterUtilities.SkeletonVisualization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CharacterUtilities.Editor.SkeletonVisualization
{
    [CustomEditor(typeof(SkeletonVisualizer))]
    internal sealed class SkeletonVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Hover a joint or bone to highlight it; left-click to select. A line selects its child bone. Alt navigation is preserved.", MessageType.Info);
        }
    }

    [InitializeOnLoad]
    internal static class SkeletonSceneHandles
    {
        private struct BoneHandle
        {
            public Transform Bone;
            public Transform Parent;
            public SkeletonVisualizer Visualizer;
            public int Id;
        }

        private static readonly List<BoneHandle> HandlesToDraw = new List<BoneHandle>();
        private static int pressedControl;
        internal static Transform HoveredBone { get; private set; }

        static SkeletonSceneHandles()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView view)
        {
            var evt = Event.current;
            if (evt == null) return;
            if (pressedControl != 0 && evt.rawType == EventType.MouseUp && evt.button == 0)
            {
                if (GUIUtility.hotControl == pressedControl) GUIUtility.hotControl = 0;
                pressedControl = 0;
                evt.Use();
            }
            HoveredBone = null;
            if (!view.drawGizmos || view.camera == null) return;
            view.wantsMouseMove = true;
            HandlesToDraw.Clear();
            var stage = StageUtility.GetCurrentStageHandle();
#if UNITY_6000_6_OR_NEWER
            var visualizers = Object.FindObjectsByType<SkeletonVisualizer>(FindObjectsInactive.Exclude);
            // Keep control allocation order stable between layout and input events.
            System.Array.Sort(visualizers, (a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
#else
            var visualizers = Object.FindObjectsByType<SkeletonVisualizer>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
#endif
            foreach (var visualizer in visualizers)
            {
                if (!visualizer.isActiveAndEnabled || EditorUtility.IsPersistent(visualizer)) continue;
                if (EditorApplication.isPlaying && !visualizer.ShowInPlayMode) continue;
                if (StageUtility.GetStageHandle(visualizer.gameObject) != stage) continue;
                if (SceneVisibilityManager.instance.IsHidden(visualizer.gameObject)) continue;
                Collect(visualizer.transform, null, visualizer);
            }

            var oldColor = Handles.color;
            var oldDepth = Handles.zTest;
            var oldMatrix = Handles.matrix;
            try
            {
                Handles.matrix = Matrix4x4.identity;
                var navigating = evt.alt || Tools.viewToolActive || evt.button == 1 || evt.button == 2;
                if ((evt.type == EventType.Layout || evt.type == EventType.MouseMove) && !navigating)
                {
                    foreach (var handle in HandlesToDraw)
                    {
                        if (SceneVisibilityManager.instance.IsPickingDisabled(handle.Bone.gameObject)) continue;
                        var position = handle.Bone.position;
                        if (view.camera.WorldToViewportPoint(position).z <= 0) continue;
                        var radius = HandleUtility.GetHandleSize(position) * handle.Visualizer.JointSize * 0.5f;
                        var distance = HandleUtility.DistanceToCircle(position, radius);
                        if (handle.Parent != null && view.camera.WorldToViewportPoint(handle.Parent.position).z > 0)
                            distance = Mathf.Min(distance, HandleUtility.DistanceToLine(handle.Parent.position, position));
                        // Unity arbitrates these distances against transform handles and other scene tools.
                        if (distance <= 8f) HandleUtility.AddControl(handle.Id, distance);
                    }
                }

                foreach (var handle in HandlesToDraw)
                {
                    var hovered = !navigating && GUIUtility.hotControl == 0 && HandleUtility.nearestControl == handle.Id;
                    if (hovered) HoveredBone = handle.Bone;
                    if (hovered && evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        pressedControl = handle.Id;
                        GUIUtility.hotControl = handle.Id;
                        Selection.activeGameObject = handle.Bone.gameObject;
                        evt.Use();
                        view.Repaint();
                    }
                    if (evt.type != EventType.Repaint) continue;
                    Handles.zTest = CompareFunction.Always;
                    Handles.color = hovered ? Color.yellow : handle.Visualizer.Color;
                    if (handle.Parent != null)
                        Handles.DrawLine(handle.Parent.position, handle.Bone.position, hovered ? 3f : 2f);
                    Handles.SphereHandleCap(0, handle.Bone.position, Quaternion.identity,
                        HandleUtility.GetHandleSize(handle.Bone.position) * handle.Visualizer.JointSize, EventType.Repaint);
                }
                if (evt.type == EventType.MouseMove || evt.type == EventType.MouseLeaveWindow) view.Repaint();
            }
            finally
            {
                Handles.color = oldColor;
                Handles.zTest = oldDepth;
                Handles.matrix = oldMatrix;
            }
        }

        private static void Collect(Transform bone, Transform parent, SkeletonVisualizer visualizer)
        {
            if (!bone.gameObject.activeInHierarchy || SceneVisibilityManager.instance.IsHidden(bone.gameObject)) return;
            HandlesToDraw.Add(new BoneHandle
            {
                Bone = bone,
                Parent = parent,
                Visualizer = visualizer,
                Id = GUIUtility.GetControlID(bone.GetHashCode(), FocusType.Passive)
            });
            foreach (Transform child in bone) Collect(child, bone, visualizer);
        }
    }
}
