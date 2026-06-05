using MinecraftEngine;

namespace MinecraftEngine.UI.Interaction
{
    public class InventoryAdapter : IInventory
    {
        private readonly PlayerInventory _inventory;

        public InventoryAdapter(PlayerInventory inventory)
        {
            _inventory = inventory;
        }

        public ItemStack GetSlot(int slotIdx) => _inventory.GetSlot(slotIdx);
        public void SetSlot(int slotIdx, ItemStack stack) => _inventory.SetSlot(slotIdx, stack);
        public bool AddItem(ushort itemID, int amount) => _inventory.AddItem(itemID, amount);
        public void ShiftClickSlot(int slotIdx) => _inventory.ShiftClickSlot(slotIdx);
        public void SwapSlots(int a, int b) => _inventory.SwapSlots(a, b);

        public int GetMaxStackSize(ushort itemID)
        {
            return ItemDatabase.Instance != null
                ? ItemDatabase.Instance.GetMaxStackSize(itemID) : 64;
        }

        public bool IsArmorSlot(int slotIdx) => slotIdx >= 36 && slotIdx <= 39;

        public bool CanEquipArmor(ItemStack stack, int targetSlot)
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
    }
}