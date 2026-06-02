using UnityEngine;

namespace MinecraftEngine
{
    public enum ArmorSlot : byte
    {
        Helmet = 0,
        Chestplate,
        Leggings,
        Boots
    }

    public enum ArmorMaterial : byte
    {
        Leather = 0,
        Chainmail,
        Iron,
        Gold,
        Diamond,
        Netherite,
        Turtle   // Turtle Shell helmet only
    }

    /// <summary>
    /// Armor item with defense, toughness, knockback resistance, and slot.
    /// </summary>
    [CreateAssetMenu(fileName = "New Armor", menuName = "MinecraftEngine/Armor Data")]
    public class ArmorData : ItemData
    {
        [Header("Armor Properties")]
        public ArmorSlot slot = ArmorSlot.Helmet;
        public ArmorMaterial material = ArmorMaterial.Iron;

        [Tooltip("Defense points (Leather Helmet=1, Iron Chestplate=6, Diamond Leggings=6, Netherite Boots=3)")]
        public int defensePoints = 1;

        [Tooltip("Armor toughness (Diamond=2, Netherite=3, others=0)")]
        public float toughness = 0f;

        [Tooltip("Knockback resistance per piece (Netherite=0.1, others=0)")]
        public float knockbackResistance = 0f;

        [Tooltip("Durability (Leather Helmet=55, Iron Chestplate=240, Diamond Leggings=495, Netherite Boots=481)")]
        public int maxDurability = 55;
    }
}
