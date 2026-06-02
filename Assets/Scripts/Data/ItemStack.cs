namespace MinecraftEngine
{
    /// <summary>
    /// Represents a stack of items in inventory. Lightweight value type.
    /// </summary>
    [System.Serializable]
    public struct ItemStack
    {
        public ushort ItemID;
        public byte Amount;
        public short Durability;   // -1 = no durability (blocks, materials). >= 0 = current durability.

        public ItemStack(ushort id, byte amount, short durability = -1)
        {
            ItemID = id;
            Amount = amount;
            Durability = durability;
        }

        // Backward-compat constructor
        public ItemStack(byte id, byte amount)
        {
            ItemID = id;
            Amount = amount;
            Durability = -1;
        }

        public bool IsEmpty => ItemID == 0 || Amount == 0;

        /// <summary>
        /// Returns max stack size from ItemDatabase, or 64 if not available.
        /// </summary>
        public int MaxStackSize
        {
            get
            {
                if (ItemDatabase.Instance != null)
                    return ItemDatabase.Instance.GetMaxStackSize(ItemID);
                return 64;
            }
        }

        /// <summary>
        /// Returns true if this stack has durability tracking (tools, weapons, armor).
        /// </summary>
        public bool HasDurability => Durability >= 0;

        public override string ToString()
        {
            return IsEmpty ? "Empty" : $"{ItemID}x{Amount}" + (HasDurability ? $" dur:{Durability}" : "");
        }
    }
}
