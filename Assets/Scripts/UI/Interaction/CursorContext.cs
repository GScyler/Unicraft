using UnityEngine;
using MinecraftEngine;

namespace MinecraftEngine.UI.Interaction
{
    public class CursorContext
    {
        public ItemStack Stack { get; set; } = new ItemStack(0, 0);

        public bool IsEmpty => Stack.IsEmpty;
        public ushort ItemID => Stack.ItemID;
        public byte Amount => Stack.Amount;

        private IInventory _inventory;

        public CursorContext(IInventory inventory)
        {
            _inventory = inventory;
        }

        public int MaxStack => _inventory != null ? _inventory.GetMaxStackSize(ItemID) : 64;

        private void SetStack(ushort id, byte amount, short durability)
        {
            Stack = new ItemStack(id, amount, durability);
        }

        private void SetStack(ItemStack stack)
        {
            Stack = stack;
        }

        private void ModifyAmount(int delta)
        {
            if (Stack.Amount + delta <= 0)
            {
                Stack = new ItemStack(0, 0);
            }
            else
            {
                Stack = new ItemStack(Stack.ItemID, (byte)(Stack.Amount + delta), Stack.Durability);
            }
        }

        public bool TryPickupAll(int slotIdx)
        {
            if (_inventory == null || slotIdx < 0) return false;
            ItemStack slotContent = _inventory.GetSlot(slotIdx);
            if (slotContent.IsEmpty) return false;

            SetStack(slotContent);
            _inventory.SetSlot(slotIdx, new ItemStack(0, 0));
            return true;
        }

        public bool TryPickupHalf(int slotIdx)
        {
            if (_inventory == null || slotIdx < 0) return false;
            ItemStack slotContent = _inventory.GetSlot(slotIdx);
            if (slotContent.IsEmpty) return false;

            byte half = (byte)UnityEngine.Mathf.CeilToInt(slotContent.Amount / 2f);
            SetStack(slotContent.ItemID, half, slotContent.Durability);
            slotContent.Amount -= half;
            _inventory.SetSlot(slotIdx, slotContent.Amount <= 0 ? new ItemStack(0, 0) : slotContent);
            return true;
        }

        public bool TryPickupOne(int slotIdx)
        {
            if (_inventory == null || slotIdx < 0) return false;
            ItemStack slotContent = _inventory.GetSlot(slotIdx);
            if (slotContent.IsEmpty) return false;

            SetStack(slotContent.ItemID, 1, slotContent.Durability);
            slotContent.Amount--;
            _inventory.SetSlot(slotIdx, slotContent.Amount <= 0 ? new ItemStack(0, 0) : slotContent);
            return true;
        }

        public bool TryPlaceOne(int slotIdx)
        {
            if (_inventory == null || IsEmpty || slotIdx < 0) return false;
            if (_inventory.IsArmorSlot(slotIdx) && !_inventory.CanEquipArmor(Stack, slotIdx)) return false;

            ItemStack target = _inventory.GetSlot(slotIdx);
            int maxStack = MaxStack;

            if (target.IsEmpty)
            {
                _inventory.SetSlot(slotIdx, new ItemStack(ItemID, 1, Stack.Durability));
                ModifyAmount(-1);
                return true;
            }
            else if (target.ItemID == ItemID && target.Amount < maxStack)
            {
                target.Amount++;
                _inventory.SetSlot(slotIdx, target);
                ModifyAmount(-1);
                return true;
            }
            return false;
        }

        public bool TryMerge(int slotIdx)
        {
            if (_inventory == null || IsEmpty || slotIdx < 0) return false;
            if (_inventory.IsArmorSlot(slotIdx) && !_inventory.CanEquipArmor(Stack, slotIdx)) return false;

            ItemStack target = _inventory.GetSlot(slotIdx);

            if (target.IsEmpty)
            {
                _inventory.SetSlot(slotIdx, Stack);
                SetStack(0, 0, 0);
                return true;
            }
            else if (target.ItemID == ItemID)
            {
                int maxStack = MaxStack;
                if (target.Amount < maxStack)
                {
                    int space = maxStack - target.Amount;
                    int moved = UnityEngine.Mathf.Min(space, Amount);
                    target.Amount += (byte)moved;
                    _inventory.SetSlot(slotIdx, target);
                    ModifyAmount(-moved);
                    return true;
                }
            }
            return false;
        }

        public void ReturnToInventory()
        {
            if (_inventory == null || IsEmpty) return;
            _inventory.AddItem(ItemID, Amount);
            SetStack(0, 0, 0);
        }

        public void Clear()
        {
            SetStack(0, 0, 0);
        }
    }
}