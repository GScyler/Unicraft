using UnityEngine;
using UnityEditor;
using System.IO;
using MinecraftEngine;

public class ItemIconGenerator : EditorWindow
{
    [MenuItem("MinecraftEngine/Generate Block Icons")]
    public static void GenerateIcons()
    {
        string iconsPath = "Assets/Resources/Icons";
        if (!Directory.Exists(iconsPath)) Directory.CreateDirectory(iconsPath);

        string[] guids = AssetDatabase.FindAssets("t:BlockData", new[] { "Assets/Resources/Blocks" });
        
        Texture2DArray texArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/Textures/BlockTexturesArray.asset");
        if (texArray == null)
        {
            Debug.LogError("Сначала создайте Texture2DArray через меню 'MinecraftEngine -> Create Texture Array'!");
            return;
        }

        int generatedCount = 0;

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            BlockData block = AssetDatabase.LoadAssetAtPath<BlockData>(assetPath);
            
            if (block == null || block.blockID == 0) continue; 

            int topIdx = block.GetTextureIndex(2);
            int frontIdx = block.GetTextureIndex(1);
            int rightIdx = block.GetTextureIndex(5);

            Texture2D icon = CreatePerfectIsometricIcon(texArray, topIdx, frontIdx, rightIdx, block);
            
            byte[] pngData = icon.EncodeToPNG();
            string savePath = $"{iconsPath}/Icon_{block.blockID}.png";
            File.WriteAllBytes(savePath, pngData);
            
            AssetDatabase.ImportAsset(savePath);
            TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point; 
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            generatedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Сгенерировано {generatedCount} ИДЕАЛЬНЫХ изометрических иконок в {iconsPath}!");
    }

    private static Texture2D CreatePerfectIsometricIcon(Texture2DArray array, int topIdx, int frontIdx, int rightIdx, BlockData block)
    {
        Texture2D result = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        
        Color[] clearCols = new Color[32 * 32];
        for (int i = 0; i < clearCols.Length; i++) clearCols[i] = Color.clear;
        result.SetPixels(clearCols);

        Color[] topPixels = GetPixelsSafe(array, topIdx);
        Color[] frontPixels = GetPixelsSafe(array, frontIdx);
        Color[] rightPixels = GetPixelsSafe(array, rightIdx);

        float topShade = 1.0f;     
        float frontShade = 0.6f;   
        float rightShade = 0.4f;   

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int cx = x - 16;
                int cy = y - 16; 

                Color finalCol = Color.clear;

                int ty = cy - 2; 
                int tx = cx;
                int uTop = tx + ty + 7; 
                int vTop = ty - tx + 8;

                if (uTop >= 0 && uTop <= 15 && vTop >= 0 && vTop <= 15)
                {
                    finalCol = GetPixel(topPixels, uTop, vTop);
                    if (finalCol.a > 0.1f)
                    {
                        if (block.blockID == 4) finalCol *= new Color32(121, 192, 90, 255); 
                        if (block.blockID == 10) finalCol *= new Color32(121, 192, 90, 255); 
                        if (block.blockID == 11) finalCol *= new Color32(63, 118, 228, 255); 
                        finalCol *= topShade;
                    }
                    else finalCol = Color.clear;
                }

                if (finalCol.a < 0.1f && cx >= 0 && cx <= 15)
                {
                    int uRight = cx;
                    int vRight = cy + cx / 2 + 5; 
                    
                    if (vRight >= 0 && vRight <= 15)
                    {
                        finalCol = GetPixel(rightPixels, uRight, vRight);
                        if (finalCol.a > 0.1f)
                        {
                            if (block.blockID == 11) finalCol *= new Color32(63, 118, 228, 255); 
                            finalCol *= rightShade;
                        }
                        else finalCol = Color.clear;
                    }
                }

                if (finalCol.a < 0.1f && cx < 0 && cx >= -15)
                {
                    int uFront = 15 + cx; 
                    int vFront = cy - cx / 2 + 5;
                    
                    if (vFront >= 0 && vFront <= 15)
                    {
                        finalCol = GetPixel(frontPixels, uFront, vFront);
                        if (finalCol.a > 0.1f)
                        {
                            if (block.blockID == 11) finalCol *= new Color32(63, 118, 228, 255); 
                            finalCol *= frontShade;
                        }
                        else finalCol = Color.clear;
                    }
                }

                if (finalCol.a > 0.1f)
                {
                    result.SetPixel(x, y, finalCol);
                }
            }
        }

        result.Apply();
        return result;
    }

    private static Color GetPixel(Color[] pixels, int x, int y)
    {
        return pixels[(15 - y) * 16 + x];
    }

    private static Color[] GetPixelsSafe(Texture2DArray array, int index)
    {
        if (index >= 0 && index < array.depth)
        {
            return array.GetPixels(index, 0);
        }
        return new Color[256]; 
    }
}