using MinecraftEngine;

namespace MinecraftEngine.UI.Interaction
{
    public interface IInventory
    {
        ItemStack GetSlot(int slotIdx);
        void SetSlot(int slotIdx, ItemStack stack);
        bool AddItem(ushort itemID, int amount);
        void ShiftClickSlot(int slotIdx);
        void SwapSlots(int a, int b);
        int GetMaxStackSize(ushort itemID);
        bool IsArmorSlot(int slotIdx);
        bool CanEquipArmor(ItemStack stack, int targetSlot);
    }
}