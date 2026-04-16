using System;
using System.Collections;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class SinkFeedbackAnimator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Transform moveRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.05f)] private float duration = 0.22f;
        [SerializeField, Min(0.1f)] private float sinkDistance = 1.15f;
        [SerializeField, Range(0.1f, 1f)] private float endScaleMultiplier = 0.72f;
        [SerializeField] private Color deathTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        private Coroutine _animationRoutine;
        private Vector3 _baseVisualScale = Vector3.one;
        private bool _hasBaseScale;
        private Vector3 _lastMovePosition;
        private bool _hasMovePosition;
        private MaterialPropertyBlock _propertyBlock;
        private TintTarget[] _tintTargets = Array.Empty<TintTarget>();

        private struct TintTarget
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public int ColorPropertyId;
            public Color CachedColor;
            public bool HasCachedColor;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheBaseScale();
            CacheTintTargets();
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
            CacheTintTargets();
            CacheCurrentTint();
            ApplyTint(deathTint);
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

            RestoreTint();
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

        private void CacheTintTargets()
        {
            if (_tintTargets.Length > 0)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            int targetCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null || GetColorPropertyId(material) == -1)
                    {
                        continue;
                    }

                    targetCount++;
                }
            }

            if (targetCount == 0)
            {
                _tintTargets = Array.Empty<TintTarget>();
                return;
            }

            _tintTargets = new TintTarget[targetCount];
            int targetIndex = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    int colorPropertyId = material != null ? GetColorPropertyId(material) : -1;
                    if (colorPropertyId == -1)
                    {
                        continue;
                    }

                    _tintTargets[targetIndex++] = new TintTarget
                    {
                        Renderer = renderer,
                        MaterialIndex = materialIndex,
                        ColorPropertyId = colorPropertyId
                    };
                }
            }
        }

        private void CacheCurrentTint()
        {
            if (_tintTargets.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < _tintTargets.Length; i++)
            {
                TintTarget target = _tintTargets[i];
                if (target.Renderer == null)
                {
                    continue;
                }

                Color materialColor = GetMaterialColor(target);
                target.CachedColor = GetRendererColor(target, materialColor);
                target.HasCachedColor = true;
                _tintTargets[i] = target;
            }
        }

        private void ApplyTint(Color tint)
        {
            if (_tintTargets.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < _tintTargets.Length; i++)
            {
                TintTarget target = _tintTargets[i];
                if (target.Renderer == null)
                {
                    continue;
                }

                target.Renderer.GetPropertyBlock(_propertyBlock, target.MaterialIndex);
                _propertyBlock.SetColor(target.ColorPropertyId, tint);
                target.Renderer.SetPropertyBlock(_propertyBlock, target.MaterialIndex);
            }
        }

        private void RestoreTint()
        {
            if (_tintTargets.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < _tintTargets.Length; i++)
            {
                TintTarget target = _tintTargets[i];
                if (target.Renderer == null || !target.HasCachedColor)
                {
                    continue;
                }

                target.Renderer.GetPropertyBlock(_propertyBlock, target.MaterialIndex);
                _propertyBlock.SetColor(target.ColorPropertyId, target.CachedColor);
                target.Renderer.SetPropertyBlock(_propertyBlock, target.MaterialIndex);
            }
        }

        private Color GetRendererColor(TintTarget target, Color fallbackColor)
        {
            target.Renderer.GetPropertyBlock(_propertyBlock, target.MaterialIndex);
            if (_propertyBlock.isEmpty)
            {
                return fallbackColor;
            }

            Color blockColor = _propertyBlock.GetColor(target.ColorPropertyId);
            return HasUsableColor(blockColor) ? blockColor : fallbackColor;
        }

        private Color GetMaterialColor(TintTarget target)
        {
            Material[] materials = target.Renderer.sharedMaterials;
            if (target.MaterialIndex < 0 || target.MaterialIndex >= materials.Length)
            {
                return deathTint;
            }

            Material material = materials[target.MaterialIndex];
            if (material == null || !material.HasProperty(target.ColorPropertyId))
            {
                return deathTint;
            }

            return material.GetColor(target.ColorPropertyId);
        }

        private static int GetColorPropertyId(Material material)
        {
            if (material.HasProperty(BaseColorId))
            {
                return BaseColorId;
            }

            return material.HasProperty(ColorId) ? ColorId : -1;
        }

        private static bool HasUsableColor(Color color)
        {
            return !Mathf.Approximately(color.r, 0f)
                || !Mathf.Approximately(color.g, 0f)
                || !Mathf.Approximately(color.b, 0f)
                || !Mathf.Approximately(color.a, 0f);
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
