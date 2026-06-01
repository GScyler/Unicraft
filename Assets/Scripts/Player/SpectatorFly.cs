using UnityEngine;
using UnityEngine.InputSystem;

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
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (mouse == null || kb == null) return;

            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * mouseSensitivity;
            _pitch -= delta.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89.9f, 89.9f);

            transform.localEulerAngles = new Vector3(_pitch, _yaw, 0f);

            float horizontal = 0f;
            float vertical = 0f;
            float up = 0f;

            if (kb.wKey.isPressed) vertical += 1f;
            if (kb.sKey.isPressed) vertical -= 1f;
            if (kb.aKey.isPressed) horizontal -= 1f;
            if (kb.dKey.isPressed) horizontal += 1f;
            if (kb.spaceKey.isPressed) up += 1f;
            if (kb.leftShiftKey.isPressed) up -= 1f;

            float currentSpeed = flySpeed;
            if (kb.leftCtrlKey.isPressed) currentSpeed *= sprintMultiplier;

            Vector3 move = transform.right * horizontal + transform.forward * vertical + Vector3.up * up;
            transform.position += move * currentSpeed * Time.deltaTime;
        }
    }
}