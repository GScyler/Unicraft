using UnityEngine;

namespace MinecraftEngine
{
    /// <summary>
    /// Full player inventory: 36 main slots (27 main + 9 hotbar),
    /// 4 armor slots, 1 offhand, 4-slot crafting grid (2x2).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        // === Inventory Layout ===
        // mainInventory[0..8]   = hotbar (bottom row, visible in HUD)
        // mainInventory[9..35]  = main inventory (3 rows x 9)
        public ItemStack[] mainInventory = new ItemStack[36];

        // Armor: 0=Helmet, 1=Chestplate, 2=Leggings, 3=Boots
        public ItemStack[] armorSlots = new ItemStack[4];

        // Offhand (shield, totem, map, etc.)
        public ItemStack offhandSlot;

        // 2x2 crafting grid (in player inventory screen)
        public ItemStack[] craftingGrid = new ItemStack[4];
        public ItemStack craftingResult;

        public int selectedSlot = 0; // 0-8 hotbar index

        // === State ===
        public bool IsInventoryOpen { get; private set; } = false;

        private bool _isInitialized = false;

        private void Start()
        {
            for (int i = 0; i < 36; i++)
                mainInventory[i] = new ItemStack(0, 0);
            for (int i = 0; i < 4; i++)
                armorSlots[i] = new ItemStack(0, 0);
            offhandSlot = new ItemStack(0, 0);
            for (int i = 0; i < 4; i++)
                craftingGrid[i] = new ItemStack(0, 0);
            craftingResult = new ItemStack(0, 0);
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                if (BlockDatabase.Instance != null && BlockDatabase.Instance.NativeBlockData.IsCreated)
                {
                    UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
                    _isInitialized = true;
                }
                return;
            }

            // Inventory toggle works in BOTH Player and UI maps
            var input = InputManager.Instance;
            if (input == null) return;

            // Check for close inventory via Escape (Cancel in UI map) or E (re-press)
            if (IsInventoryOpen)
            {
                // UI map is active — listen for Cancel (Esc) or re-check Inventory key via keyboard fallback
                if (UnityEngine.InputSystem.Keyboard.current != null &&
                    (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame ||
                     UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame))
                {
                    ToggleInventory();
                }
                return;
            }

            HandleHotbarInput();
        }

        private void HandleHotbarInput()
        {
            var input = InputManager.Instance;
            if (input == null || !input.IsPlayerMapActive) return;

            // Scroll wheel
            float scroll = input.ScrollHotbar.ReadValue<Vector2>().y;
            if (scroll > 0)
            {
                selectedSlot--;
                if (selectedSlot < 0) selectedSlot = 8;
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            }
            else if (scroll < 0)
            {
                selectedSlot++;
                if (selectedSlot > 8) selectedSlot = 0;
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            }

            // Number keys
            int hotbarKey = input.GetHotbarPressed();
            if (hotbarKey >= 0)
            {
                selectedSlot = hotbarKey;
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            }

            // Toggle inventory (E key)
            if (input.Inventory.WasPressedThisFrame())
            {
                ToggleInventory();
            }
        }

        // ═══════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════

        /// <summary>Returns the 9-slot hotbar array (mainInventory[0..8]).</summary>
        public ItemStack[] GetHotbar()
        {
            ItemStack[] hotbar = new ItemStack[9];
            System.Array.Copy(mainInventory, 0, hotbar, 0, 9);
            return hotbar;
        }

        /// <summary>Returns the ItemID of the selected hotbar slot.</summary>
        public ushort GetSelectedBlockID()
        {
            return mainInventory[selectedSlot].ItemID;
        }

        /// <summary>Returns the selected hotbar ItemStack.</summary>
        public ItemStack GetSelectedItem()
        {
            return mainInventory[selectedSlot];
        }

        /// <summary>Removes 1 item from the selected hotbar slot.</summary>
        public void RemoveItemFromSelectedSlot()
        {
            if (!mainInventory[selectedSlot].IsEmpty)
            {
                mainInventory[selectedSlot].Amount--;
                if (mainInventory[selectedSlot].Amount <= 0)
                {
                    mainInventory[selectedSlot] = new ItemStack(0, 0);
                }
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            }
        }

        /// <summary>
        /// Adds items to inventory. Tries existing stacks first, then empty slots.
        /// Returns true if ALL items were added, false if inventory is full.
        /// </summary>
        public bool AddItem(ushort itemID, int amount)
        {
            int maxStack = 64;
            if (ItemDatabase.Instance != null)
                maxStack = ItemDatabase.Instance.GetMaxStackSize(itemID);

            // 1. Try to merge with existing stacks (hotbar first, then main)
            for (int i = 0; i < 36; i++)
            {
                if (mainInventory[i].ItemID == itemID && mainInventory[i].Amount < maxStack)
                {
                    int spaceLeft = maxStack - mainInventory[i].Amount;
                    if (amount <= spaceLeft)
                    {
                        mainInventory[i].Amount += (byte)amount;
                        UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
                        return true;
                    }
                    else
                    {
                        mainInventory[i].Amount = (byte)maxStack;
                        amount -= spaceLeft;
                    }
                }
            }

            // 2. Find empty slots
            if (amount > 0)
            {
                for (int i = 0; i < 36; i++)
                {
                    if (mainInventory[i].IsEmpty)
                    {
                        int toPlace = Mathf.Min(amount, maxStack);
                        mainInventory[i] = new ItemStack(itemID, (byte)toPlace);
                        amount -= toPlace;
                        if (amount <= 0)
                        {
                            UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
                            return true;
                        }
                    }
                }
            }

            // Partial add — refresh UI anyway
            UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            return amount <= 0;
        }

        // ═══════════════════════════════════════════
        // SLOT OPERATIONS (for UI drag & drop)
        // ═══════════════════════════════════════════

        /// <summary>Get item from any slot index (0-35=main, 36-39=armor, 40=offhand, 41-44=craft grid, 45=craft result).</summary>
        public ItemStack GetSlot(int slotIndex)
        {
            if (slotIndex < 36) return mainInventory[slotIndex];
            if (slotIndex < 40) return armorSlots[slotIndex - 36];
            if (slotIndex == 40) return offhandSlot;
            if (slotIndex < 45) return craftingGrid[slotIndex - 41];
            if (slotIndex == 45) return craftingResult;
            return new ItemStack(0, 0);
        }

        /// <summary>Set item in any slot index.</summary>
        public void SetSlot(int slotIndex, ItemStack stack)
        {
            if (slotIndex < 36) mainInventory[slotIndex] = stack;
            else if (slotIndex < 40) armorSlots[slotIndex - 36] = stack;
            else if (slotIndex == 40) offhandSlot = stack;
            else if (slotIndex < 45) craftingGrid[slotIndex - 41] = stack;
            else if (slotIndex == 45) craftingResult = stack;

            if (slotIndex < 9) // hotbar changed
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
        }

        /// <summary>Swap two slots (for drag & drop).</summary>
        public void SwapSlots(int a, int b)
        {
            ItemStack temp = GetSlot(a);
            SetSlot(a, GetSlot(b));
            SetSlot(b, temp);
        }

        /// <summary>
        /// Try to merge cursorStack into targetSlot.
        /// Returns the remaining cursor stack (may be empty).
        /// </summary>
        public ItemStack TryMergeIntoSlot(int slotIndex, ItemStack cursorStack)
        {
            ItemStack target = GetSlot(slotIndex);

            if (target.IsEmpty)
            {
                SetSlot(slotIndex, cursorStack);
                return new ItemStack(0, 0);
            }

            if (target.ItemID == cursorStack.ItemID)
            {
                int maxStack = 64;
                if (ItemDatabase.Instance != null)
                    maxStack = ItemDatabase.Instance.GetMaxStackSize(target.ItemID);

                int canAdd = maxStack - target.Amount;
                if (canAdd <= 0) return cursorStack; // full

                int toAdd = Mathf.Min(canAdd, cursorStack.Amount);
                target.Amount += (byte)toAdd;
                cursorStack.Amount -= (byte)toAdd;
                SetSlot(slotIndex, target);

                if (cursorStack.Amount <= 0)
                    return new ItemStack(0, 0);
                return cursorStack;
            }

            // Different items — swap
            SetSlot(slotIndex, cursorStack);
            return target;
        }

        /// <summary>
        /// Shift-click: move item from slot to the other section.
        /// Hotbar ↔ Main inventory. Container → Inventory.
        /// </summary>
        public void ShiftClickSlot(int slotIndex)
        {
            ItemStack stack = GetSlot(slotIndex);
            if (stack.IsEmpty) return;

            int targetStart, targetEnd;

            if (slotIndex < 9)
            {
                // Hotbar → Main (9-35)
                targetStart = 9; targetEnd = 36;
            }
            else if (slotIndex < 36)
            {
                // Main → Hotbar (0-8)
                targetStart = 0; targetEnd = 9;
            }
            else
            {
                // Armor/Offhand/Crafting → Hotbar first, then Main
                targetStart = 0; targetEnd = 36;
            }

            int maxStack = 64;
            if (ItemDatabase.Instance != null)
                maxStack = ItemDatabase.Instance.GetMaxStackSize(stack.ItemID);

            // Try merge first
            for (int i = targetStart; i < targetEnd && stack.Amount > 0; i++)
            {
                if (mainInventory[i].ItemID == stack.ItemID && mainInventory[i].Amount < maxStack)
                {
                    int canAdd = maxStack - mainInventory[i].Amount;
                    int toAdd = Mathf.Min(canAdd, stack.Amount);
                    mainInventory[i].Amount += (byte)toAdd;
                    stack.Amount -= (byte)toAdd;
                }
            }

            // Then empty slots
            for (int i = targetStart; i < targetEnd && stack.Amount > 0; i++)
            {
                if (mainInventory[i].IsEmpty)
                {
                    int toPlace = Mathf.Min(stack.Amount, maxStack);
                    mainInventory[i] = new ItemStack(stack.ItemID, (byte)toPlace, stack.Durability);
                    stack.Amount -= (byte)toPlace;
                }
            }

            if (stack.Amount <= 0)
                SetSlot(slotIndex, new ItemStack(0, 0));
            else
                SetSlot(slotIndex, stack);

            UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
        }

        // ═══════════════════════════════════════════
        // INVENTORY TOGGLE
        // ═══════════════════════════════════════════

        private ContainerScreen _containerScreen;

        public void ToggleInventory()
        {
            IsInventoryOpen = !IsInventoryOpen;

            if (IsInventoryOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                InputManager.Instance?.EnableUIMap();

                if (_containerScreen == null)
                {
                    _containerScreen = ContainerScreen.Create(this);
                    _containerScreen.AddPlayerSlots();
                }
                _containerScreen.Show();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                InputManager.Instance?.EnablePlayerMap();

                if (_containerScreen != null)
                    _containerScreen.Hide();

                // Return crafting grid items to inventory
                for (int i = 0; i < 4; i++)
                {
                    if (!craftingGrid[i].IsEmpty)
                    {
                        AddItem(craftingGrid[i].ItemID, craftingGrid[i].Amount);
                        craftingGrid[i] = new ItemStack(0, 0);
                    }
                }
                craftingResult = new ItemStack(0, 0);
            }
        }

        /// <summary>Total number of slots for UI indexing.</summary>
        public const int TotalSlots = 46;
        // 0-8: hotbar, 9-35: main, 36-39: armor, 40: offhand, 41-44: craft grid, 45: craft result
    }
}
