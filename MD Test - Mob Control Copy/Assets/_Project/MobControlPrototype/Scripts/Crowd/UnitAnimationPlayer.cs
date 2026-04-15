using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MobControlPrototype.Crowd
{
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class UnitAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0f)] private float playbackSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float normalizedStartTime;

        private PlayableGraph _graph;

        public void Initialize(AnimationClip animationClip, float speed, float startTime)
        {
            clip = animationClip;
            playbackSpeed = Mathf.Max(0f, speed);
            normalizedStartTime = Mathf.Repeat(startTime, 1f);

            if (isActiveAndEnabled)
            {
                RebuildGraph();
            }
        }

        private void OnEnable()
        {
            RebuildGraph();
        }

        private void OnDisable()
        {
            DestroyGraph();
        }

        private void OnDestroy()
        {
            DestroyGraph();
        }

        private void RebuildGraph()
        {
            if (clip == null)
            {
                return;
            }

            Animator animator = GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            DestroyGraph();

            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create($"{name}_Running");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Running", animator);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(playbackSpeed);

            if (clip.length > 0f)
            {
                playable.SetTime(normalizedStartTime * clip.length);
            }

            output.SetSourcePlayable(playable);
            _graph.Play();
        }

        private void DestroyGraph()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
