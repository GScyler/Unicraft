using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Editor tool: распределяет ресурсы из импортированной папки minecraft-assets-1.20.4
/// по целевой иерархии проекта. Запуск: MinecraftEngine → Organize Imported Assets.
/// 
/// Ожидает папку Assets/minecraft-assets-1.20.4/ (или Assets/minecraft-assets-1.20.4-1.20.4/)
/// с оригинальной структурой из GitHub.
/// </summary>
public class AssetOrganizer : EditorWindow
{
    // Ищем папку с импортированными ассетами (может называться по-разному)
    private static string FindImportRoot()
    {
        string[] candidates = new string[]
        {
            "Assets/minecraft-assets-1.20.4",
            "Assets/minecraft-assets-1.20.4-1.20.4",
            "Assets/minecraft-assets",
        };

        foreach (string c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }

        // Поиск любой папки с "minecraft-assets" в имени
        foreach (string dir in Directory.GetDirectories("Assets"))
        {
            if (Path.GetFileName(dir).Contains("minecraft-assets"))
                return dir;
        }

        return null;
    }

    [MenuItem("MinecraftEngine/Organize Imported Assets")]
    public static void Organize()
    {
        string importRoot = FindImportRoot();
        if (importRoot == null)
        {
            Debug.LogError("[AssetOrganizer] Не найдена папка minecraft-assets в Assets/. " +
                "Импортируйте репозиторий minecraft-assets (ветка 1.20.4) в Assets/.");
            return;
        }

        Debug.Log($"[AssetOrganizer] Найдена папка: {importRoot}");

        string mcAssets = Path.Combine(importRoot, "assets", "minecraft");
        string mcData = Path.Combine(importRoot, "data", "minecraft");

        if (!Directory.Exists(mcAssets))
        {
            // Может быть без вложенной assets/minecraft
            mcAssets = importRoot;
            Debug.LogWarning($"[AssetOrganizer] assets/minecraft не найден, используем {importRoot} напрямую");
        }

        int movedFiles = 0;
        int movedDirs = 0;

        // === 1. ТЕКСТУРЫ: заменяем MinecraftTextures ===
        string srcTextures = Path.Combine(mcAssets, "textures");
        string dstTextures = "Assets/Textures/MinecraftTextures";

        if (Directory.Exists(srcTextures))
        {
            // Удаляем старые текстуры
            if (Directory.Exists(dstTextures))
            {
                Debug.Log("[AssetOrganizer] Удаляю старые текстуры...");
                // Используем AssetDatabase для корректного удаления
                AssetDatabase.DeleteAsset(dstTextures);
            }

            Debug.Log("[AssetOrganizer] Копирую новые текстуры...");
            CopyDirectory(srcTextures, dstTextures);
            movedDirs++;
            Debug.Log($"[AssetOrganizer] ✅ Текстуры → {dstTextures}");
        }
        else
        {
            Debug.LogWarning($"[AssetOrganizer] Текстуры не найдены: {srcTextures}");
        }

        // === 2. DATA: blockstates, models, loot_tables, recipes, tags, lang ===
        string dstData = "Assets/Data";
        EnsureDirectory(dstData);

        // Blockstates
        CopySubdirectory(mcAssets, "blockstates", Path.Combine(dstData, "blockstates"), ref movedDirs);

        // Models (block + item)
        CopySubdirectory(mcAssets, "models", Path.Combine(dstData, "models"), ref movedDirs);

        // Lang
        string langSrc = Path.Combine(mcAssets, "lang", "en_us.json");
        if (File.Exists(langSrc))
        {
            File.Copy(langSrc, Path.Combine(dstData, "en_us.json"), true);
            movedFiles++;
            Debug.Log($"[AssetOrganizer] ✅ en_us.json → {dstData}");
        }

        // Data: loot_tables, recipes, tags
        if (Directory.Exists(mcData))
        {
            CopySubdirectory(mcData, "loot_tables", Path.Combine(dstData, "loot_tables"), ref movedDirs);
            CopySubdirectory(mcData, "recipes", Path.Combine(dstData, "recipes"), ref movedDirs);
            CopySubdirectory(mcData, "tags", Path.Combine(dstData, "tags"), ref movedDirs);

            // worldgen (полезно для биомов)
            CopySubdirectory(mcData, "worldgen", Path.Combine(dstData, "worldgen"), ref movedDirs);
        }
        else
        {
            Debug.LogWarning($"[AssetOrganizer] data/minecraft не найден: {mcData}");
        }

        // === 3. ЗВУКИ (если есть в minecraft-assets) ===
        string srcSounds = Path.Combine(mcAssets, "sounds");
        string dstSounds = "Assets/Sounds/minecraft-sounds";
        if (Directory.Exists(srcSounds) && !Directory.Exists(dstSounds))
        {
            CopyDirectory(srcSounds, dstSounds);
            movedDirs++;
            Debug.Log($"[AssetOrganizer] ✅ Звуки → {dstSounds}");
        }

        // === 4. Переименовываем Scenes → _Scenes (если ещё не) ===
        if (Directory.Exists("Assets/Scenes") && !Directory.Exists("Assets/_Scenes"))
        {
            AssetDatabase.MoveAsset("Assets/Scenes", "Assets/_Scenes");
            Debug.Log("[AssetOrganizer] ✅ Scenes → _Scenes");
        }

        // === 5. Удаляем импортированную папку (опционально) ===
        // НЕ удаляем автоматически — пользователь может захотеть проверить
        Debug.Log($"[AssetOrganizer] Импортированная папка сохранена: {importRoot}");
        Debug.Log($"[AssetOrganizer] Удалите её вручную после проверки: правый клик → Delete");

        // === Обновляем AssetDatabase ===
        AssetDatabase.Refresh();

        Debug.Log($"[AssetOrganizer] ✅ Готово! Перемещено: {movedDirs} папок, {movedFiles} файлов.");
        Debug.Log("[AssetOrganizer] Следующие шаги:");
        Debug.Log("  1. MinecraftEngine → Create Texture Array");
        Debug.Log("  2. MinecraftEngine → Create Cracks Array");
        Debug.Log("  3. MinecraftEngine → Generate Block SOs (Auto-Textures)");
    }

    private static void CopySubdirectory(string parentSrc, string subDir, string dst, ref int count)
    {
        string src = Path.Combine(parentSrc, subDir);
        if (Directory.Exists(src))
        {
            EnsureDirectory(dst);
            CopyDirectory(src, dst);
            count++;
            Debug.Log($"[AssetOrganizer] ✅ {subDir} → {dst}");
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        EnsureDirectory(dst);

        foreach (string file in Directory.GetFiles(src))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(dst, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (string dir in Directory.GetDirectories(src))
        {
            string dirName = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(dst, dirName));
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
