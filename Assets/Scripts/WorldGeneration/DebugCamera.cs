using UnityEngine;
using UnityEngine.InputSystem;

namespace MinecraftEngine
{
    // Временная камера для свободного полета и тестирования генерации (Этап 4).
    // Использует New Input System без необходимости создавать Action Maps.
    public class DebugCamera : MonoBehaviour
    {
        public float speed = 50f;
        public float sensitivity = 0.2f;
        
        private float pitch = 0f;
        private float yaw = 0f;

        void Update()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;

            if (mouse == null || kb == null) return;

            // Вращение камерой при зажатой Правой Кнопке Мыши
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch -= delta.y * sensitivity;
                pitch = Mathf.Clamp(pitch, -90f, 90f);
                transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
            }

            // Перемещение (WASD, Space - Вверх, LeftShift - Вниз)
            float horizontal = 0f;
            float vertical = 0f;
            float up = 0f;

            if (kb.wKey.isPressed) vertical += 1f;
            if (kb.sKey.isPressed) vertical -= 1f;
            if (kb.aKey.isPressed) horizontal -= 1f;
            if (kb.dKey.isPressed) horizontal += 1f;
            if (kb.spaceKey.isPressed) up += 1f;
            if (kb.leftShiftKey.isPressed) up -= 1f;

            Vector3 move = transform.right * horizontal + transform.forward * vertical + Vector3.up * up;
            transform.position += move * speed * Time.deltaTime;
        }
    }
}