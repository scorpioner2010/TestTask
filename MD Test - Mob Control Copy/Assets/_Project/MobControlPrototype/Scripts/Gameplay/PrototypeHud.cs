using UnityEngine;
using UnityEngine.UI;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private UnitRunnerManager runnerManager;
        [SerializeField] private FinishTarget finishTarget;
        [SerializeField] private Text unitsLabel;
        [SerializeField] private Text castleLabel;
        [SerializeField] private Text stateLabel;

        private void OnEnable()
        {
            Subscribe();
            RefreshUnits(runnerManager != null ? runnerManager.ActiveCount : 0);

            if (finishTarget != null)
            {
                RefreshCastle(finishTarget.CurrentHealth, finishTarget.MaxHealth);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (runnerManager != null)
            {
                runnerManager.CountChanged += RefreshUnits;
                runnerManager.LevelEnded += HandleLevelEnded;
            }

            if (finishTarget != null)
            {
                finishTarget.HealthChanged += RefreshCastle;
            }
        }

        private void Unsubscribe()
        {
            if (runnerManager != null)
            {
                runnerManager.CountChanged -= RefreshUnits;
                runnerManager.LevelEnded -= HandleLevelEnded;
            }

            if (finishTarget != null)
            {
                finishTarget.HealthChanged -= RefreshCastle;
            }
        }

        private void RefreshUnits(int count)
        {
            if (unitsLabel != null)
            {
                unitsLabel.text = $"Units {count}";
            }
        }

        private void RefreshCastle(int currentHealth, int maxHealth)
        {
            if (castleLabel != null)
            {
                castleLabel.text = $"Castle {Mathf.Max(0, currentHealth)}/{maxHealth}";
            }
        }

        private void HandleLevelEnded(bool success)
        {
            if (stateLabel != null)
            {
                stateLabel.text = success ? "Castle Destroyed" : string.Empty;
            }
        }
    }
}
