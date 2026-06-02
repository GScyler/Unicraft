using UnityEngine;

namespace MinecraftEngine
{
    public class SpectatorFly : MonoBehaviour
    {
        public Camera spectatorCamera;

        [Header("Flight Settings")]
        public float flySpeed = 20f;
        public float sprintMultiplier = 3f;
        public float mouseSensitivity = 0.2f;

        private float _pitch = 0f;
        private float _yaw = 0f;

        private void Start()
        {
            if (spectatorCamera == null) spectatorCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _pitch = transform.localEulerAngles.x;
            _yaw = transform.localEulerAngles.y;
        }

        private void Update()
        {
            var input = InputManager.Instance;
            if (input == null || !input.IsSpectatorMapActive) return;

            Vector2 delta = input.SpectatorLook.ReadValue<Vector2>();
            _yaw += delta.x * mouseSensitivity;
            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89.9f, 89.9f);

            transform.localEulerAngles = new Vector3(_pitch, _yaw, 0f);

            Vector2 moveInput = input.SpectatorMove.ReadValue<Vector2>();
            float horizontal = moveInput.x;
            float vertical = moveInput.y;
            float up = 0f;

            if (input.SpectatorAscend.IsPressed()) up += 1f;
            if (input.SpectatorDescend.IsPressed()) up -= 1f;

            float currentSpeed = flySpeed;
            if (input.SpectatorSpeedBoost.IsPressed()) currentSpeed *= sprintMultiplier;

            Vector3 move = transform.right * horizontal + transform.forward * vertical + Vector3.up * up;
            transform.position += move * currentSpeed * Time.deltaTime;
        }
    }
}
