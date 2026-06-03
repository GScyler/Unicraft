using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        // ─── Cursor (floating item under mouse) ─────────────────────────────────
        private ItemStack _cursorStack = new ItemStack(0, 0);
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

        // ═══════════════════════════════════════════════════════════════════════
        // MouseTweaks State Machine
        // ═══════════════════════════════════════════════════════════════════════

        // LMB drag
        private bool _lmbDown = false;
        private int _lmbPickupSlot = -1;        // -1 = cursor from double-click (no redistribution)
        private float _lmbPressTime = 0f;
        private HashSet<int> _lmbTargets = new HashSet<int>();

        // RMB drag
        private bool _rmbDown = false;
        private HashSet<int> _rmbTargets = new HashSet<int>();

        // Double-click guard
        private float _lastClickTime = 0f;
        private int _lastClickSlot = -1;
        private bool _guardClick = false;

        private const float HOLD_THRESHOLD = 0.1f;
        private const float DOUBLE_CLICK_WINDOW = 0.4f;

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

            return screen;
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
            gameObject.SetActive(true);
            _canvas.gameObject.SetActive(true);
            RefreshAllSlots();
        }

        public void Hide()
        {
            ReturnCursorToInventory();
            _canvas.gameObject.SetActive(false);
        }

        // ─── Update ─────────────────────────────────────────────────────────────
        private void Update()
        {
            UpdateCursorPosition();
            UpdateHoveredSlot();
            HandleInput();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Input Handler
        // ═══════════════════════════════════════════════════════════════════════
        private void HandleInput()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (mouse == null) return;

            bool isShift = kb != null && kb.leftShiftKey.isPressed;
            bool isCtrl = kb != null && kb.leftCtrlKey.isPressed;
            int hovered = _hoveredSlot >= 0 ? _slots[_hoveredSlot].SlotIndex : -1;

            // ── Q-drop ──────────────────────────────────────────────────────────
            if (kb != null && kb.qKey.wasPressedThisFrame && hovered >= 0 && !_lmbDown && !_rmbDown)
                DoDrop(hovered, isCtrl);

            // ── Number keys ──────────────────────────────────────────────────────
            if (hovered >= 0 && !_lmbDown && !_rmbDown)
                DoNumberKey(hovered);

            // ── Double-click guard ──────────────────────────────────────────────
            if (mouse.leftButton.wasPressedThisFrame && hovered >= 0 && !_guardClick)
            {
                float now = Time.time;
                if (hovered == _lastClickSlot && (now - _lastClickTime) < DOUBLE_CLICK_WINDOW && !_cursorStack.IsEmpty)
                {
                    DoDoubleClick(hovered);
                    _guardClick = true;
                    _lastClickSlot = -1;
                    _lmbPickupSlot = -1; // cursor from double-click — no redistribution
                }
            }

            // ── LMB press ───────────────────────────────────────────────────────
            if (mouse.leftButton.wasPressedThisFrame && hovered >= 0 && !_guardClick)
            {
                DoLeftMouseDown(hovered, isShift);
                _lastClickTime = Time.time;
                _lastClickSlot = hovered;
            }

            // ── LMB hold ─────────────────────────────────────────────────────────
            if (_lmbDown && hovered >= 0 && hovered != 45)
                DoLeftMouseHold(hovered);

            // ── LMB release ─────────────────────────────────────────────────────
            if (mouse.leftButton.wasReleasedThisFrame)
                DoLeftMouseUp(hovered);

            // ── RMB press ───────────────────────────────────────────────────────
            if (mouse.rightButton.wasPressedThisFrame && hovered >= 0)
                DoRightMouseDown(hovered, isShift);

            // ── RMB hold ─────────────────────────────────────────────────────────
            if (_rmbDown && hovered >= 0 && hovered != 45)
                DoRightMouseHold(hovered);

            // ── RMB release ─────────────────────────────────────────────────────
            if (mouse.rightButton.wasReleasedThisFrame)
                DoRightMouseUp(hovered);

            _guardClick = false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // LMB: Pick up / Place / Redistribute
        // ═══════════════════════════════════════════════════════════════════════

        // LMB press: pick up stack or interact with hovered slot
        private void DoLeftMouseDown(int slotIdx, bool shiftHeld)
        {
            if (shiftHeld)
            {
                _inventory.ShiftClickSlot(slotIdx);
                RefreshAllSlots();
                return;
            }

            if (slotIdx == 45)
            {
                if (_cursorStack.IsEmpty)
                {
                    ItemStack result = _inventory.GetSlot(45);
                    if (!result.IsEmpty)
                    {
                        _cursorStack = result;
                        _inventory.SetSlot(45, new ItemStack(0, 0));
                        RefreshAllSlots();
                    }
                }
                return;
            }

            // Armor slot validation
            if (IsArmorSlot(slotIdx) && !_cursorStack.IsEmpty && !CanEquipArmor(_cursorStack, slotIdx))
                return;

            if (_cursorStack.IsEmpty)
            {
                // ── Pick up entire stack ─────────────────────────────────────────
                ItemStack slotContent = _inventory.GetSlot(slotIdx);
                if (slotContent.IsEmpty) return;

                _inventory.SetSlot(slotIdx, new ItemStack(0, 0));
                _cursorStack = slotContent;

                _lmbDown = true;
                _lmbPickupSlot = slotIdx;  // non-negative → redistribution allowed
                _lmbPressTime = Time.time;
                _lmbTargets.Clear();
                _lmbTargets.Add(slotIdx);
            }
            else
            {
                // ── Cursor has items — interact ──────────────────────────────────
                if (IsArmorSlot(slotIdx) && !CanEquipArmor(_cursorStack, slotIdx))
                    return;

                ItemStack slotContent = _inventory.GetSlot(slotIdx);

                if (slotContent.IsEmpty)
                {
                    // Place entire cursor
                    _inventory.SetSlot(slotIdx, _cursorStack);
                    _cursorStack = new ItemStack(0, 0);
                    _lmbDown = false;
                }
                else if (slotContent.ItemID == _cursorStack.ItemID)
                {
                    // Merge
                    int maxStack = GetMaxStackSize(slotContent.ItemID);
                    if (slotContent.Amount < maxStack)
                    {
                        int space = maxStack - slotContent.Amount;
                        int moved = Mathf.Min(space, _cursorStack.Amount);
                        slotContent.Amount += (byte)moved;
                        _inventory.SetSlot(slotIdx, slotContent);
                        _cursorStack.Amount -= (byte)moved;
                        if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);
                    }
                    else
                    {
                        _cursorStack = _inventory.TryMergeIntoSlot(slotIdx, _cursorStack);
                    }
                }
                else
                {
                    // Swap
                    _cursorStack = _inventory.TryMergeIntoSlot(slotIdx, _cursorStack);
                }
            }

            RefreshAllSlots();
        }

        // LMB hold: if held long enough over new slots → redistribute evenly
        private void DoLeftMouseHold(int slotIdx)
        {
            if (_cursorStack.IsEmpty)
            {
                _lmbDown = false;
                return;
            }

            if (_lmbTargets.Contains(slotIdx)) return;

            bool isHolding = (Time.time - _lmbPressTime) >= HOLD_THRESHOLD;
            bool canRedistribute = isHolding && _lmbPickupSlot >= 0;

            if (IsArmorSlot(slotIdx) && !CanEquipArmor(_cursorStack, slotIdx)) return;

            ItemStack target = _inventory.GetSlot(slotIdx);

            if (target.IsEmpty)
            {
                if (canRedistribute)
                {
                    // Accumulate empty slot and redistribute (cursor decreases)
                    _lmbTargets.Add(slotIdx);
                    RedistributeCursorLMB();
                }
                // else: do nothing (quick release → simple transfer)
            }
            else if (target.ItemID == _cursorStack.ItemID)
            {
                // Fill up to maxStack
                int maxStack = GetMaxStackSize(target.ItemID);
                if (target.Amount < maxStack)
                {
                    int space = maxStack - target.Amount;
                    int moved = Mathf.Min(space, _cursorStack.Amount);
                    target.Amount += (byte)moved;
                    _inventory.SetSlot(slotIdx, target);
                    _cursorStack.Amount -= (byte)moved;
                    if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);
                }
                _lmbTargets.Add(slotIdx);
            }
            else
            {
                _cursorStack = _inventory.TryMergeIntoSlot(slotIdx, _cursorStack);
                _lmbTargets.Add(slotIdx);
                if (_cursorStack.IsEmpty) _lmbDown = false;
            }

            RefreshAllSlots();
        }

        // LMB release: if redistribution was active → finalize; else simple transfer
        private void DoLeftMouseUp(int hovered)
        {
            if (_lmbDown && !_cursorStack.IsEmpty && _lmbPickupSlot >= 0)
            {
                // Check if redistribution was triggered (3+ empty targets)
                int emptyCount = 0;
                foreach (int t in _lmbTargets)
                {
                    if (t == _lmbPickupSlot) continue;
                    if (_inventory.GetSlot(t).IsEmpty) emptyCount++;
                }

                if (emptyCount >= 3)
                {
                    // Redistribution was active — items already distributed, just clear cursor
                    _cursorStack = new ItemStack(0, 0);
                    _lmbPickupSlot = -1;
                    RefreshAllSlots();
                    _lmbDown = false;
                    _lmbTargets.Clear();
                    return;
                }
            }

            // Simple transfer: put cursor in hovered slot (or pickup slot)
            if (!_cursorStack.IsEmpty)
            {
                int target = (hovered >= 0 && hovered != 45) ? hovered : _lmbPickupSlot;
                if (target >= 0)
                {
                    _inventory.SetSlot(target, _cursorStack);
                    _cursorStack = new ItemStack(0, 0);
                }
            }

            _lmbDown = false;
            _lmbPickupSlot = -1;
            _lmbTargets.Clear();
            RefreshAllSlots();
        }

        // Redistribute cursor evenly across all accumulated empty slots
        // Cursor amount decreases by total placed; shows 0 when empty
        private void RedistributeCursorLMB()
        {
            if (_lmbTargets.Count == 0 || _cursorStack.IsEmpty) return;

            int total = _cursorStack.Amount;
            int emptyCount = 0;
            foreach (int t in _lmbTargets)
                if (t != _lmbPickupSlot && _inventory.GetSlot(t).IsEmpty) emptyCount++;

            if (emptyCount == 0) return;

            int perSlot = total / emptyCount;
            int remainder = total % emptyCount;

            int placed = 0;
            int idx = 0;
            foreach (int t in _lmbTargets)
            {
                if (t == _lmbPickupSlot) continue;
                if (!_inventory.GetSlot(t).IsEmpty) continue;

                int amount = perSlot + (idx < remainder ? 1 : 0);
                _inventory.SetSlot(t, new ItemStack(_cursorStack.ItemID, (byte)amount, _cursorStack.Durability));
                placed += amount;
                idx++;
            }

            _cursorStack.Amount -= (byte)placed;
            if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RMB: Pick up half / Place one per slot
        // ═══════════════════════════════════════════════════════════════════════

        private void DoRightMouseDown(int slotIdx, bool shiftHeld)
        {
            if (shiftHeld)
            {
                _inventory.ShiftClickSlot(slotIdx);
                RefreshAllSlots();
                return;
            }

            if (slotIdx == 45)
            {
                if (_cursorStack.IsEmpty)
                {
                    ItemStack result = _inventory.GetSlot(45);
                    if (!result.IsEmpty)
                    {
                        _cursorStack = result;
                        _inventory.SetSlot(45, new ItemStack(0, 0));
                        RefreshAllSlots();
                    }
                }
                return;
            }

            if (IsArmorSlot(slotIdx) && !_cursorStack.IsEmpty && !CanEquipArmor(_cursorStack, slotIdx))
                return;

            if (_cursorStack.IsEmpty)
            {
                // ── Pick up half ─────────────────────────────────────────────────
                ItemStack slotContent = _inventory.GetSlot(slotIdx);
                if (slotContent.IsEmpty) return;

                byte half = (byte)Mathf.CeilToInt(slotContent.Amount / 2f);
                _cursorStack = new ItemStack(slotContent.ItemID, half, slotContent.Durability);
                slotContent.Amount -= half;
                _inventory.SetSlot(slotIdx, slotContent.Amount <= 0 ? new ItemStack(0, 0) : slotContent);

                _rmbDown = true;
                _rmbTargets.Clear();
                _rmbTargets.Add(slotIdx);
            }
            else
            {
                if (IsArmorSlot(slotIdx) && !CanEquipArmor(_cursorStack, slotIdx))
                    return;

                ItemStack target = _inventory.GetSlot(slotIdx);

                if (target.IsEmpty)
                {
                    _inventory.SetSlot(slotIdx, new ItemStack(_cursorStack.ItemID, 1, _cursorStack.Durability));
                    _cursorStack.Amount--;
                    if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);

                    _rmbDown = true;
                    _rmbTargets.Clear();
                    _rmbTargets.Add(slotIdx);
                }
                else if (target.ItemID == _cursorStack.ItemID)
                {
                    int maxStack = GetMaxStackSize(target.ItemID);
                    if (target.Amount < maxStack)
                    {
                        target.Amount++;
                        _inventory.SetSlot(slotIdx, target);
                        _cursorStack.Amount--;
                        if (_cursorStack.Amount <= 0) _cursorStack = new ItemStack(0, 0);

                        _rmbDown = true;
                        _rmbTargets.Clear();
                        _rmbTargets.Add(slotIdx);
                    }
                }
            }

            RefreshAllSlots();
        }

        private void DoRightMouseHold(int slotIdx)
        {
            if (_cursorStack.IsEmpty)
            {
                _rmbDown = false;
                return;
            }

            if (_rmbTargets.Contains(slotIdx)) return;
            if (IsArmorSlot(slotIdx) && !CanEquipArmor(_cursorStack, slotIdx)) return;

            ItemStack target = _inventory.GetSlot(slotIdx);

            if (target.IsEmpty)
            {
                _inventory.SetSlot(slotIdx, new ItemStack(_cursorStack.ItemID, 1, _cursorStack.Durability));
                _cursorStack.Amount--;
                if (_cursorStack.Amount <= 0)
                {
                    _cursorStack = new ItemStack(0, 0);
                    _rmbDown = false;
                }
            }
            else if (target.ItemID == _cursorStack.ItemID)
            {
                int maxStack = GetMaxStackSize(target.ItemID);
                if (target.Amount < maxStack)
                {
                    target.Amount++;
                    _inventory.SetSlot(slotIdx, target);
                    _cursorStack.Amount--;
                    if (_cursorStack.Amount <= 0)
                    {
                        _cursorStack = new ItemStack(0, 0);
                        _rmbDown = false;
                    }
                }
            }
            else
            {
                _cursorStack = _inventory.TryMergeIntoSlot(slotIdx, _cursorStack);
                if (_cursorStack.IsEmpty) _rmbDown = false;
            }

            _rmbTargets.Add(slotIdx);
            RefreshAllSlots();
        }

        private void DoRightMouseUp(int hovered)
        {
            _rmbDown = false;
            _rmbTargets.Clear();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Double-click: collect all matching items
        // ═══════════════════════════════════════════════════════════════════════
        private void DoDoubleClick(int slotIdx)
        {
            if (_cursorStack.IsEmpty) return;

            int maxStack = GetMaxStackSize(_cursorStack.ItemID);
            if (_cursorStack.Amount >= maxStack) return;

            for (int i = 0; i < 46; i++)
            {
                if (i == 45) continue;
                if (i == slotIdx) continue;

                ItemStack s = _inventory.GetSlot(i);
                if (s.ItemID == _cursorStack.ItemID && s.Amount > 0)
                {
                    int space = maxStack - _cursorStack.Amount;
                    int take = Mathf.Min(s.Amount, space);
                    _cursorStack.Amount += (byte)take;
                    s.Amount -= (byte)take;
                    _inventory.SetSlot(i, s.Amount <= 0 ? new ItemStack(0, 0) : s);

                    if (_cursorStack.Amount >= maxStack) break;
                }
            }

            RefreshAllSlots();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Q-drop: throw item in look direction
        // ═══════════════════════════════════════════════════════════════════════
        private void DoDrop(int slotIdx, bool ctrlHeld)
        {
            if (slotIdx == 45) return;

            ItemStack slotItem = _inventory.GetSlot(slotIdx);
            if (slotItem.IsEmpty) return;

            ushort itemID = slotItem.ItemID;
            short durability = slotItem.Durability;
            byte currentAmount = slotItem.Amount;

            if (ItemManager.Instance != null)
            {
                (Vector3 pos, Vector3 vel) = GetDropPositionAndVelocity();

                if (ctrlHeld)
                {
                    for (byte b = 0; b < currentAmount; b++)
                        ItemManager.Instance.SpawnItem(itemID, pos, -1f, vel);
                    _inventory.SetSlot(slotIdx, new ItemStack(0, 0));
                }
                else
                {
                    ItemManager.Instance.SpawnItem(itemID, pos, -1f, vel);
                    byte newAmount = (byte)(currentAmount - 1);
                    _inventory.SetSlot(slotIdx, newAmount > 0 ? new ItemStack(itemID, newAmount, durability) : new ItemStack(0, 0));
                }

                ItemManager.Instance.OnInventoryDrop();
            }

            RefreshAllSlots();
        }

        private (Vector3 position, Vector3 velocity) GetDropPositionAndVelocity()
        {
            Camera cam = Camera.main;
            Vector3 dir;
            Vector3 origin;

            if (cam != null)
            {
                dir = cam.transform.forward;
                origin = cam.transform.position;
            }
            else if (_inventory != null)
            {
                dir = _inventory.transform.forward;
                origin = _inventory.transform.position + Vector3.up * 1.5f;
            }
            else
            {
                dir = Vector3.forward;
                origin = Vector3.zero;
            }

            Vector3 pos = origin + dir * 1.5f;
            Vector3 vel = dir * 3f + Vector3.up * 2f
                + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, UnityEngine.Random.Range(-0.3f, 0.3f));

            return (pos, vel);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Number keys: swap hovered slot with hotbar
        // ═══════════════════════════════════════════════════════════════════════
        private void DoNumberKey(int hovered)
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

            if (hotbarSlot >= 0 && hovered != hotbarSlot)
            {
                _inventory.SwapSlots(hovered, hotbarSlot);
                RefreshAllSlots();
            }
        }

        // ─── Cursor ─────────────────────────────────────────────────────────────
        private void ReturnCursorToInventory()
        {
            if (!_cursorStack.IsEmpty)
            {
                _inventory.AddItem(_cursorStack.ItemID, _cursorStack.Amount);
                _cursorStack = new ItemStack(0, 0);
            }
        }

        private void UpdateCursorPosition()
        {
            if (_cursorRect == null) return;

            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.GetComponent<RectTransform>(), GetMouseScreenPosition(), null, out mousePos);
            _cursorRect.anchoredPosition = mousePos;

            if (_cursorStack.IsEmpty)
            {
                _cursorIcon.color = Color.clear;
                _cursorAmount.text = "";
            }
            else
            {
                _cursorIcon.texture = GetCachedItemIcon(_cursorStack.ItemID);
                _cursorIcon.color = Color.white;
                _cursorAmount.text = _cursorStack.Amount > 1 ? _cursorStack.Amount.ToString() : "";
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

        // ─── Helpers ─────────────────────────────────────────────────────────────
        private bool IsArmorSlot(int slotIdx) => slotIdx >= 36 && slotIdx <= 39;

        private bool CanEquipArmor(ItemStack stack, int targetSlot)
        {
            if (stack.IsEmpty) return true;
            if (!IsArmorSlot(targetSlot)) return true;

            int armorIdx = targetSlot - 36;
            if (ItemDatabase.Instance != null)
            {
                ItemData data = ItemDatabase.Instance.GetItem(stack.ItemID);
                if (data is ArmorData armor)
                    return (int)armor.slot == armorIdx;
                return false;
            }
            return true;
        }

        private int GetMaxStackSize(ushort itemID)
        {
            return ItemDatabase.Instance != null
                ? ItemDatabase.Instance.GetMaxStackSize(itemID) : 64;
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
    }
}