using UnityEngine;

namespace MinecraftEngine
{
    public class PlayerInventory : MonoBehaviour
    {
        public ItemStack[] mainInventory = new ItemStack[36];
        public ItemStack[] armorSlots = new ItemStack[4];
        public ItemStack offhandSlot;
        public ItemStack[] craftingGrid = new ItemStack[4];
        public ItemStack craftingResult;

        public int selectedSlot = 0;

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

            var input = InputManager.Instance;
            if (input == null) return;

            if (IsInventoryOpen)
            {
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

            int hotbarKey = input.GetHotbarPressed();
            if (hotbarKey >= 0)
            {
                selectedSlot = hotbarKey;
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            }

            if (input.Inventory.WasPressedThisFrame())
            {
                ToggleInventory();
            }
        }

        public ItemStack[] GetHotbar()
        {
            ItemStack[] hotbar = new ItemStack[9];
            System.Array.Copy(mainInventory, 0, hotbar, 0, 9);
            return hotbar;
        }

        public ushort GetSelectedBlockID()
        {
            return mainInventory[selectedSlot].ItemID;
        }

        public ItemStack GetSelectedItem()
        {
            return mainInventory[selectedSlot];
        }

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

        public bool AddItem(ushort itemID, int amount)
        {
            int maxStack = 64;
            if (ItemDatabase.Instance != null)
                maxStack = ItemDatabase.Instance.GetMaxStackSize(itemID);

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

            UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
            return amount <= 0;
        }

        public ItemStack GetSlot(int slotIndex)
        {
            if (slotIndex < 36) return mainInventory[slotIndex];
            if (slotIndex < 40) return armorSlots[slotIndex - 36];
            if (slotIndex == 40) return offhandSlot;
            if (slotIndex < 45) return craftingGrid[slotIndex - 41];
            if (slotIndex == 45) return craftingResult;
            return new ItemStack(0, 0);
        }

        public void SetSlot(int slotIndex, ItemStack stack)
        {
            if (slotIndex < 36) mainInventory[slotIndex] = stack;
            else if (slotIndex < 40) armorSlots[slotIndex - 36] = stack;
            else if (slotIndex == 40) offhandSlot = stack;
            else if (slotIndex < 45) craftingGrid[slotIndex - 41] = stack;
            else if (slotIndex == 45) craftingResult = stack;

            if (slotIndex < 9)
                UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
        }

        public void SwapSlots(int a, int b)
        {
            ItemStack temp = GetSlot(a);
            SetSlot(a, GetSlot(b));
            SetSlot(b, temp);
        }

        public ItemStack TryMergeIntoSlot(int slotIndex, ItemStack cursorStack)
        {
            if (IsArmorSlot(slotIndex))
            {
                ItemStack target = GetSlot(slotIndex);
                if (!target.IsEmpty)
                {
                    return cursorStack;
                }
                if (CanPlaceInArmorSlot(cursorStack, slotIndex))
                {
                    SetSlot(slotIndex, cursorStack);
                    return new ItemStack(0, 0);
                }
                return cursorStack;
            }

            if (slotIndex == 40)
            {
                ItemStack target = GetSlot(40);
                if (!target.IsEmpty && target.ItemID != cursorStack.ItemID)
                {
                    return cursorStack;
                }
            }

            ItemStack targetSlot = GetSlot(slotIndex);

            if (targetSlot.IsEmpty)
            {
                SetSlot(slotIndex, cursorStack);
                return new ItemStack(0, 0);
            }

            if (targetSlot.ItemID == cursorStack.ItemID)
            {
                int maxStack = 64;
                if (ItemDatabase.Instance != null)
                    maxStack = ItemDatabase.Instance.GetMaxStackSize(targetSlot.ItemID);

                int canAdd = maxStack - targetSlot.Amount;
                if (canAdd <= 0) return cursorStack;

                int toAdd = Mathf.Min(canAdd, cursorStack.Amount);
                targetSlot.Amount += (byte)toAdd;
                cursorStack.Amount -= (byte)toAdd;
                SetSlot(slotIndex, targetSlot);

                if (cursorStack.Amount <= 0)
                    return new ItemStack(0, 0);
                return cursorStack;
            }

            SetSlot(slotIndex, cursorStack);
            return targetSlot;
        }

        public void ShiftClickSlot(int slotIndex)
        {
            ItemStack stack = GetSlot(slotIndex);
            if (stack.IsEmpty) return;

            if (IsArmorSlot(slotIndex))
            {
                int targetSlot = GetBestArmorSlot(stack);
                if (targetSlot >= 0)
                {
                    ItemStack existing = GetSlot(targetSlot);
                    if (existing.IsEmpty)
                    {
                        SetSlot(targetSlot, stack);
                        SetSlot(slotIndex, new ItemStack(0, 0));
                    }
                    else
                    {
                        SetSlot(slotIndex, existing);
                        SetSlot(targetSlot, stack);
                    }
                }
                else
                {
                    TryMoveToMainInventory(stack, slotIndex);
                }
                return;
            }

            if (slotIndex == 40)
            {
                TryMoveToMainInventory(stack, slotIndex);
                return;
            }

            if (slotIndex < 9)
            {
                TryMoveToMainInventory(stack, slotIndex);
            }
            else if (slotIndex < 36)
            {
                TryMoveToHotbar(stack, slotIndex);
            }
            else
            {
                TryMoveToMainInventory(stack, slotIndex);
            }
        }

        private void TryMoveToMainInventory(ItemStack stack, int fromSlot)
        {
            int maxStack = 64;
            if (ItemDatabase.Instance != null)
                maxStack = ItemDatabase.Instance.GetMaxStackSize(stack.ItemID);

            for (int i = 9; i < 36 && stack.Amount > 0; i++)
            {
                if (mainInventory[i].ItemID == stack.ItemID && mainInventory[i].Amount < maxStack)
                {
                    int canAdd = maxStack - mainInventory[i].Amount;
                    int toAdd = Mathf.Min(canAdd, stack.Amount);
                    mainInventory[i].Amount += (byte)toAdd;
                    stack.Amount -= (byte)toAdd;
                }
            }

            for (int i = 9; i < 36 && stack.Amount > 0; i++)
            {
                if (mainInventory[i].IsEmpty)
                {
                    int toPlace = Mathf.Min(stack.Amount, maxStack);
                    mainInventory[i] = new ItemStack(stack.ItemID, (byte)toPlace, stack.Durability);
                    stack.Amount -= (byte)toPlace;
                }
            }

            if (stack.Amount <= 0)
                SetSlot(fromSlot, new ItemStack(0, 0));
            else
                SetSlot(fromSlot, stack);

            UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
        }

        private void TryMoveToHotbar(ItemStack stack, int fromSlot)
        {
            int maxStack = 64;
            if (ItemDatabase.Instance != null)
                maxStack = ItemDatabase.Instance.GetMaxStackSize(stack.ItemID);

            for (int i = 0; i < 9 && stack.Amount > 0; i++)
            {
                if (mainInventory[i].ItemID == stack.ItemID && mainInventory[i].Amount < maxStack)
                {
                    int canAdd = maxStack - mainInventory[i].Amount;
                    int toAdd = Mathf.Min(canAdd, stack.Amount);
                    mainInventory[i].Amount += (byte)toAdd;
                    stack.Amount -= (byte)toAdd;
                }
            }

            for (int i = 0; i < 9 && stack.Amount > 0; i++)
            {
                if (mainInventory[i].IsEmpty)
                {
                    int toPlace = Mathf.Min(stack.Amount, maxStack);
                    mainInventory[i] = new ItemStack(stack.ItemID, (byte)toPlace, stack.Durability);
                    stack.Amount -= (byte)toPlace;
                }
            }

            if (stack.Amount <= 0)
                SetSlot(fromSlot, new ItemStack(0, 0));
            else
                SetSlot(fromSlot, stack);

            UIManager.Instance.UpdateHotbarUI(GetHotbar(), selectedSlot);
        }

        private bool IsArmorSlot(int slotIndex)
        {
            return slotIndex >= 36 && slotIndex <= 39;
        }

        private bool CanPlaceInArmorSlot(ItemStack stack, int targetSlot)
        {
            if (stack.IsEmpty) return true;
            if (!IsArmorSlot(targetSlot)) return true;

            int armorSlotIndex = targetSlot - 36;

            if (ItemDatabase.Instance != null)
            {
                ItemData itemData = ItemDatabase.Instance.GetItem(stack.ItemID);
                if (itemData is ArmorData armorData)
                {
                    return (int)armorData.slot == armorSlotIndex;
                }
            }

            return true;
        }

        private int GetBestArmorSlot(ItemStack stack)
        {
            if (ItemDatabase.Instance == null) return -1;

            ItemData itemData = ItemDatabase.Instance.GetItem(stack.ItemID);
            if (itemData is ArmorData armorData)
            {
                return 36 + (int)armorData.slot;
            }

            return -1;
        }

        public ItemStack GetArmor(int slot) => slot >= 0 && slot < 4 ? armorSlots[slot] : new ItemStack(0, 0);
        public ItemStack GetOffhand() => offhandSlot;

        public void RemoveOffhandItem()
        {
            if (!offhandSlot.IsEmpty)
            {
                offhandSlot.Amount--;
                if (offhandSlot.Amount <= 0)
                    offhandSlot = new ItemStack(0, 0);
            }
        }

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

        public const int TotalSlots = 46;
    }
}