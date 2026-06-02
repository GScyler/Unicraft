using Unity.Mathematics;
using UnityEngine;

namespace MinecraftEngine
{
    public class PlayerController : MonoBehaviour
    {
        public WorldManager worldManager;
        public Camera playerCamera;

        [Header("Debug (F3)")]
        public GameObject debugCameraObject;

        [Header("Movement Settings")]
        public float walkSpeed = 4.317f;
        public float sprintSpeed = 5.612f;
        public float sneakSpeed = 1.295f;

        public float jumpForce = 8.4f;
        public float gravity = -32f;

        [Header("Friction")]
        public float acceleration = 20f;
        public float deceleration = 25f;
        public float airControl = 3f;

        [Header("Camera Settings")]
        public float mouseSensitivity = 0.2f;
        public float standingHeight = 1.62f;
        public float sneakingHeight = 1.54f;

        private float _playerWidth = 0.6f;
        private float _currentPlayerHeight = 1.8f;

        private float _pitch = 0f;
        private float _yaw = 0f;

        private Vector3 _velocity;
        private Vector3 _currentMoveVelocity;

        private bool _isGrounded;
        private bool _isSneaking;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerCamera != null)
            {
                playerCamera.nearClipPlane = 0.01f;
                playerCamera.transform.localPosition = new Vector3(0, standingHeight, 0);
            }
        }

        private void Update()
        {
            var input = InputManager.Instance;
            if (input == null || input.ToggleDebug == null) return;

            if (input.ToggleDebug.WasPressedThisFrame() && debugCameraObject != null)
            {
                ToggleSpectatorMode();
            }

            if (worldManager != null && !worldManager.IsGameStarted) return;
            if (debugCameraObject != null && debugCameraObject.activeSelf) return;

            HandleMouseLook();
            HandleMovement();
        }

        private void ToggleSpectatorMode()
        {
            bool isSpectatorNow = !debugCameraObject.activeSelf;
            Camera debugCamComponent = debugCameraObject.GetComponent<Camera>();

            if (isSpectatorNow)
            {
                debugCameraObject.transform.position = playerCamera.transform.position;
                debugCameraObject.transform.rotation = playerCamera.transform.rotation;
                debugCameraObject.SetActive(true);

                if (debugCamComponent != null) debugCamComponent.enabled = true;

                playerCamera.enabled = false;
                playerCamera.gameObject.SetActive(false);
                GetComponent<PlayerInteraction>().enabled = false;

                worldManager.viewerCamera = debugCamComponent;
                InputManager.Instance.EnableSpectatorMap();
            }
            else
            {
                debugCameraObject.SetActive(false);
                if (debugCamComponent != null) debugCamComponent.enabled = false;

                playerCamera.gameObject.SetActive(true);
                playerCamera.enabled = true;
                GetComponent<PlayerInteraction>().enabled = true;

                worldManager.viewerCamera = playerCamera;
                InputManager.Instance.EnablePlayerMap();

                _pitch = playerCamera.transform.localEulerAngles.x;
                _yaw = transform.eulerAngles.y;
            }
        }

        private void HandleMouseLook()
        {
            var input = InputManager.Instance;
            if (input == null || !input.IsPlayerMapActive) return;

            Vector2 delta = input.Look.ReadValue<Vector2>();

            _yaw += delta.x * mouseSensitivity;
            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89.9f, 89.9f);

            playerCamera.transform.localEulerAngles = new Vector3(_pitch, 0f, 0f);
            transform.eulerAngles = new Vector3(0f, _yaw, 0f);
        }

        private void HandleMovement()
        {
            var input = InputManager.Instance;
            if (input == null || !input.IsPlayerMapActive) return;

            Vector2 moveInput = input.Move.ReadValue<Vector2>();
            float horizontal = moveInput.x;
            float vertical = moveInput.y;

            bool sneakPressed = input.Sneak.IsPressed();
            bool sprintPressed = input.Sprint.IsPressed();
            bool jumpPressed = input.Jump.IsPressed();

            if (sneakPressed)
            {
                _isSneaking = true;
                _currentPlayerHeight = 1.5f;
            }
            else
            {
                if (!CanStandUp())
                {
                    _isSneaking = true;
                }
                else
                {
                    _isSneaking = false;
                    _currentPlayerHeight = 1.8f;
                }
            }

            float targetCamHeight = _isSneaking ? sneakingHeight : standingHeight;
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                new Vector3(0, targetCamHeight, 0),
                Time.deltaTime * 15f
            );

            float targetSpeed = _isSneaking ? sneakSpeed : (sprintPressed ? sprintSpeed : walkSpeed);
            Vector3 targetMoveInput = (transform.right * horizontal + transform.forward * vertical).normalized * targetSpeed;

            float interp = _isGrounded ? (targetMoveInput.sqrMagnitude > 0 ? acceleration : deceleration) : airControl;
            _currentMoveVelocity = Vector3.Lerp(_currentMoveVelocity, targetMoveInput, interp * Time.deltaTime);

            if (_isGrounded)
            {
                _velocity.y = 0f;

                if (jumpPressed)
                {
                    _velocity.y = jumpForce;
                    _isGrounded = false;
                }
            }
            else
            {
                _velocity.y += gravity * Time.deltaTime;
            }

            Vector3 dx = new Vector3(_currentMoveVelocity.x * Time.deltaTime, 0, 0);
            Vector3 dy = new Vector3(0, _velocity.y * Time.deltaTime, 0);
            Vector3 dz = new Vector3(0, 0, _currentMoveVelocity.z * Time.deltaTime);

            if (_isSneaking && _isGrounded)
            {
                if (!HasCollision(transform.position + dx, new Vector3(0, -0.1f, 0), _currentPlayerHeight))
                {
                    dx.x = 0;
                    _currentMoveVelocity.x = 0;
                }
                if (!HasCollision(transform.position + dz, new Vector3(0, -0.1f, 0), _currentPlayerHeight))
                {
                    dz.z = 0;
                    _currentMoveVelocity.z = 0;
                }
            }

            Vector3 currentPos = transform.position;

            if (HasCollision(currentPos + dy, Vector3.zero, _currentPlayerHeight))
            {
                float signY = math.sign(dy.y);
                float step = 0.01f * signY;
                dy.y = 0;

                while (!HasCollision(currentPos + dy + new Vector3(0, step, 0), Vector3.zero, _currentPlayerHeight) && math.abs(dy.y) < math.abs(_velocity.y * Time.deltaTime))
                {
                    dy.y += step;
                }
                _velocity.y = 0;
            }
            currentPos += dy;

            _isGrounded = HasCollision(currentPos, new Vector3(0, -0.05f, 0), _currentPlayerHeight);

            if (HasCollision(currentPos + dx, Vector3.zero, _currentPlayerHeight))
            {
                float signX = math.sign(dx.x);
                float step = 0.01f * signX;
                dx.x = 0;

                while (!HasCollision(currentPos + dx + new Vector3(step, 0, 0), Vector3.zero, _currentPlayerHeight) && math.abs(dx.x) < math.abs(_currentMoveVelocity.x * Time.deltaTime))
                {
                    dx.x += step;
                }
                _currentMoveVelocity.x = 0;
            }
            currentPos += dx;

            if (HasCollision(currentPos + dz, Vector3.zero, _currentPlayerHeight))
            {
                float signZ = math.sign(dz.z);
                float step = 0.01f * signZ;
                dz.z = 0;

                while (!HasCollision(currentPos + dz + new Vector3(0, 0, step), Vector3.zero, _currentPlayerHeight) && math.abs(dz.z) < math.abs(_currentMoveVelocity.z * Time.deltaTime))
                {
                    dz.z += step;
                }
                _currentMoveVelocity.z = 0;
            }
            currentPos += dz;

            transform.position = currentPos;
        }

        private bool CanStandUp()
        {
            float shrink = 0.05f;
            Vector3 pos = transform.position;

            int minX = Mathf.FloorToInt(pos.x - _playerWidth / 2f + shrink);
            int maxX = Mathf.FloorToInt(pos.x + _playerWidth / 2f - shrink);
            int minY = Mathf.FloorToInt(pos.y + 1.51f);
            int maxY = Mathf.FloorToInt(pos.y + 1.8f - shrink);
            int minZ = Mathf.FloorToInt(pos.z - _playerWidth / 2f + shrink);
            int maxZ = Mathf.FloorToInt(pos.z + _playerWidth / 2f - shrink);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    for (int z = minZ; z <= maxZ; z++)
                        if (worldManager.IsSolidBlockAt(new Vector3(x, y, z)))
                            return false;
            return true;
        }

        private bool HasCollision(Vector3 pos, Vector3 offset, float height)
        {
            Vector3 newPos = pos + offset;
            float shrink = 0.05f;

            int minX = Mathf.FloorToInt(newPos.x - _playerWidth / 2f + shrink);
            int maxX = Mathf.FloorToInt(newPos.x + _playerWidth / 2f - shrink);
            int minY = Mathf.FloorToInt(newPos.y);
            int maxY = Mathf.FloorToInt(newPos.y + height - shrink);
            int minZ = Mathf.FloorToInt(newPos.z - _playerWidth / 2f + shrink);
            int maxZ = Mathf.FloorToInt(newPos.z + _playerWidth / 2f - shrink);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    for (int z = minZ; z <= maxZ; z++)
                        if (worldManager.IsSolidBlockAt(new Vector3(x, y, z)))
                            return true;
            return false;
        }

        public bool IsBlockIntersectingPlayer(int3 blockCoord)
        {
            float shrink = 0.05f;
            float toleranceY = 0.1f;

            Vector3 minP = transform.position + new Vector3(-_playerWidth / 2f + shrink, toleranceY, -_playerWidth / 2f + shrink);
            Vector3 maxP = transform.position + new Vector3(_playerWidth / 2f - shrink, _currentPlayerHeight - shrink, _playerWidth / 2f - shrink);

            return (blockCoord.x + 1 > minP.x && blockCoord.x < maxP.x) &&
                   (blockCoord.y + 1 > minP.y && blockCoord.y < maxP.y) &&
                   (blockCoord.z + 1 > minP.z && blockCoord.z < maxP.z);
        }
    }
}
