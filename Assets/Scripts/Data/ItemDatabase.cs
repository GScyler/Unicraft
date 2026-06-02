using System.Collections.Generic;
using UnityEngine;

namespace MinecraftEngine
{
    /// <summary>
    /// Central registry of all items. Loads from Resources/Items/ at runtime.
    /// Auto-generates block items from BlockDatabase for blocks without explicit ItemData.
    /// </summary>
    public class ItemDatabase : MonoBehaviour
    {
        public static ItemDatabase Instance;

        private Dictionary<ushort, ItemData> _items = new Dictionary<ushort, ItemData>();

        public void Initialize()
        {
            Instance = this;
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            _items.Clear();

            // 1. Load explicit item SOs from Resources/Items/
            ItemData[] loadedItems = Resources.LoadAll<ItemData>("Items");
            foreach (ItemData item in loadedItems)
            {
                if (!_items.ContainsKey(item.itemID))
                {
                    _items.Add(item.itemID, item);
                }
                else
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate item ID {item.itemID}: {item.itemName}");
                }
            }

            // 2. Auto-generate block items for every block that doesn't have an explicit item
            if (BlockDatabase.Instance != null)
            {
                foreach (BlockType bt in System.Enum.GetValues(typeof(BlockType)))
                {
                    ushort id = (ushort)bt;
                    if (id == 0) continue; // skip Air
                    if (_items.ContainsKey(id)) continue; // explicit item exists

                    BlockData block = BlockDatabase.Instance.GetBlock(id);
                    if (block == null) continue;

                    // Create runtime-only ItemData for this block
                    ItemData autoItem = ScriptableObject.CreateInstance<ItemData>();
                    autoItem.itemID = id;
                    autoItem.itemName = block.blockName;
                    autoItem.type = ItemType.Block;
                    autoItem.maxStackSize = 64;
                    autoItem.blockToPlace = id;
                    autoItem.attackDamage = 1f;
                    autoItem.attackSpeed = 4f;

                    _items.Add(id, autoItem);
                }
            }

            Debug.Log($"[ItemDatabase] Loaded {_items.Count} items ({loadedItems.Length} explicit + {_items.Count - loadedItems.Length} auto-generated block items)");
        }

        public ItemData GetItem(ushort id)
        {
            if (_items.TryGetValue(id, out ItemData data)) return data;
            return null;
        }

        /// <summary>
        /// Returns the max stack size for an item. Defaults to 64 if item not found.
        /// </summary>
        public int GetMaxStackSize(ushort id)
        {
            if (_items.TryGetValue(id, out ItemData data)) return data.maxStackSize;
            return 64;
        }

        /// <summary>
        /// Returns true if this item places a block when used.
        /// </summary>
        public bool IsBlockItem(ushort id)
        {
            if (_items.TryGetValue(id, out ItemData data)) return data.blockToPlace > 0;
            return false;
        }

        /// <summary>
        /// Returns the BlockType this item places, or 0 if not a block item.
        /// </summary>
        public ushort GetBlockToPlace(ushort id)
        {
            if (_items.TryGetValue(id, out ItemData data)) return data.blockToPlace;
            return 0;
        }

        public void Cleanup()
        {
            _items.Clear();
        }
    }
}
