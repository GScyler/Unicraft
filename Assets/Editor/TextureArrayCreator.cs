using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class TextureArrayCreator : EditorWindow
{
    [MenuItem("MinecraftEngine/Create Texture Array")]
    static void Init()
    {
        string folderPath = "Assets/Textures/minecraft-textures/block";

        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"Папка {folderPath} не найдена! Убедитесь, что репозиторий клонирован.");
            return;
        }

        string[] filePaths = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);

        if (filePaths.Length == 0)
        {
            Debug.LogError("В папке не найдено ни одного .png файла!");
            return;
        }

        List<string> validPaths = new List<string>(filePaths);
        validPaths.Sort();
        validPaths.RemoveAll(p => p.EndsWith(".mcmeta"));

        int textureCount = validPaths.Count;

        Texture2DArray textureArray = new Texture2DArray(16, 16, textureCount, TextureFormat.RGBA32, false);
        textureArray.filterMode = FilterMode.Point;
        textureArray.wrapMode = TextureWrapMode.Repeat;

        for (int i = 0; i < textureCount; i++)
        {
            string path = validPaths[i].Replace('\\', '/');
            string texName = Path.GetFileNameWithoutExtension(path);

            EnsureTextureReadable(path);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                if (tex.width == 16 && tex.height >= 16)
                {
                    Color[] pixels = tex.GetPixels(0, tex.height - 16, 16, 16);
                    textureArray.SetPixels(pixels, i, 0);
                }
                else
                {
                    textureArray.SetPixels(new Color[16 * 16], i, 0);
                }
            }
        }

        textureArray.Apply();

        string savePath = "Assets/Textures/BlockTexturesArray.asset";
        AssetDatabase.CreateAsset(textureArray, savePath);
        AssetDatabase.SaveAssets();

        string log = "Индексы текстур:\n";
        for (int i = 0; i < textureCount; i++)
        {
            log += $"{i} = {Path.GetFileNameWithoutExtension(validPaths[i])}\n";
        }
        Debug.Log($"Texture2DArray успешно создан! Загружено {textureCount} текстур. (См. {savePath})\n" + log);
    }

    static void EnsureTextureReadable(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool needsReimport = false;
            if (!importer.isReadable) { importer.isReadable = true; needsReimport = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; needsReimport = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; needsReimport = true; }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
        }
    }
}