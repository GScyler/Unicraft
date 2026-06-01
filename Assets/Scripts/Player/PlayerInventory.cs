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
            // Убрал стартовые стаки по 64 блока. Теперь вы появляетесь с пустым инвентарем и должны всё добыть!
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
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
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
            }

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { selectedSlot = 0; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit2Key.wasPressedThisFrame) { selectedSlot = 1; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit3Key.wasPressedThisFrame) { selectedSlot = 2; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit4Key.wasPressedThisFrame) { selectedSlot = 3; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit5Key.wasPressedThisFrame) { selectedSlot = 4; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit6Key.wasPressedThisFrame) { selectedSlot = 5; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit7Key.wasPressedThisFrame) { selectedSlot = 6; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit8Key.wasPressedThisFrame) { selectedSlot = 7; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
                if (kb.digit9Key.wasPressedThisFrame) { selectedSlot = 8; UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot); }
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

        // Логика добавления предмета в инвентарь (когда вы его подбираете с земли)
        public bool AddItem(byte itemID, int amount)
        {
            // 1. Пытаемся найти уже существующий неполный стак такого же предмета
            for (int i = 0; i < 9; i++)
            {
                if (hotbar[i].ItemID == itemID && hotbar[i].Amount < 64)
                {
                    // Кладем в этот стак сколько влезет
                    int spaceLeft = 64 - hotbar[i].Amount;
                    if (amount <= spaceLeft)
                    {
                        hotbar[i].Amount += (byte)amount;
                        UIManager.Instance.UpdateHotbarUI(hotbar, selectedSlot);
                        return true; // Предмет полностью поместился
                    }
                    else
                    {
                        hotbar[i].Amount = 64;
                        amount -= spaceLeft; // Остаток попытаемся положить в следующий стак
                    }
                }
            }

            // 2. Если стаков нет или они заполнились, ищем пустой слот
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

            // Инвентарь полностью забит, предмет остается лежать на земле
            return false;
        }
    }
}