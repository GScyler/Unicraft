using UnityEngine;
using UnityEngine.InputSystem;

namespace MinecraftEngine
{
    /// <summary>
    /// Central input manager. Wraps InputActions asset, manages action map switching,
    /// and exposes input state for all scripts. Cross-platform: KB/Mouse, Gamepad, Touch, XR.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [SerializeField] private InputActionAsset inputActions;

        // Action Maps
        private InputActionMap _playerMap;
        private InputActionMap _spectatorMap;
        private InputActionMap _uiMap;

        // === Player Actions ===
        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Sneak { get; private set; }
        public InputAction Attack { get; private set; }
        public InputAction UseItem { get; private set; }
        public InputAction DropItem { get; private set; }
        public InputAction Inventory { get; private set; }
        public InputAction ScrollHotbar { get; private set; }
        public InputAction Hotbar1 { get; private set; }
        public InputAction Hotbar2 { get; private set; }
        public InputAction Hotbar3 { get; private set; }
        public InputAction Hotbar4 { get; private set; }
        public InputAction Hotbar5 { get; private set; }
        public InputAction Hotbar6 { get; private set; }
        public InputAction Hotbar7 { get; private set; }
        public InputAction Hotbar8 { get; private set; }
        public InputAction Hotbar9 { get; private set; }
        public InputAction ToggleDebug { get; private set; }
        public InputAction Pause { get; private set; }

        // === Spectator Actions ===
        public InputAction SpectatorMove { get; private set; }
        public InputAction SpectatorLook { get; private set; }
        public InputAction SpectatorAscend { get; private set; }
        public InputAction SpectatorDescend { get; private set; }
        public InputAction SpectatorSpeedBoost { get; private set; }
        public InputAction SpectatorExit { get; private set; }

        // === State ===
        public bool IsPlayerMapActive => _playerMap != null && _playerMap.enabled;
        public bool IsSpectatorMapActive => _spectatorMap != null && _spectatorMap.enabled;
        public bool IsUIMapActive => _uiMap != null && _uiMap.enabled;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            if (inputActions == null)
            {
                inputActions = Resources.Load<InputActionAsset>("GameInputActions");
            }

            if (inputActions == null)
            {
                // Fallback: ищем любой InputActionAsset в проекте
                var allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                if (allAssets.Length > 0) inputActions = allAssets[0];
            }

            if (inputActions == null)
            {
                Debug.LogError("[InputManager] InputActionAsset not found! Assign it in Inspector or place in Resources/.");
                enabled = false;
                return;
            }

            InitActionMaps();
            EnablePlayerMap();
        }

        private void InitActionMaps()
        {
            _playerMap = inputActions.FindActionMap("Player", true);
            _spectatorMap = inputActions.FindActionMap("Spectator", true);
            _uiMap = inputActions.FindActionMap("UI", true);

            // Player
            Move = _playerMap.FindAction("Move", true);
            Look = _playerMap.FindAction("Look", true);
            Jump = _playerMap.FindAction("Jump", true);
            Sprint = _playerMap.FindAction("Sprint", true);
            Sneak = _playerMap.FindAction("Sneak", true);
            Attack = _playerMap.FindAction("Attack", true);
            UseItem = _playerMap.FindAction("UseItem", true);
            DropItem = _playerMap.FindAction("DropItem", true);
            Inventory = _playerMap.FindAction("Inventory", true);
            ScrollHotbar = _playerMap.FindAction("ScrollHotbar", true);
            Hotbar1 = _playerMap.FindAction("Hotbar1", true);
            Hotbar2 = _playerMap.FindAction("Hotbar2", true);
            Hotbar3 = _playerMap.FindAction("Hotbar3", true);
            Hotbar4 = _playerMap.FindAction("Hotbar4", true);
            Hotbar5 = _playerMap.FindAction("Hotbar5", true);
            Hotbar6 = _playerMap.FindAction("Hotbar6", true);
            Hotbar7 = _playerMap.FindAction("Hotbar7", true);
            Hotbar8 = _playerMap.FindAction("Hotbar8", true);
            Hotbar9 = _playerMap.FindAction("Hotbar9", true);
            ToggleDebug = _playerMap.FindAction("ToggleDebug", true);
            Pause = _playerMap.FindAction("Pause", true);

            // Spectator
            SpectatorMove = _spectatorMap.FindAction("Move", true);
            SpectatorLook = _spectatorMap.FindAction("Look", true);
            SpectatorAscend = _spectatorMap.FindAction("Ascend", true);
            SpectatorDescend = _spectatorMap.FindAction("Descend", true);
            SpectatorSpeedBoost = _spectatorMap.FindAction("SpeedBoost", true);
            SpectatorExit = _spectatorMap.FindAction("Exit", true);
        }

        // === Map Switching ===

        public void EnablePlayerMap()
        {
            _spectatorMap.Disable();
            _uiMap.Disable();
            _playerMap.Enable();
        }

        public void EnableSpectatorMap()
        {
            _playerMap.Disable();
            _uiMap.Disable();
            _spectatorMap.Enable();
        }

        public void EnableUIMap()
        {
            _playerMap.Disable();
            _spectatorMap.Disable();
            _uiMap.Enable();
        }

        /// <summary>
        /// Returns the hotbar slot index (0-8) if any hotbar key was pressed this frame, or -1.
        /// </summary>
        public int GetHotbarPressed()
        {
            if (Hotbar1.WasPressedThisFrame()) return 0;
            if (Hotbar2.WasPressedThisFrame()) return 1;
            if (Hotbar3.WasPressedThisFrame()) return 2;
            if (Hotbar4.WasPressedThisFrame()) return 3;
            if (Hotbar5.WasPressedThisFrame()) return 4;
            if (Hotbar6.WasPressedThisFrame()) return 5;
            if (Hotbar7.WasPressedThisFrame()) return 6;
            if (Hotbar8.WasPressedThisFrame()) return 7;
            if (Hotbar9.WasPressedThisFrame()) return 8;
            return -1;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            inputActions?.Disable();
        }
    }
}
