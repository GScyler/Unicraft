using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class TextureArrayCreator : EditorWindow
{
    [MenuItem("MinecraftEngine/Create Texture Array")]
    static void Init()
    {
        string folderPath = "Assets/Textures/MinecraftTextures/block";

        if (!Directory.Exists(folderPath))
        {
            Debug.LogError($"Папка {folderPath} не найдена!");
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

        int textureCount = validPaths.Count;
        int skipped = 0;

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
                if (tex.width >= 16 && tex.height >= 16)
                {
                    // Для текстур 16×N (анимированные) — берём первый кадр (верхние 16×16)
                    // Для текстур NxN где N>16 — ресайзим до 16×16
                    Color[] pixels;

                    if (tex.width == 16)
                    {
                        // 16xN — первый кадр сверху (y = height-16 .. height)
                        pixels = tex.GetPixels(0, tex.height - 16, 16, 16);
                    }
                    else if (tex.width == tex.height)
                    {
                        // NxN (например 64x64) — ресайзим
                        pixels = ResizeTo16(tex);
                    }
                    else
                    {
                        // Нестандартный размер — пробуем взять верхний левый 16x16
                        if (tex.width >= 16 && tex.height >= 16)
                            pixels = tex.GetPixels(0, tex.height - 16, 16, 16);
                        else
                            pixels = new Color[16 * 16];
                    }

                    textureArray.SetPixels(pixels, i, 0);
                }
                else
                {
                    textureArray.SetPixels(new Color[16 * 16], i, 0);
                    skipped++;
                }
            }
            else
            {
                textureArray.SetPixels(new Color[16 * 16], i, 0);
                skipped++;
            }
        }

        textureArray.Apply();

        string savePath = "Assets/Textures/BlockTexturesArray.asset";

        // Удаляем старый если есть
        if (File.Exists(savePath))
            AssetDatabase.DeleteAsset(savePath);

        AssetDatabase.CreateAsset(textureArray, savePath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TextureArray] ✅ Создан! {textureCount} текстур (пропущено: {skipped}). Path: {savePath}");

        // Вывод индексов в лог (можно закомментировать для больших массивов)
        if (textureCount <= 200)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Индексы текстур:");
            for (int i = 0; i < textureCount; i++)
            {
                sb.AppendLine($"{i} = {Path.GetFileNameWithoutExtension(validPaths[i])}");
            }
            Debug.Log(sb.ToString());
        }
        else
        {
            Debug.Log($"[TextureArray] Слишком много текстур для полного лога ({textureCount}). Первые 20:");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 20 && i < textureCount; i++)
            {
                sb.AppendLine($"{i} = {Path.GetFileNameWithoutExtension(validPaths[i])}");
            }
            Debug.Log(sb.ToString());
        }
    }

    static Color[] ResizeTo16(Texture2D source)
    {
        Color[] result = new Color[16 * 16];
        float scaleX = (float)source.width / 16f;
        float scaleY = (float)source.height / 16f;

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int srcX = Mathf.FloorToInt(x * scaleX);
                int srcY = Mathf.FloorToInt(y * scaleY);
                srcX = Mathf.Clamp(srcX, 0, source.width - 1);
                srcY = Mathf.Clamp(srcY, 0, source.height - 1);
                result[y * 16 + x] = source.GetPixel(srcX, srcY);
            }
        }

        return result;
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

            // Отключаем maxSize чтобы большие текстуры не обрезались
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; needsReimport = true; }

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
        }
    }
}
