using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MinecraftEngine.UI.Interaction;

namespace MinecraftEngine
{
    public class ContainerScreen : MonoBehaviour
    {
        // ─── References ─────────────────────────────────────────────────────────
        private PlayerInventory _inventory;
        private Canvas _canvas;
        private RectTransform _root;
        private RawImage _background;
        private List<SlotView> _slots = new List<SlotView>();

        // ─── Interaction ─────────────────────────────────────────────────────────
        private ContainerInteraction _interaction;

        // ─── Cursor ─────────────────────────────────────────────────────────────
        private RawImage _cursorIcon;
        private TextMeshProUGUI _cursorAmount;
        private RectTransform _cursorRect;

        // ─── GUI constants ───────────────────────────────────────────────────────
        private float _guiScale = 3f;
        private const int GUI_WIDTH = 176;
        private const int GUI_HEIGHT = 166;
        private const int SLOT_SIZE = 16;
        private const int SLOT_STEP = 18;

        // ─── Rendering ───────────────────────────────────────────────────────────
        private TMP_FontAsset _font;
        private int _hoveredSlot = -1;
        private Dictionary<ushort, Texture2D> _iconCache = new Dictionary<ushort, Texture2D>();

        // ─── Debug ──────────────────────────────────────────────────────────────────
        private float _debugUpdateInterval = 0.1f;
        private float _lastDebugUpdate = 0f;

        // Input state tracking to prevent duplicate events
        private bool _lmbPressedThisFrame = false;
        private bool _rmbPressedThisFrame = false;
        private bool _lmbPressedLastFrame = false;
        private bool _rmbPressedLastFrame = false;

        // ─── Lifecycle ───────────────────────────────────────────────────────────
        public static ContainerScreen Create(PlayerInventory inventory, float guiScale = 3f)
        {
            GameObject canvasObj = new GameObject("InventoryCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject overlay = new GameObject("Overlay");
            overlay.transform.SetParent(canvasObj.transform, false);
            Image overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.5f);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            GameObject root = new GameObject("GUIRoot");
            root.transform.SetParent(canvasObj.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();

            ContainerScreen screen = root.AddComponent<ContainerScreen>();
            screen._inventory = inventory;
            screen._guiScale = guiScale;
            screen._canvas = canvas;
            screen._root = rootRect;
            screen._root.anchorMin = new Vector2(0.5f, 0.5f);
            screen._root.anchorMax = new Vector2(0.5f, 0.5f);
            screen._root.pivot = new Vector2(0.5f, 0.5f);
            screen._root.sizeDelta = new Vector2(GUI_WIDTH * guiScale, GUI_HEIGHT * guiScale);

            screen._background = root.AddComponent<RawImage>();
            Texture2D bgTex = LoadGUITexture("inventory");
            if (bgTex != null)
            {
                bgTex.filterMode = FilterMode.Point;
                screen._background.texture = bgTex;
                screen._background.uvRect = new Rect(0, 1f - (GUI_HEIGHT / 256f), GUI_WIDTH / 256f, GUI_HEIGHT / 256f);
            }

            screen._font = UIManager.Instance != null ? UIManager.Instance.regularFont : TMP_Settings.defaultFontAsset;
            screen.CreateCursorDisplay();
            // Debug panel created lazily in Update

            return screen;
        }

        public void InitializeInteraction()
        {
            IInventory invAdapter = new InventoryAdapter(_inventory);
            _interaction = new ContainerInteraction(invAdapter, RefreshAllSlots);
        }

        public void AddPlayerSlots()
        {
            for (int i = 0; i < 9; i++)
                AddSlot(i, new Vector2(8 + i * SLOT_STEP, 142));

            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 9; col++)
                    AddSlot(9 + row * 9 + col, new Vector2(8 + col * SLOT_STEP, 84 + row * SLOT_STEP));

            for (int i = 0; i < 4; i++)
                AddSlot(36 + i, new Vector2(8, 8 + i * SLOT_STEP));

            AddSlot(40, new Vector2(77, 62));

            AddSlot(41, new Vector2(98, 18));
            AddSlot(42, new Vector2(116, 18));
            AddSlot(43, new Vector2(98, 36));
            AddSlot(44, new Vector2(116, 36));

            AddSlot(45, new Vector2(154, 28));

            RefreshAllSlots();
        }

        public void AddSlot(int inventoryIndex, Vector2 guiPixelPos)
        {
            SlotView sv = SlotView.Create(_root.transform, inventoryIndex, guiPixelPos, _guiScale, _font);
            _slots.Add(sv);
        }

        public void Show()
        {
            if (_interaction == null) InitializeInteraction();
            gameObject.SetActive(true);
            _canvas.gameObject.SetActive(true);
            RefreshAllSlots();
        }

        public void Hide()
        {
            _interaction?.ReturnCursor();
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            UpdateCursorPosition();
            UpdateHoveredSlot();
            HandleInput();
            UpdateDebugPanel();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Input Handler — теперь просто диспетчер
        // ═══════════════════════════════════════════════════════════════════════
        private void HandleInput()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse == null || _interaction == null) return;

            // Toggle debug with F3
            if (kb != null && kb.f3Key.wasPressedThisFrame)
                DebugService.Instance.enabled = !DebugService.Instance.enabled;

            bool shift = kb != null && kb.leftShiftKey.isPressed;
            bool ctrl = kb != null && kb.leftCtrlKey.isPressed;
            int hovered = _hoveredSlot >= 0 ? _slots[_hoveredSlot].SlotIndex : -1;

            // Track LMB state
            bool lmbDown = mouse.leftButton.isPressed;
            bool lmbJustPressed = lmbDown && !_lmbPressedLastFrame;
            bool lmbJustReleased = !lmbDown && _lmbPressedLastFrame;

            // Track RMB state
            bool rmbDown = mouse.rightButton.isPressed;
            bool rmbJustPressed = rmbDown && !_rmbPressedLastFrame;
            bool rmbJustReleased = !rmbDown && _rmbPressedLastFrame;

            _lmbPressedLastFrame = lmbDown;
            _rmbPressedLastFrame = rmbDown;

            // Q / Ctrl+Q — always available
            if (kb != null && kb.qKey.wasPressedThisFrame && hovered >= 0)
            {
                DebugService.Instance.Log("Q pressed", "Input");
                _interaction.HandleQKey(hovered, ctrl);
            }

            // Number keys — always available
            if (hovered >= 0)
                HandleNumberKeys(hovered);

            // LMB press
            if (lmbJustPressed && hovered >= 0 && !rmbJustPressed)
            {
                DebugService.Instance.Log($"LMB Press: slot={hovered}, shift={shift}", "Input");
                if (shift)
                {
                    DebugService.Instance.Log("Shift+LMB -> DoShiftClick", "Input");
                    DoShiftClick(hovered);
                }
                else
                    _interaction.OnMousePress(hovered, MouseButton.Left, shift, ctrl);
            }

            // LMB hold (only if LMB is pressed, not RMB)
            if (lmbDown && !rmbDown && hovered >= 0 && hovered != 45)
            {
                _interaction.OnMouseHold(hovered, MouseButton.Left, shift);
            }

            // LMB release
            if (lmbJustReleased)
            {
                DebugService.Instance.Log("LMB Release", "Input");
                _interaction.OnMouseRelease(MouseButton.Left);
            }

            // RMB press
            if (rmbJustPressed && hovered >= 0 && !lmbJustPressed)
            {
                DebugService.Instance.Log($"RMB Press: slot={hovered}, shift={shift}", "Input");
                if (shift)
                {
                    DebugService.Instance.Log("Shift+RMB -> DoShiftClick", "Input");
                    DoShiftClick(hovered);
                }
                else
                    _interaction.OnMousePress(hovered, MouseButton.Right, shift, ctrl);
            }

            // RMB hold (only if RMB is pressed, not LMB)
            if (rmbDown && !lmbDown && hovered >= 0 && hovered != 45)
            {
                _interaction.OnMouseHold(hovered, MouseButton.Right, shift);
            }

            // RMB release
            if (rmbJustReleased)
            {
                DebugService.Instance.Log("RMB Release", "Input");
                _interaction.OnMouseRelease(MouseButton.Right);
            }

            // Scroll wheel
            Vector2 scroll = mouse.scroll.ReadValue();
            if (Mathf.Abs(scroll.y) > 0.01f && hovered >= 0)
            {
                DebugService.Instance.Log($"Scroll: delta={scroll.y}, slot={hovered}", "Input");
                _interaction.OnScroll(hovered, scroll.y);
            }
        }

        private void HandleNumberKeys(int hovered)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            int hotbarSlot = -1;
            if (kb.digit1Key.wasPressedThisFrame) hotbarSlot = 0;
            else if (kb.digit2Key.wasPressedThisFrame) hotbarSlot = 1;
            else if (kb.digit3Key.wasPressedThisFrame) hotbarSlot = 2;
            else if (kb.digit4Key.wasPressedThisFrame) hotbarSlot = 3;
            else if (kb.digit5Key.wasPressedThisFrame) hotbarSlot = 4;
            else if (kb.digit6Key.wasPressedThisFrame) hotbarSlot = 5;
            else if (kb.digit7Key.wasPressedThisFrame) hotbarSlot = 6;
            else if (kb.digit8Key.wasPressedThisFrame) hotbarSlot = 7;
            else if (kb.digit9Key.wasPressedThisFrame) hotbarSlot = 8;

            if (hotbarSlot >= 0)
                _interaction.HandleNumberKey(hovered, hotbarSlot);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shift+Click (delegated to inventory)
        // ═══════════════════════════════════════════════════════════════════════
        private void DoShiftClick(int slotIdx)
        {
            if (slotIdx == 45)
            {
                ItemStack result = _inventory.GetSlot(45);
                if (!result.IsEmpty)
                {
                    if (_inventory.AddItem(result.ItemID, result.Amount))
                        _inventory.SetSlot(45, new ItemStack(0, 0));
                }
                RefreshAllSlots();
                return;
            }

            _inventory.ShiftClickSlot(slotIdx);
            RefreshAllSlots();
        }

        // ─── Cursor management ──────────────────────────────────────────────────
        private void UpdateCursorPosition()
        {
            if (_cursorRect == null) return;

            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(), GetMouseScreenPosition(), null, out mousePos);
            _cursorRect.anchoredPosition = mousePos;

            var cursorStack = _interaction?.Cursor?.Stack ?? new ItemStack(0, 0);

            if (cursorStack.IsEmpty)
            {
                _cursorIcon.color = Color.clear;
                _cursorAmount.text = "";
            }
            else
            {
                _cursorIcon.texture = GetCachedItemIcon(cursorStack.ItemID);
                _cursorIcon.color = Color.white;
                _cursorAmount.text = cursorStack.Amount > 1 ? cursorStack.Amount.ToString() : "";
            }
        }

        private Vector2 GetMouseScreenPosition()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }

        // ─── Hover detection ────────────────────────────────────────────────────
        private void UpdateHoveredSlot()
        {
            if (_hoveredSlot >= 0 && _hoveredSlot < _slots.Count)
                _slots[_hoveredSlot].SetHighlight(false);

            _hoveredSlot = -1;
            Vector2 mouseScreen = GetMouseScreenPosition();

            for (int i = 0; i < _slots.Count; i++)
            {
                RectTransform r = _slots[i].Rect;
                Vector2 localPoint;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, mouseScreen, null, out localPoint))
                    continue;

                Vector2 size = r.sizeDelta;
                Vector2 pivotOffset = new Vector2(r.pivot.x * size.x, r.pivot.y * size.y);
                Rect slotRect = new Rect(-pivotOffset.x, -pivotOffset.y, size.x, size.y);

                if (slotRect.Contains(localPoint))
                {
                    _hoveredSlot = i;
                    _slots[i].SetHighlight(true);
                    break;
                }
            }
        }

        // ─── Refresh ─────────────────────────────────────────────────────────────
        public void RefreshAllSlots()
        {
            foreach (var sv in _slots)
            {
                ItemStack stack = _inventory.GetSlot(sv.SlotIndex);
                Texture2D icon = stack.IsEmpty ? null : GetCachedItemIcon(stack.ItemID);
                sv.UpdateVisual(stack, icon);
            }

            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHotbarUI(_inventory.GetHotbar(), _inventory.selectedSlot);
        }

        private Texture2D GetCachedItemIcon(ushort itemID)
        {
            if (_iconCache.TryGetValue(itemID, out Texture2D cached)) return cached;

            Texture2D rendered = GetItemIcon(itemID);
            if (rendered != null) _iconCache[itemID] = rendered;
            return rendered;
        }

        public static Texture2D GetItemIcon(ushort itemID)
        {
            if (UIManager.Instance != null)
            {
                Texture2D rendered = UIManager.Instance.RenderBlockIcon(itemID);
                if (rendered != null) return rendered;
            }

            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[256];
            Color c = Color.HSVToRGB((itemID * 0.618f) % 1f, 0.5f, 0.8f);
            for (int i = 0; i < 256; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void CreateCursorDisplay()
        {
            GameObject cursorObj = new GameObject("CursorItem");
            cursorObj.transform.SetParent(_canvas.transform, false);

            _cursorRect = cursorObj.AddComponent<RectTransform>();
            _cursorRect.sizeDelta = new Vector2(SLOT_SIZE * _guiScale, SLOT_SIZE * _guiScale);
            _cursorRect.pivot = new Vector2(0.5f, 0.5f);

            Canvas cursorCanvas = cursorObj.AddComponent<Canvas>();
            cursorCanvas.overrideSorting = true;
            cursorCanvas.sortingOrder = 300;
            cursorObj.AddComponent<GraphicRaycaster>();

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cursorObj.transform, false);
            _cursorIcon = iconObj.AddComponent<RawImage>();
            _cursorIcon.color = Color.clear;
            _cursorIcon.raycastTarget = false;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

            GameObject amtObj = new GameObject("Amount");
            amtObj.transform.SetParent(cursorObj.transform, false);
            _cursorAmount = amtObj.AddComponent<TextMeshProUGUI>();
            _cursorAmount.font = _font;
            _cursorAmount.fontSize = 8 * _guiScale;
            _cursorAmount.alignment = TextAlignmentOptions.BottomRight;
            _cursorAmount.color = Color.white;
            _cursorAmount.raycastTarget = false;
            RectTransform amtRect = amtObj.GetComponent<RectTransform>();
            amtRect.anchorMin = Vector2.zero;
            amtRect.anchorMax = Vector2.one;
            amtRect.sizeDelta = Vector2.zero;
        }

        public static Texture2D LoadGUITexture(string name)
        {
            Texture2D tex = Resources.Load<Texture2D>($"GUI/{name}");
            if (tex != null) return tex;

#if UNITY_EDITOR
            string[] paths = new string[]
            {
                $"Assets/Textures/MinecraftTextures/gui/container/{name}.png",
                $"Assets/Textures/minecraft-textures/gui/container/{name}.png",
            };
            foreach (string p in paths)
            {
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (tex != null) return tex;
            }
#endif

            return null;
        }

        private void OnDestroy()
        {
            foreach (var tex in _iconCache.Values)
                if (tex != null) Object.Destroy(tex);

            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void UpdateDebugPanel()
        {
            // Debug panel disabled - using Unity Console instead
            // Press F3 to toggle DebugService logging (logs to Console)
        }
    }
}