using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Приводит структуру проекта к единому формату:
/// - PascalCase для папок
/// - Шейдеры и материалы в раздельных подпапках
/// - Scenes → _Scenes (вверху иерархии)
/// - Удаление устаревших файлов
/// - Логическая группировка скриптов
///
/// Запуск: MinecraftEngine → Refactor Project Structure
/// </summary>
public class ProjectStructureRefactor : EditorWindow
{
    [MenuItem("MinecraftEngine/Refactor Project Structure")]
    public static void Refactor()
    {
        int actions = 0;

        // ══════════════════════════════════════════════
        // 1. Scenes → _Scenes
        // ══════════════════════════════════════════════
        if (MoveAsset("Assets/Scenes", "Assets/_Scenes")) actions++;

        // ══════════════════════════════════════════════
        // 2. Materials → Materials/Shaders + Materials/Mats
        // ══════════════════════════════════════════════
        EnsureDir("Assets/Materials/Shaders");
        
        MoveAsset("Assets/Materials/TextureArrayShader.shader",    "Assets/Materials/Shaders/TextureArrayShader.shader");
        MoveAsset("Assets/Materials/BlockCracks.shader",           "Assets/Materials/Shaders/BlockCracks.shader");
        MoveAsset("Assets/Materials/ThickLines.shader",            "Assets/Materials/Shaders/ThickLines.shader");
        MoveAsset("Assets/Materials/UI_TextureArrayShader.shader", "Assets/Materials/Shaders/UI_TextureArrayShader.shader");
        actions++;

        // ══════════════════════════════════════════════
        // 3. Textures: minecraft-textures → MinecraftTextures
        // ══════════════════════════════════════════════
        MoveAsset("Assets/Textures/minecraft-textures", "Assets/Textures/MinecraftTextures");
        actions++;

        // ══════════════════════════════════════════════
        // 4. Sounds: minecraft-sounds → MinecraftSounds
        // ══════════════════════════════════════════════
        MoveAsset("Assets/Sounds/minecraft-sounds", "Assets/Sounds/MinecraftSounds");
        actions++;

        // ══════════════════════════════════════════════
        // 5. Data: создаём если нет, minecraft-data → MinecraftData
        // ══════════════════════════════════════════════
        EnsureDir("Assets/Data");
        MoveAsset("Assets/Data/minecraft-data", "Assets/Data/MinecraftData");
        actions++;

        // ══════════════════════════════════════════════
        // 6. Editor: группируем по назначению
        // ══════════════════════════════════════════════
        EnsureDir("Assets/Editor/Generators");
        EnsureDir("Assets/Editor/Tools");

        // Generators — скрипты создающие ассеты
        MoveAsset("Assets/Editor/BlockDatabaseGenerator.cs", "Assets/Editor/Generators/BlockDatabaseGenerator.cs");
        MoveAsset("Assets/Editor/BiomeGenerator.cs",         "Assets/Editor/Generators/BiomeGenerator.cs");
        MoveAsset("Assets/Editor/TextureArrayCreator.cs",    "Assets/Editor/Generators/TextureArrayCreator.cs");
        MoveAsset("Assets/Editor/CracksTextureCreator.cs",   "Assets/Editor/Generators/CracksTextureCreator.cs");
        MoveAsset("Assets/Editor/ItemIconGenerator.cs",      "Assets/Editor/Generators/ItemIconGenerator.cs");

        // Tools — утилиты
        MoveAsset("Assets/Editor/AssetOrganizer.cs",         "Assets/Editor/Tools/AssetOrganizer.cs");
        MoveAsset("Assets/Editor/SetupMaterialsTool.cs",     "Assets/Editor/Tools/SetupMaterialsTool.cs");
        MoveAsset("Assets/Editor/GUIDownloader.cs",          "Assets/Editor/Tools/GUIDownloader.cs");
        MoveAsset("Assets/Editor/ProjectStructureRefactor.cs","Assets/Editor/Tools/ProjectStructureRefactor.cs");
        actions++;

        // ══════════════════════════════════════════════
        // 7. Scripts: DebugCamera → Scripts/Player (логичнее)
        // ══════════════════════════════════════════════
        MoveAsset("Assets/Scripts/WorldGeneration/DebugCamera.cs", "Assets/Scripts/Player/DebugCamera.cs");
        actions++;

        // ══════════════════════════════════════════════
        // 8. Удаляем устаревший InputSystem_Actions (заменён GameInputActions)
        // ══════════════════════════════════════════════
        if (File.Exists("Assets/Unity/InputSystem_Actions.inputactions"))
        {
            AssetDatabase.DeleteAsset("Assets/Unity/InputSystem_Actions.inputactions");
            Debug.Log("[Refactor] Удалён устаревший InputSystem_Actions.inputactions");
            actions++;
        }

        // ══════════════════════════════════════════════
        // 9. Обновляем пути в скриптах
        // ══════════════════════════════════════════════
        UpdatePathsInScripts();
        actions++;

        AssetDatabase.Refresh();

        Debug.Log($"[Refactor] ✅ Готово! Выполнено {actions} действий.");
        Debug.Log("[Refactor] Новая структура:");
        Debug.Log(@"
Assets/
├── _Scenes/               ← сцены (вверху)
├── Data/                  ← MC данные (JSON)
├── Editor/
│   ├── Generators/        ← генераторы ассетов
│   └── Tools/             ← утилиты
├── Input/                 ← InputActions
├── Materials/
│   └── Shaders/           ← .shader файлы
├── Resources/             ← SO (Blocks, Biomes, Fonts, GUI)
├── Scripts/
│   ├── Core/              ← BlockType, InputManager, UIManager, VoxelData, VoxelSettings
│   ├── Data/              ← BlockData, BlockDatabase, BiomeData, BiomeDatabase
│   ├── Items/             ← ItemEntity, ItemManager
│   ├── Player/            ← Controller, Interaction, Inventory, SpectatorFly, DebugCamera
│   └── WorldGeneration/   ← WorldManager, ChunkRenderer, Jobs
├── Sounds/
│   └── MinecraftSounds/   ← .ogg файлы
├── Textures/
│   ├── MinecraftTextures/ ← block/, item/, entity/, gui/ etc.
│   ├── BlockTexturesArray.asset
│   └── CracksArray.asset
└── Unity/                 ← TMP, Settings
");
    }

    /// <summary>
    /// Обновляет захардкоженные пути в скриптах после переименования папок.
    /// </summary>
    static void UpdatePathsInScripts()
    {
        // Файлы которые ссылаются на minecraft-textures
        string[][] replacements = new string[][]
        {
            // Generators
            new[] { "Assets/Editor/Generators/BlockDatabaseGenerator.cs",
                     "minecraft-textures/block", "MinecraftTextures/block" },
            new[] { "Assets/Editor/Generators/TextureArrayCreator.cs",
                     "minecraft-textures/block", "MinecraftTextures/block" },
            new[] { "Assets/Editor/Generators/CracksTextureCreator.cs",
                     "minecraft-textures/block", "MinecraftTextures/block" },
            new[] { "Assets/Editor/Generators/ItemIconGenerator.cs",
                     "minecraft-textures/block", "MinecraftTextures/block" },

            // Tools
            new[] { "Assets/Editor/Tools/AssetOrganizer.cs",
                     "minecraft-textures", "MinecraftTextures" },
        };

        foreach (var r in replacements)
        {
            string filePath = r[0];
            string oldStr = r[1];
            string newStr = r[2];

            if (!File.Exists(filePath))
            {
                // Может файл ещё не переместился — пробуем старый путь
                string oldPath = filePath.Replace("Generators/", "").Replace("Tools/", "");
                if (File.Exists(oldPath)) filePath = oldPath;
                else continue;
            }

            string content = File.ReadAllText(filePath);
            if (content.Contains(oldStr))
            {
                content = content.Replace(oldStr, newStr);
                File.WriteAllText(filePath, content);
                Debug.Log($"[Refactor] Обновлён путь в {Path.GetFileName(filePath)}: {oldStr} → {newStr}");
            }
        }
    }

    // ── Helpers ──

    static bool MoveAsset(string from, string to)
    {
        if (!File.Exists(from) && !Directory.Exists(from)) return false;
        if (File.Exists(to) || Directory.Exists(to)) return false; // уже существует

        // Убеждаемся что целевая директория существует
        string targetDir = Path.GetDirectoryName(to);
        if (!string.IsNullOrEmpty(targetDir))
            EnsureDir(targetDir);

        string result = AssetDatabase.MoveAsset(from, to);
        if (string.IsNullOrEmpty(result))
        {
            Debug.Log($"[Refactor] {from} → {to}");
            return true;
        }
        else
        {
            Debug.LogWarning($"[Refactor] Не удалось: {from} → {to}: {result}");
            return false;
        }
    }

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
