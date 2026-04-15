using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class UnitBlocker : MonoBehaviour
    {
        [SerializeField, Min(1)] private int health = 12;
        [SerializeField] private TextMesh label;
        [SerializeField] private GameObject visualRoot;

        private int _currentHealth;
        private bool _resolved;

        public int Health => health;

        private void Awake()
        {
            _currentHealth = Mathf.Max(1, health);
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
            RefreshLabel();
        }

        private void OnValidate()
        {
            health = Mathf.Max(1, health);
            if (!Application.isPlaying)
            {
                _currentHealth = health;
            }

            RefreshLabel();
        }

        public bool TryConsume(UnitRunner runner)
        {
            if (_resolved || runner == null || !runner.IsActive)
            {
                return false;
            }

            _currentHealth--;
            runner.Manager.RemoveRunner(runner);

            if (_currentHealth <= 0)
            {
                _resolved = true;
                if (label != null)
                {
                    label.text = "0";
                }

                if (visualRoot != null)
                {
                    visualRoot.SetActive(false);
                }

                Collider trigger = GetComponent<Collider>();
                trigger.enabled = false;
                return true;
            }

            RefreshLabel();
            return true;
        }

        private void RefreshLabel()
        {
            if (label != null)
            {
                label.text = $"-{Mathf.Max(1, _currentHealth)}";
            }
        }
    }
}
