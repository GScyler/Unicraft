using UnityEngine;

namespace MinecraftEngine
{
    public enum ItemType : byte
    {
        Block = 0,
        Tool,
        Weapon,
        Armor,
        Food,
        Material,     // stick, ingot, diamond, redstone, string
        Potion,
        SpawnEgg,
        Misc          // bucket, compass, clock, map, name_tag
    }

    /// <summary>
    /// Base ScriptableObject for all items in the game.
    /// Block items reference a BlockType, non-block items exist independently.
    /// </summary>
    [CreateAssetMenu(fileName = "New Item", menuName = "MinecraftEngine/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        public ushort itemID;
        public string itemName;
        [TextArea(1, 3)]
        public string itemDescription;
        public ItemType type = ItemType.Material;

        [Header("Stacking")]
        [Range(1, 64)]
        public int maxStackSize = 64;        // 1 for tools/weapons, 16 for eggs/ender_pearls, 64 default

        [Header("Block Placement")]
        [Tooltip("If this item places a block, set the BlockType ID here. 0 = not a block item.")]
        public ushort blockToPlace = 0;

        [Header("Combat")]
        public float attackDamage = 1f;      // hand = 1
        public float attackSpeed = 4f;       // hand = 4, sword = 1.6, axe = 0.8-1.0

        [Header("Visual")]
        public Sprite icon;                  // for UI rendering (optional, can generate from texture)
    }
}
