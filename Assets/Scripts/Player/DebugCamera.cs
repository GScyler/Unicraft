using UnityEngine;

namespace MinecraftEngine
{
    /// <summary>
    /// Temporary free-fly camera for testing world generation.
    /// Uses InputManager Spectator actions when available,
    /// falls back to direct input for standalone use.
    /// </summary>
    public class DebugCamera : MonoBehaviour
    {
        public float speed = 50f;
        public float sensitivity = 0.2f;

        private float pitch = 0f;
        private float yaw = 0f;

        void Update()
        {
            var input = InputManager.Instance;
            if (input == null || !input.IsSpectatorMapActive)
            {
                // Fallback: direct input for standalone debug camera
                var mouse = UnityEngine.InputSystem.Mouse.current;
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (mouse == null || kb == null) return;

                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    yaw += delta.x * sensitivity;
                    pitch -= delta.y * sensitivity;
                    pitch = Mathf.Clamp(pitch, -90f, 90f);
                    transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
                }

                float h = 0f, v = 0f, u = 0f;
                if (kb.wKey.isPressed) v += 1f;
                if (kb.sKey.isPressed) v -= 1f;
                if (kb.aKey.isPressed) h -= 1f;
                if (kb.dKey.isPressed) h += 1f;
                if (kb.spaceKey.isPressed) u += 1f;
                if (kb.leftShiftKey.isPressed) u -= 1f;

                Vector3 move = transform.right * h + transform.forward * v + Vector3.up * u;
                transform.position += move * speed * Time.deltaTime;
                return;
            }

            // InputManager-based input
            Vector2 look = input.SpectatorLook.ReadValue<Vector2>();
            yaw += look.x * sensitivity;
            pitch -= look.y * sensitivity;
            pitch = Mathf.Clamp(pitch, -90f, 90f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);

            Vector2 moveInput = input.SpectatorMove.ReadValue<Vector2>();
            float up2 = 0f;
            if (input.SpectatorAscend.IsPressed()) up2 += 1f;
            if (input.SpectatorDescend.IsPressed()) up2 -= 1f;

            float spd = speed;
            if (input.SpectatorSpeedBoost.IsPressed()) spd *= 3f;

            Vector3 mv = transform.right * moveInput.x + transform.forward * moveInput.y + Vector3.up * up2;
            transform.position += mv * spd * Time.deltaTime;
        }
    }
}
