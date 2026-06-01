using UnityEngine;

namespace MinecraftEngine
{
    // Перечисление инструментов для добычи
    public enum ToolType { None, Pickaxe, Axe, Shovel, Hoe, Sword }

    [CreateAssetMenu(fileName = "New Block", menuName = "MinecraftEngine/Block Data")]
    public class BlockData : ScriptableObject
    {
        [Header("Базовая информация")]
        public string blockName;
        public byte blockID;

        [Header("Рендер (Индексы текстур из Texture2DArray)")]
        [Tooltip("0-Back, 1-Front, 2-Top, 3-Bottom, 4-Left, 5-Right")]
        public int[] textureIndices = new int[6];
        
        public bool isTransparent = false;
        // Нужно ли рисовать этот блок, если он окружен непрозрачными соседями
        public bool isSolid = true; 

        [Header("Взаимодействие")]
        public float hardness = 1.0f; // Время в секундах (рукой)
        public ToolType bestTool = ToolType.None;
        public byte dropItemBlockID; // Что выпадает (пока что ID блока, позже будет ID предмета)

        // Вспомогательный метод для получения текстуры по грани
        public int GetTextureIndex(int faceIndex)
        {
            if (textureIndices == null || textureIndices.Length != 6) return 0;
            return textureIndices[faceIndex];
        }
    }
}