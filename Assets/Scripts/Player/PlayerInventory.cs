using UnityEngine;

namespace MinecraftEngine
{
    [System.Serializable]
    public struct ItemStack
    {
        public byte ItemID;
        public byte Amount;

        public ItemStack(byte id, byte amount)
        {
            ItemID = id;
            Amount = amount;
        }

        public bool IsEmpty => ItemID == 0 || Amount == 0;
    }

    public class PlayerInventory : MonoBehaviour
    {
        [Header("Inventory Data")]
        public ItemStack[] hotbar = new ItemStack[9];

        public int selectedSlot = 0;

        private bool _isInitialized = false;

        private void Start()
        {
            for (int i = 0; i < 9; i++)
            {
                hotbar[i] = new ItemStack(0, 0);
            }
        }

        private void Update()
        {
            if (!_isInitialized)
            {
                if (BlockDatabase.Instance != null && BlockDatabase.Instance.NativeBlockData.IsCreated)
                {
                    UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
                    _isInitialized = true;
                }
                return;
            }

            HandleScrollInput();
        }

        private void HandleScrollInput()
        {
            var input = InputManager.Instance;
            if (input == null || !input.IsPlayerMapActive) return;

            // Scroll wheel
            float scroll = input.ScrollHotbar.ReadValue<Vector2>().y;
            if (scroll > 0)
            {
                selectedSlot--;
                if (selectedSlot < 0) selectedSlot = 8;
                UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
            }
            else if (scroll < 0)
            {
                selectedSlot++;
                if (selectedSlot > 8) selectedSlot = 0;
                UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
            }

            // Number keys
            int hotbarKey = input.GetHotbarPressed();
            if (hotbarKey >= 0)
            {
                selectedSlot = hotbarKey;
                UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
            }
        }

        public byte GetSelectedBlockID()
        {
            return hotbar[selectedSlot].ItemID;
        }

        public void RemoveItemFromSelectedSlot()
        {
            if (!hotbar[selectedSlot].IsEmpty)
            {
                hotbar[selectedSlot].Amount--;
                if (hotbar[selectedSlot].Amount <= 0)
                {
                    hotbar[selectedSlot].ItemID = 0;
                }
                UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
            }
        }

        public bool AddItem(byte itemID, int amount)
        {
            for (int i = 0; i < 9; i++)
            {
                if (hotbar[i].ItemID == itemID && hotbar[i].Amount < 64)
                {
                    int spaceLeft = 64 - hotbar[i].Amount;
                    if (amount <= spaceLeft)
                    {
                        hotbar[i].Amount += (byte)amount;
                        UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
                        return true;
                    }
                    else
                    {
                        hotbar[i].Amount = 64;
                        amount -= spaceLeft;
                    }
                }
            }

            if (amount > 0)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (hotbar[i].IsEmpty)
                    {
                        hotbar[i] = new ItemStack(itemID, (byte)amount);
                        UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
