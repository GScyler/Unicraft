using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MinecraftEngine
{
    /// <summary>
    /// Universal container screen renderer — mimics Minecraft's HandledScreen.
    /// Renders a background texture, programmatically places SlotViews,
    /// and handles all mouse interaction (click, shift-click, drag, split).
    ///
    /// Usage:
    ///   var screen = ContainerScreen.Create(inventory, "inventory", guiScale);
    ///   screen.AddPlayerSlots();     // adds 36 main + 4 armor + offhand + crafting
    ///   screen.Show();
    /// </summary>
    public class ContainerScreen : MonoBehaviour
    {
        // === References ===
        private PlayerInventory _inventory;
        private Canvas _canvas;
        private RectTransform _root;         // sized to GUI texture
        private RawImage _background;
        private List<SlotView> _slots = new List<SlotView>();

        // === Cursor item ===
        private ItemStack _cursorStack;
        private RawImage _cursorIcon;
        private TextMeshProUGUI _cursorAmount;
        private RectTransform _cursorRect;

        // === Settings ===
        private float _guiScale = 3f;
        private const int GUI_WIDTH = 176;    // MC inventory texture width
        private const int GUI_HEIGHT = 166;   // MC inventory texture height
        private const int SLOT_SIZE = 16;     // icon size in GUI pixels
        private const int SLOT_STEP = 18;     // slot spacing (16 + 2px border)

        private TMP_FontAsset _font;
        private int _hoveredSlot = -1;

        // === Cached icon renderer (reuses UIManager's system) ===
        private Dictionary<ushort, Texture2D> _iconCache = new Dictionary<ushort, Texture2D>();

        // ══════════════════════════════════════════
        // CREATION
        // ══════════════════════════════════════════

        /// <summary>
        /// Creates and returns a ContainerScreen for the player inventory.
        /// </summary>
        public static ContainerScreen Create(PlayerInventory inventory, float guiScale = 3f)
        {
            // Canvas
            GameObject canvasObj = new GameObject("InventoryCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above HUD
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Darkened overlay
            GameObject overlay = new GameObject("Overlay");
            overlay.transform.SetParent(canvasObj.transform, false);
            Image overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.5f);
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            // GUI root (centered, sized to texture × scale)
            GameObject root = new GameObject("GUIRoot");
            root.transform.SetParent(canvasObj.transform, false);
            // RectTransform must be added before any MonoBehaviour that accesses it
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

            // Background texture
            screen._background = root.AddComponent<RawImage>();
            Texture2D bgTex = LoadGUITexture("inventory");
            if (bgTex != null)
            {
                bgTex.filterMode = FilterMode.Point; // crisp pixels, no blur
                screen._background.texture = bgTex;
                screen._background.uvRect = new Rect(0, 1f - (GUI_HEIGHT / 256f), GUI_WIDTH / 256f, GUI_HEIGHT / 256f);
            }

            // Font
            screen._font = UIManager.Instance != null ? UIManager.Instance.regularFont : TMP_Settings.defaultFontAsset;

            // Cursor item display
            screen.CreateCursorDisplay();

            return screen;
        }

        /// <summary>
        /// Adds all player inventory slots (hotbar, main, armor, offhand, crafting 2x2).
        /// </summary>
        public void AddPlayerSlots()
        {
            // Hotbar: row at y=142, x starts at 8, step 18
            for (int i = 0; i < 9; i++)
                AddSlot(i, new Vector2(8 + i * SLOT_STEP, 142));

            // Main inventory: 3 rows at y=84, 102, 120
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 9; col++)
                    AddSlot(9 + row * 9 + col, new Vector2(8 + col * SLOT_STEP, 84 + row * SLOT_STEP));

            // Armor: (8,8), (8,26), (8,44), (8,62)
            for (int i = 0; i < 4; i++)
                AddSlot(36 + i, new Vector2(8, 8 + i * SLOT_STEP));

            // Offhand: (77,62)
            AddSlot(40, new Vector2(77, 62));

            // Crafting 2x2: (98,18), (116,18), (98,36), (116,36)
            AddSlot(41, new Vector2(98, 18));
            AddSlot(42, new Vector2(116, 18));
            AddSlot(43, new Vector2(98, 36));
            AddSlot(44, new Vector2(116, 36));

            // Crafting result: (154,28)
            AddSlot(45, new Vector2(154, 28));

            RefreshAllSlots();
        }

        /// <summary>Add a slot at MC GUI pixel coordinates.</summary>
        public void AddSlot(int inventoryIndex, Vector2 guiPixelPos)
        {
            SlotView sv = SlotView.Create(_root.transform, inventoryIndex, guiPixelPos, _guiScale, _font);
            _slots.Add(sv);
        }

        // ══════════════════════════════════════════
        // SHOW / HIDE
        // ══════════════════════════════════════════

        public void Show()
        {
            gameObject.SetActive(true);
            _canvas.gameObject.SetActive(true);
            RefreshAllSlots();
        }

        public void Hide()
        {
            // Return cursor item to inventory
            if (!_cursorStack.IsEmpty)
            {
                _inventory.AddItem(_cursorStack.ItemID, _cursorStack.Amount);
                _cursorStack = new ItemStack(0, 0);
            }

            _canvas.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════
        // UPDATE (mouse handling)
        // ══════════════════════════════════════════

        private void Update()
        {
            UpdateCursorPosition();
            UpdateHoveredSlot();
            HandleMouseInput();
        }

        private Vector2 GetMouseScreenPosition()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        }

        private void UpdateCursorPosition()
        {
            if (_cursorRect != null)
            {
                Vector2 mousePos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvas.GetComponent<RectTransform>(), GetMouseScreenPosition(), null, out mousePos);
                _cursorRect.anchoredPosition = mousePos;

                // Update cursor visuals
                if (_cursorStack.IsEmpty)
                {
                    _cursorIcon.color = Color.clear;
                    _cursorAmount.text = "";
                }
                else
                {
                    _cursorIcon.texture = GetItemIcon(_cursorStack.ItemID);
                    _cursorIcon.color = Color.white;
                    _cursorAmount.text = _cursorStack.Amount > 1 ? _cursorStack.Amount.ToString() : "";
                }
            }
        }

        private void UpdateHoveredSlot()
        {
            // Unhighlight previous
            if (_hoveredSlot >= 0 && _hoveredSlot < _slots.Count)
                _slots[_hoveredSlot].SetHighlight(false);

            _hoveredSlot = -1;

            Vector2 mouseScreen = GetMouseScreenPosition();

            for (int i = 0; i < _slots.Count; i++)
            {
                RectTransform r = _slots[i].Rect;

                // Convert mouse screen pos to slot's local space
                Vector2 localPoint;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        r, mouseScreen, null, out localPoint))
                    continue;

                // Check if inside (local space: 0,0 = pivot, size = sizeDelta)
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

        // === Drag state ===
        private bool _isDraggingLeft = false;
        private bool _isDraggingRight = false;
        private List<int> _draggedSlots = new List<int>();
        private ItemStack _dragOriginalCursor;
        private int _dragStartSlot = -1;    // slot where LMB drag started
        private float _lastClickTime = 0f;
        private int _lastClickSlot = -1;

        private void HandleMouseInput()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse == null) return;

            bool isShift = kb != null && kb.leftShiftKey.isPressed;
            bool isCtrl = kb != null && kb.leftCtrlKey.isPressed;
            int hovSlotIdx = _hoveredSlot >= 0 ? _slots[_hoveredSlot].SlotIndex : -1;

            // === Q key: drop item from hovered slot ===
            if (kb != null && kb.qKey.wasPressedThisFrame && hovSlotIdx >= 0)
            {
                ItemStack slotItem = _inventory.GetSlot(hovSlotIdx);
                if (!slotItem.IsEmpty)
                {
                    if (isCtrl)
                    {
                        // Ctrl+Q: drop entire stack
                        if (ItemManager.Instance != null)
                            ItemManager.Instance.SpawnItem(slotItem.ItemID,
                                _inventory.transform.position + Vector3.up);
                        _inventory.SetSlot(hovSlotIdx, new ItemStack(0, 0));
                    }
                    else
                    {
                        // Q: drop 1
                        if (ItemManager.Instance != null)
                            ItemManager.Instance.SpawnItem(slotItem.ItemID,
                                _inventory.transform.position + Vector3.up);
                        slotItem.Amount--;
                        _inventory.SetSlot(hovSlotIdx, slotItem.Amount <= 0 ? new ItemStack(0, 0) : slotItem);
                    }
                    RefreshAllSlots();
                }
            }

            // === Number keys 1-9: swap hovered slot with hotbar ===
            if (hovSlotIdx >= 0 && _cursorStack.IsEmpty)
            {
                int hotbarKey = -1;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) hotbarKey = 0;
                    else if (kb.digit2Key.wasPressedThisFrame) hotbarKey = 1;
                    else if (kb.digit3Key.wasPressedThisFrame) hotbarKey = 2;
                    else if (kb.digit4Key.wasPressedThisFrame) hotbarKey = 3;
                    else if (kb.digit5Key.wasPressedThisFrame) hotbarKey = 4;
                    else if (kb.digit6Key.wasPressedThisFrame) hotbarKey = 5;
                    else if (kb.digit7Key.wasPressedThisFrame) hotbarKey = 6;
                    else if (kb.digit8Key.wasPressedThisFrame) hotbarKey = 7;
                    else if (kb.digit9Key.wasPressedThisFrame) hotbarKey = 8;
                }

                if (hotbarKey >= 0 && hovSlotIdx != hotbarKey)
                {
                    _inventory.SwapSlots(hovSlotIdx, hotbarKey);
                    RefreshAllSlots();
                }
            }

            // === Left click ===
            if (mouse.leftButton.wasPressedThisFrame && hovSlotIdx >= 0)
            {
                // Double-click detection: collect all matching items into cursor
                float now = Time.time;
                if (hovSlotIdx == _lastClickSlot && (now - _lastClickTime) < 0.4f && !_cursorStack.IsEmpty)
                {
                    // Double click: collect all matching items
                    int maxStack = ItemDatabase.Instance != null
                        ? ItemDatabase.Instance.GetMaxStackSize(_cursorStack.ItemID) : 64;

                    for (int i = 0; i < 46 && _cursorStack.Amount < maxStack; i++)
                    {
                        if (i == 45) continue; // skip crafting result
                        ItemStack s = _inventory.GetSlot(i);
                        if (s.ItemID == _cursorStack.ItemID)
                        {
                            int take = Mathf.Min(s.Amount, maxStack - _cursorStack.Amount);
                            _cursorStack.Amount += (byte)take;
                            s.Amount -= (byte)take;
                            _inventory.SetSlot(i, s.Amount <= 0 ? new ItemStack(0, 0) : s);
                        }
                    }
                    _lastClickSlot = -1;
                    RefreshAllSlots();
                    return;
                }

                _lastClickTime = now;
                _lastClickSlot = hovSlotIdx;

                if (isShift)
                {
                    _inventory.ShiftClickSlot(hovSlotIdx);
                }
                else if (hovSlotIdx == 45)
                {
                    ItemStack result = _inventory.GetSlot(45);
                    if (!result.IsEmpty && _cursorStack.IsEmpty)
                    {
                        _cursorStack = result;
                        _inventory.SetSlot(45, new ItemStack(0, 0));
                    }
                }
                else
                {
                    if (_cursorStack.IsEmpty)
                    {
                        _cursorStack = _inventory.GetSlot(hovSlotIdx);
                        _inventory.SetSlot(hovSlotIdx, new ItemStack(0, 0));

                        // Remember where we picked up from — drag distribute 
                        // only activates when mouse moves to a DIFFERENT slot
                        _dragStartSlot = hovSlotIdx;
                        _isDraggingLeft = false; // not yet distributing
                        _draggedSlots.Clear();
                        _dragOriginalCursor = _cursorStack;
                    }
                    else
                    {
                        _cursorStack = _inventory.TryMergeIntoSlot(hovSlotIdx, _cursorStack);
                        _dragStartSlot = -1;
                    }
                }
                RefreshAllSlots();
            }

            // === LMB drag: distribute evenly across dragged slots ===
            // Activate distribute mode only when mouse moves to a different slot while holding LMB
            if (!_isDraggingLeft && _dragStartSlot >= 0 && mouse.leftButton.isPressed
                && hovSlotIdx >= 0 && hovSlotIdx != _dragStartSlot && !_cursorStack.IsEmpty)
            {
                // First move to a different slot — enter distribute mode
                _isDraggingLeft = true;
                _draggedSlots.Clear();
                _dragOriginalCursor = _cursorStack;

                // Add first target slot
                ItemStack target = _inventory.GetSlot(hovSlotIdx);
                if ((target.IsEmpty || target.ItemID == _dragOriginalCursor.ItemID) && hovSlotIdx != 45)
                {
                    _draggedSlots.Add(hovSlotIdx);
                    RedistributeDrag();
                }
            }
            else if (_isDraggingLeft && mouse.leftButton.isPressed && hovSlotIdx >= 0)
            {
                if (!_draggedSlots.Contains(hovSlotIdx) && hovSlotIdx != 45)
                {
                    ItemStack target = _inventory.GetSlot(hovSlotIdx);
                    if (target.IsEmpty || target.ItemID == _dragOriginalCursor.ItemID)
                    {
                        _draggedSlots.Add(hovSlotIdx);
                        RedistributeDrag();
                    }
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                _isDraggingLeft = false;
                _dragStartSlot = -1;
                _draggedSlots.Clear();
            }

            // === Right click ===
            if (mouse.rightButton.wasPressedThisFrame && hovSlotIdx >= 0)
            {
                if (_cursorStack.IsEmpty)
                {
                    // Pick up half
                    ItemStack slotItem = _inventory.GetSlot(hovSlotIdx);
                    if (!slotItem.IsEmpty)
                    {
                        byte half = (byte)Mathf.CeilToInt(slotItem.Amount / 2f);
                        _cursorStack = new ItemStack(slotItem.ItemID, half, slotItem.Durability);
                        slotItem.Amount -= half;
                        _inventory.SetSlot(hovSlotIdx, slotItem.Amount <= 0 ? new ItemStack(0, 0) : slotItem);

                        // Start RMB drag
                        _isDraggingRight = true;
                        _draggedSlots.Clear();
                        _draggedSlots.Add(hovSlotIdx);
                    }
                }
                else
                {
                    // Place 1 item
                    ItemStack slotItem = _inventory.GetSlot(hovSlotIdx);
                    if (slotItem.IsEmpty)
                    {
                        _inventory.SetSlot(hovSlotIdx, new ItemStack(_cursorStack.ItemID, 1, _cursorStack.Durability));
                        _cursorStack.Amount--;
                        if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);

                        _isDraggingRight = true;
                        _draggedSlots.Clear();
                        _draggedSlots.Add(hovSlotIdx);
                    }
                    else if (slotItem.ItemID == _cursorStack.ItemID)
                    {
                        int maxStack = ItemDatabase.Instance != null
                            ? ItemDatabase.Instance.GetMaxStackSize(slotItem.ItemID) : 64;
                        if (slotItem.Amount < maxStack)
                        {
                            slotItem.Amount++;
                            _inventory.SetSlot(hovSlotIdx, slotItem);
                            _cursorStack.Amount--;
                            if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);
                        }
                    }
                }
                RefreshAllSlots();
            }

            // === RMB drag: place 1 item per slot ===
            if (_isDraggingRight && mouse.rightButton.isPressed && hovSlotIdx >= 0 && !_cursorStack.IsEmpty)
            {
                if (!_draggedSlots.Contains(hovSlotIdx) && hovSlotIdx != 45)
                {
                    ItemStack target = _inventory.GetSlot(hovSlotIdx);
                    if (target.IsEmpty)
                    {
                        _inventory.SetSlot(hovSlotIdx, new ItemStack(_cursorStack.ItemID, 1, _cursorStack.Durability));
                        _cursorStack.Amount--;
                        if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);
                        _draggedSlots.Add(hovSlotIdx);
                        RefreshAllSlots();
                    }
                    else if (target.ItemID == _cursorStack.ItemID)
                    {
                        int maxStack = ItemDatabase.Instance != null
                            ? ItemDatabase.Instance.GetMaxStackSize(target.ItemID) : 64;
                        if (target.Amount < maxStack)
                        {
                            target.Amount++;
                            _inventory.SetSlot(hovSlotIdx, target);
                            _cursorStack.Amount--;
                            if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);
                            _draggedSlots.Add(hovSlotIdx);
                            RefreshAllSlots();
                        }
                    }
                }
            }

            if (_isDraggingRight && mouse.rightButton.wasReleasedThisFrame)
            {
                _isDraggingRight = false;
                _draggedSlots.Clear();
            }
        }

        /// <summary>
        /// Redistributes cursor items evenly across all dragged slots (LMB drag behavior).
        /// </summary>
        private void RedistributeDrag()
        {
            if (_draggedSlots.Count == 0 || _dragOriginalCursor.IsEmpty) return;

            int total = _dragOriginalCursor.Amount;
            int perSlot = total / _draggedSlots.Count;
            int remainder = total - perSlot * _draggedSlots.Count;

            if (perSlot <= 0) return;

            foreach (int slotIdx in _draggedSlots)
            {
                _inventory.SetSlot(slotIdx, new ItemStack(_dragOriginalCursor.ItemID, (byte)perSlot, _dragOriginalCursor.Durability));
            }

            _cursorStack = new ItemStack(_dragOriginalCursor.ItemID, (byte)remainder, _dragOriginalCursor.Durability);
            if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);

            RefreshAllSlots();
        }

        // ══════════════════════════════════════════
        // REFRESH VISUALS
        // ══════════════════════════════════════════

        public void RefreshAllSlots()
        {
            foreach (var sv in _slots)
            {
                ItemStack stack = _inventory.GetSlot(sv.SlotIndex);
                Texture2D icon = stack.IsEmpty ? null : GetItemIcon(stack.ItemID);
                sv.UpdateVisual(stack, icon);
            }

            // Also update hotbar HUD
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHotbarUI(_inventory.GetHotbar(), _inventory.selectedSlot);
        }

        // ══════════════════════════════════════════
        // ITEM ICONS (reuses UIManager approach)
        // ══════════════════════════════════════════

        private Texture2D GetItemIcon(ushort itemID)
        {
            if (_iconCache.TryGetValue(itemID, out Texture2D cached)) return cached;

            // Use UIManager's 3D block icon renderer
            if (UIManager.Instance != null)
            {
                Texture2D rendered = UIManager.Instance.RenderBlockIcon(itemID);
                if (rendered != null)
                {
                    _iconCache[itemID] = rendered;
                    return rendered;
                }
            }

            // Fallback: 16x16 colored placeholder
            Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] pixels = new Color[256];
            Color c = Color.HSVToRGB((itemID * 0.618f) % 1f, 0.5f, 0.8f);
            for (int i = 0; i < 256; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();

            _iconCache[itemID] = tex;
            return tex;
        }

        // ══════════════════════════════════════════
        // CURSOR DISPLAY
        // ══════════════════════════════════════════

        private void CreateCursorDisplay()
        {
            GameObject cursorObj = new GameObject("CursorItem");
            cursorObj.transform.SetParent(_canvas.transform, false);

            _cursorRect = cursorObj.AddComponent<RectTransform>();
            _cursorRect.sizeDelta = new Vector2(SLOT_SIZE * _guiScale, SLOT_SIZE * _guiScale);
            _cursorRect.pivot = new Vector2(0.5f, 0.5f);

            // Make sure cursor renders on top
            Canvas cursorCanvas = cursorObj.AddComponent<Canvas>();
            cursorCanvas.overrideSorting = true;
            cursorCanvas.sortingOrder = 300;
            cursorObj.AddComponent<GraphicRaycaster>();

            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cursorObj.transform, false);
            _cursorIcon = iconObj.AddComponent<RawImage>();
            _cursorIcon.color = Color.clear;
            _cursorIcon.raycastTarget = false;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

            // Amount
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

        /// <summary>
        /// Loads a GUI container texture by name. Searches multiple paths.
        /// </summary>
        public static Texture2D LoadGUITexture(string name)
        {
            // 1. Resources/GUI/
            Texture2D tex = Resources.Load<Texture2D>($"GUI/{name}");
            if (tex != null) return tex;

            // 2. Try common texture paths via AssetDatabase (Editor only)
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
    }
}
