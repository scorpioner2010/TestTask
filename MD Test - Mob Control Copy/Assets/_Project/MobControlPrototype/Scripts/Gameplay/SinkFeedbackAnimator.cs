using System;
using System.Collections;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SinkFeedbackAnimator : MonoBehaviour
    {
        [SerializeField] private Transform moveRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.05f)] private float duration = 0.22f;
        [SerializeField, Min(0.1f)] private float sinkDistance = 1.15f;
        [SerializeField, Range(0.1f, 1f)] private float endScaleMultiplier = 0.72f;

        private Coroutine _animationRoutine;
        private Vector3 _baseVisualScale = Vector3.one;
        private bool _hasBaseScale;
        private Vector3 _lastMovePosition;
        private bool _hasMovePosition;

        private void Awake()
        {
            ResolveReferences();
            CacheBaseScale();
        }

        private void OnEnable()
        {
            ResetImmediate();
        }

        private void OnDisable()
        {
            StopAnimation();
            RestoreMoveRoot();
            ResetImmediate();
        }

        public void Play(Action onComplete)
        {
            ResolveReferences();
            CacheBaseScale();
            StopAnimation();
            _animationRoutine = StartCoroutine(PlayRoutine(onComplete));
        }

        public void ResetImmediate()
        {
            ResolveReferences();
            CacheBaseScale();

            if (visualRoot != null)
            {
                visualRoot.localScale = _baseVisualScale;
            }
        }

        private IEnumerator PlayRoutine(Action onComplete)
        {
            Transform motionTarget = moveRoot != null ? moveRoot : transform;
            Transform scaleTarget = visualRoot != null ? visualRoot : motionTarget;

            Vector3 startPosition = motionTarget.position;
            Vector3 startScale = scaleTarget.localScale;
            Vector3 endScale = _baseVisualScale * endScaleMultiplier;
            float elapsed = 0f;

            _lastMovePosition = startPosition;
            _hasMovePosition = true;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);

                motionTarget.position = startPosition + Vector3.down * (sinkDistance * eased);
                scaleTarget.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                yield return null;
            }

            _animationRoutine = null;
            onComplete?.Invoke();
        }

        private void ResolveReferences()
        {
            if (moveRoot == null)
            {
                moveRoot = transform;
            }

            if (visualRoot != null)
            {
                return;
            }

            Renderer renderer = GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                visualRoot = transform;
                return;
            }

            Transform candidate = renderer.transform;
            while (candidate.parent != null && candidate.parent != transform)
            {
                candidate = candidate.parent;
            }

            visualRoot = candidate;
        }

        private void CacheBaseScale()
        {
            if (_hasBaseScale || visualRoot == null)
            {
                return;
            }

            _baseVisualScale = visualRoot.localScale;
            _hasBaseScale = true;
        }

        private void StopAnimation()
        {
            if (_animationRoutine == null)
            {
                return;
            }

            StopCoroutine(_animationRoutine);
            _animationRoutine = null;
        }

        private void RestoreMoveRoot()
        {
            if (!_hasMovePosition || moveRoot == null)
            {
                return;
            }

            moveRoot.position = _lastMovePosition;
        }
    }
}
