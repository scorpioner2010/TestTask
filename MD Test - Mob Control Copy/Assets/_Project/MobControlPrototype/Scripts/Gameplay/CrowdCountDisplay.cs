using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CrowdCountDisplay : MonoBehaviour
    {
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private TextMesh label;
        [SerializeField] private string prefix = "Units: ";

        private void Awake()
        {
            if (runnerManager == null)
            {
                runnerManager = GetComponentInParent<UnitRunnerManager>();
            }

            if (label == null)
            {
                label = GetComponent<TextMesh>();
            }
        }

        private void OnEnable()
        {
            if (runnerManager != null)
            {
                runnerManager.CountChanged += Refresh;
                Refresh(runnerManager.ActiveCount);
            }
        }

        private void Start()
        {
            if (runnerManager != null)
            {
                Refresh(runnerManager.ActiveCount);
            }
        }

        private void OnDisable()
        {
            if (runnerManager != null)
            {
                runnerManager.CountChanged -= Refresh;
            }
        }

        private void Refresh(int count)
        {
            if (label != null)
            {
                label.text = $"{prefix}{count}";
            }
        }
    }
}
