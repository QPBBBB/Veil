using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Veil.Character.AnimationPlayback
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Character/Animation Player")]
    public sealed class CharacterAnimationPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("Played on enable in Play mode. Looping follows the clip's import settings.")]
        private AnimationClip clip;

        private PlayableGraph graph;

        public AnimationClip Clip => clip;

        private void OnEnable()
        {
            if (Application.isPlaying && clip != null) Play(clip);
        }

        /// <summary>Immediately plays a clip from the start. Does not blend or change clip settings.</summary>
        public void Play(AnimationClip animationClip)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("CharacterAnimationPlayer plays animations only in Play mode.");
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("Enable the animation player before playing a clip.");
            if (animationClip == null) throw new ArgumentNullException(nameof(animationClip));
            if (animationClip.legacy) throw new ArgumentException("Legacy clips are not supported.", nameof(animationClip));

            var animator = GetComponent<Animator>();
            if (animationClip.humanMotion && (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman))
                throw new InvalidOperationException("A valid Humanoid Avatar is required for this animation.");

            Stop();
            clip = animationClip;
            graph = PlayableGraph.Create(name + " Animation");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                var playable = AnimationClipPlayable.Create(graph, animationClip);
                var output = AnimationPlayableOutput.Create(graph, "Character", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
            }
            catch
            {
                Stop();
                throw;
            }
        }

        /// <summary>Releases the playback graph. The assigned clip is retained for the next enable.</summary>
        public void Stop()
        {
            if (graph.IsValid()) graph.Destroy();
            graph = default;
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Stop();
    }
}
