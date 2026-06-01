using UnityEngine;
using UnityEditor;
using MinecraftEngine;
using System.IO;
using System.Collections.Generic;

public class BlockDatabaseGenerator : EditorWindow
{
    [MenuItem("MinecraftEngine/Generate Block SOs (Auto-Textures)")]
    public static void GenerateBlocks()
    {
        string blocksPath = "Assets/Resources/Blocks";
        if (!Directory.Exists(blocksPath)) Directory.CreateDirectory(blocksPath);

        // 1. СКАНИРУЕМ ТЕКСТУРЫ И ПОЛУЧАЕМ ИХ ИНДЕКСЫ
        string texturesFolder = "Assets/Textures/minecraft-textures/block";
        if (!Directory.Exists(texturesFolder))
        {
            Debug.LogError($"Не найдена папка текстур: {texturesFolder}");
            return;
        }

        string[] filePaths = Directory.GetFiles(texturesFolder, "*.png", SearchOption.TopDirectoryOnly);
        List<string> validPaths = new List<string>(filePaths);
        validPaths.Sort(); // Обязательно сортируем по алфавиту
        validPaths.RemoveAll(p => p.EndsWith(".mcmeta")); // Убираем меты

        Dictionary<string, int> texIndexMap = new Dictionary<string, int>();
        for (int i = 0; i < validPaths.Count; i++)
        {
            string fileName = Path.GetFileNameWithoutExtension(validPaths[i]);
            texIndexMap[fileName] = i;
        }

        // Вспомогательная локальная функция для безопасного получения индекса
        int GetTexIdx(string texName)
        {
            if (texIndexMap.TryGetValue(texName, out int idx)) return idx;
            Debug.LogWarning($"Текстура {texName} не найдена в репозитории. Заменен на 0.");
            return 0; // Резервный индекс, если текстура не найдена
        }

        // 2. ГЕНЕРИРУЕМ SCRIPTABLE OBJECTS
        foreach (BlockType type in System.Enum.GetValues(typeof(BlockType)))
        {
            byte id = (byte)type;
            string name = type.ToString();

            BlockData block = ScriptableObject.CreateInstance<BlockData>();
            block.blockName = name;
            block.blockID = id;

            // --- БАЗОВЫЕ НАСТРОЙКИ ФИЗИКИ И РЕНДЕРА ---
            block.isSolid = true;
            block.isTransparent = false;
            block.hardness = 1.5f; // Камень по умолчанию

            if (type == BlockType.Air || type == BlockType.Water || type == BlockType.Lava || type == BlockType.Glass || type == BlockType.OakLeaves || type == BlockType.Ice)
            {
                block.isTransparent = true;
            }
            if (type == BlockType.Air || type == BlockType.Water || type == BlockType.Lava)
            {
                block.isSolid = false;
                block.hardness = 0f;
            }
            if (type == BlockType.Bedrock) block.hardness = -1f;
            if (type == BlockType.Dirt || type == BlockType.GrassBlock || type == BlockType.Sand || type == BlockType.RedSand || type == BlockType.Gravel || type == BlockType.Snow || type == BlockType.Clay || type == BlockType.MossBlock || type == BlockType.Podzol)
                block.hardness = 0.5f;
            if (type == BlockType.OakLeaves || type == BlockType.Glass) block.hardness = 0.3f;
            if (type == BlockType.Deepslate || type == BlockType.Tuff) block.hardness = 3.0f;
            if (type == BlockType.AncientDebris || type == BlockType.CryingObsidian) block.hardness = 30.0f;

            // --- НАСТРОЙКИ ТЕКСТУР ДЛЯ КАЖДОЙ ИЗ 6 ГРАНЕЙ ---
            // 0-Back, 1-Front, 2-Top, 3-Bottom, 4-Left, 5-Right
            string top = "", bottom = "", sides = "";

            switch (type)
            {
                case BlockType.Air: break; // Нет текстур
                case BlockType.GrassBlock: top = "grass_block_top"; bottom = "dirt"; sides = "grass_block_side"; break;
                case BlockType.Podzol: top = "podzol_top"; bottom = "dirt"; sides = "podzol_side"; break;
                case BlockType.OakLog: top = "oak_log_top"; bottom = "oak_log_top"; sides = "oak_log"; break;
                case BlockType.AncientDebris: top = "ancient_debris_top"; bottom = "ancient_debris_top"; sides = "ancient_debris_side"; break;
                case BlockType.Deepslate: top = "deepslate_top"; bottom = "deepslate_top"; sides = "deepslate"; break;

                case BlockType.Water: top = "water_still"; bottom = "water_still"; sides = "water_still"; break;
                case BlockType.Lava: top = "lava_still"; bottom = "lava_still"; sides = "lava_still"; break;

                // Простые блоки (одна текстура со всех сторон). 
                // Преобразуем название Enum "Stone" в "stone", "DiamondOre" в "diamond_ore"
                default:
                    string snakeCaseName = "";
                    for (int i = 0; i < name.Length; i++)
                    {
                        if (char.IsUpper(name[i]) && i > 0) snakeCaseName += "_";
                        snakeCaseName += char.ToLower(name[i]);
                    }
                    top = bottom = sides = snakeCaseName;
                    break;
            }

            if (type != BlockType.Air)
            {
                block.textureIndices[0] = GetTexIdx(sides); // Back
                block.textureIndices[1] = GetTexIdx(sides); // Front
                block.textureIndices[2] = GetTexIdx(top);   // Top
                block.textureIndices[3] = GetTexIdx(bottom);// Bottom
                block.textureIndices[4] = GetTexIdx(sides); // Left
                block.textureIndices[5] = GetTexIdx(sides); // Right
            }

            AssetDatabase.CreateAsset(block, $"{blocksPath}/{id}_{name}.asset");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ВСЕ ScriptableObjects блоков сгенерированы! Индексы проставлены автоматически.");
    }
}