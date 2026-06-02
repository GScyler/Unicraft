using UnityEngine;

namespace MinecraftEngine
{
    // Перечисление инструментов для добычи
    public enum ToolType { None, Pickaxe, Axe, Shovel, Hoe, Sword, Shears }

    public enum BlockShape : byte
    {
        FullBlock = 0,
        Slab,
        Stairs,
        Fence,
        Door,
        Trapdoor,
        ThinPane,   // Glass Pane, Iron Bars
        Cross,      // Flowers, Saplings, Tall Grass
        Torch,
        Button,
        PressurePlate,
        Carpet,
        Sign,
        Wall,
        Ladder,
        Lantern,
        Chain
    }

    public enum SoundCategory : byte
    {
        Stone = 0, Wood, Grass, Sand, Gravel, Glass, Metal,
        Wool, Snow, Coral, Amethyst, Deepslate, Sculk,
        CherryWood, BambooWood, Nether, MudBrick, PackedMud
    }

    [CreateAssetMenu(fileName = "New Block", menuName = "MinecraftEngine/Block Data")]
    public class BlockData : ScriptableObject
    {
        [Header("Базовая информация")]
        public string blockName;
        public ushort blockID;

        [Header("Рендер (Индексы текстур из Texture2DArray)")]
        [Tooltip("0-Back, 1-Front, 2-Top, 3-Bottom, 4-Left, 5-Right")]
        public int[] textureIndices = new int[6];

        public bool isTransparent = false;
        public bool isSolid = true;

        [Header("Освещение")]
        [Range(0, 15)]
        public byte lightEmission = 0;  // torch=14, glowstone=15, lava=15
        [Range(0, 15)]
        public byte opacity = 15;       // glass=0, water=1, leaves=1, solid=15

        [Header("Взаимодействие")]
        public float hardness = 1.0f;
        public float blastResistance = 6.0f;
        public ToolType bestTool = ToolType.None;
        [Range(0, 4)]
        public int harvestLevel = 0;    // 0=hand, 1=wood, 2=stone, 3=iron, 4=diamond

        [Header("Дроп")]
        public ushort dropItemBlockID;

        [Header("Физика")]
        public bool isGravityAffected = false;   // sand, gravel
        public bool isFlammable = false;
        public int flammability = 0;             // chance to catch fire
        public int fireSpread = 0;               // chance to spread fire
        public bool isInteractable = false;      // crafting table, furnace, chest

        [Header("Форма и звук")]
        public BlockShape shape = BlockShape.FullBlock;
        public SoundCategory soundCategory = SoundCategory.Stone;

        public int GetTextureIndex(int faceIndex)
        {
            if (textureIndices == null || textureIndices.Length != 6) return 0;
            return textureIndices[faceIndex];
        }
    }
}