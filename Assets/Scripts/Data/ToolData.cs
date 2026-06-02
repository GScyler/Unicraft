using UnityEngine;

namespace MinecraftEngine
{
    public enum ToolTier : byte
    {
        Wood = 0,
        Stone,
        Iron,
        Gold,
        Diamond,
        Netherite
    }

    /// <summary>
    /// Tool/Weapon item with durability, mining speed, and harvest level.
    /// </summary>
    [CreateAssetMenu(fileName = "New Tool", menuName = "MinecraftEngine/Tool Data")]
    public class ToolData : ItemData
    {
        [Header("Tool Properties")]
        public ToolType toolType = ToolType.Pickaxe;
        public ToolTier tier = ToolTier.Wood;

        [Tooltip("Wood=59, Stone=131, Iron=250, Gold=32, Diamond=1561, Netherite=2031")]
        public int maxDurability = 59;

        [Tooltip("Wood=2, Stone=4, Iron=6, Gold=12, Diamond=8, Netherite=9")]
        public float miningSpeedMultiplier = 2f;

        [Tooltip("Wood=0, Stone=1, Iron=2, Diamond=3, Netherite=3")]
        [Range(0, 4)]
        public int harvestLevel = 0;

        [Tooltip("Wood=15, Stone=5, Iron=14, Gold=22, Diamond=10, Netherite=15")]
        public int enchantability = 15;
    }
}
