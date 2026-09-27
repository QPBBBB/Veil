using UnityEngine;

namespace CharacterUtilities.SkeletonVisualization
{
    // Attach to the skeleton root itself. The editor draws its descendants while the component is enabled.
    [DisallowMultipleComponent]
    [AddComponentMenu("Character Utilities/Skeleton Visualizer")]
    public sealed class SkeletonVisualizer : MonoBehaviour
    {
        [SerializeField, Tooltip("Show and interact with bones in the Editor Scene view during Play mode.")]
        private bool showInPlayMode = false;
        [SerializeField] private Color color = new Color(1f, 0f, 0f, 1f);
        [SerializeField, Range(0.025f, 0.15f)] private float jointSize = 0.025f;

        public bool ShowInPlayMode => showInPlayMode;
        public Color Color => color;
        public float JointSize => Mathf.Clamp(jointSize, 0.025f, 0.15f);
    }
}
