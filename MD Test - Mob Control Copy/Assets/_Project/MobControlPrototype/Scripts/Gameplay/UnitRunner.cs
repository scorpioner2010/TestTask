using System.Collections.Generic;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class UnitRunner : MonoBehaviour
    {
        private readonly List<int> _passedGateIds = new List<int>(4);
        private UnitRunnerManager _manager;
        private Rigidbody _body;

        public int ActiveIndex { get; set; }
        public bool IsActive { get; private set; }
        public UnitRunnerManager Manager => _manager;
        public Rigidbody Body => _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
        }

        public void Initialize(UnitRunnerManager manager)
        {
            _manager = manager;
            IsActive = true;
            _passedGateIds.Clear();
            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }
        }

        public void Deactivate()
        {
            IsActive = false;
            _passedGateIds.Clear();
        }

        public bool HasPassedGate(int gateId)
        {
            return _passedGateIds.Contains(gateId);
        }

        public void MarkGatePassed(int gateId)
        {
            if (!_passedGateIds.Contains(gateId))
            {
                _passedGateIds.Add(gateId);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActive || _manager == null)
            {
                return;
            }

            GateModifier gate = other.GetComponentInParent<GateModifier>();
            if (gate != null)
            {
                gate.TryApply(this);
                return;
            }

            EnemyMob enemyMob = other.GetComponentInParent<EnemyMob>();
            if (enemyMob != null)
            {
                enemyMob.TryConsume(this);
                return;
            }

            FinishTarget finishTarget = other.GetComponentInParent<FinishTarget>();
            if (finishTarget != null)
            {
                finishTarget.TryDamage(this);
            }
        }
    }
}
