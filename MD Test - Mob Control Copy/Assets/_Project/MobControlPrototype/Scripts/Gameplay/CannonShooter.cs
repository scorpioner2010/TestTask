using MobControlPrototype.Infrastructure;
using UnityEngine;

namespace MobControlPrototype.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CannonShooter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private UnitRunnerManager runnerManager;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float horizontalSpeed = 5f;
        [SerializeField] private float minX = -2.7f;
        [SerializeField] private float maxX = 2.7f;
        [SerializeField] private bool followMouseWithoutClick = true;
        [SerializeField, Min(0.1f)] private float mouseFollowSharpness = 16f;

        [Header("Shooting")]
        [SerializeField, Min(0.05f)] private float shotsPerSecond = 4f;
        [SerializeField] private KeyCode keyboardFireKey = KeyCode.Space;
        [SerializeField] private Vector3 runnerSpawnOffset = new Vector3(0f, 0.12f, 0.95f);

        private UnityEngine.Camera _camera;
        private float _shotTimer;

        private void Start()
        {
            if (runnerManager == null)
            {
                ServiceLocator.TryGet(out runnerManager);
            }

            if (muzzle == null)
            {
                muzzle = transform;
            }

            _camera = UnityEngine.Camera.main;
        }

        private void Update()
        {
            HandleHorizontalInput(Time.deltaTime);
            HandleShooting(Time.deltaTime);
        }

        private void HandleHorizontalInput(float deltaTime)
        {
            float keyboardInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(keyboardInput) > 0.01f)
            {
                MoveByKeyboard(keyboardInput, deltaTime);
                return;
            }

            if (followMouseWithoutClick)
            {
                MoveTowardMouse(deltaTime);
            }
        }

        private void MoveByKeyboard(float input, float deltaTime)
        {
            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x + input * horizontalSpeed * deltaTime, minX, maxX);
            transform.position = position;
        }

        private void MoveTowardMouse(float deltaTime)
        {
            if (_camera == null)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float distance))
            {
                return;
            }

            float targetX = Mathf.Clamp(ray.GetPoint(distance).x, minX, maxX);
            Vector3 position = transform.position;
            position.x = Mathf.Lerp(position.x, targetX, 1f - Mathf.Exp(-mouseFollowSharpness * deltaTime));
            transform.position = position;
        }

        private void HandleShooting(float deltaTime)
        {
            if (runnerManager == null || runnerManager.IsLevelEnded || shotsPerSecond <= 0f)
            {
                return;
            }

            if (!IsFireHeld())
            {
                _shotTimer = 1f / shotsPerSecond;
                return;
            }

            _shotTimer += deltaTime;
            float interval = 1f / shotsPerSecond;
            while (_shotTimer >= interval)
            {
                _shotTimer -= interval;
                FireSingleRunner();
            }
        }

        private bool IsFireHeld()
        {
            return Input.GetKey(keyboardFireKey) || Input.GetMouseButton(0);
        }

        private void FireSingleRunner()
        {
            Vector3 spawnPosition = muzzle.TransformPoint(runnerSpawnOffset);
            Quaternion spawnRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            runnerManager.FireUnit(spawnPosition, spawnRotation);
        }
    }
}
