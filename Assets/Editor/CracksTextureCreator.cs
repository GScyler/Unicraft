using UnityEditor;
using UnityEngine;

public class CracksTextureCreator : EditorWindow
{
    [MenuItem("MinecraftEngine/Create Cracks Array")]
    static void Init()
    {
        // 10 стадий разрушения (destroy_stage_0.png ... destroy_stage_9.png)
        Texture2DArray textureArray = new Texture2DArray(16, 16, 10, TextureFormat.RGBA32, false);
        textureArray.filterMode = FilterMode.Point;
        textureArray.wrapMode = TextureWrapMode.Repeat;

        for (int i = 0; i < 10; i++)
        {
            string path = $"Assets/Textures/minecraft-textures/block/destroy_stage_{i}.png";
            EnsureTextureReadable(path);
            
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                textureArray.SetPixels(tex.GetPixels(0), i, 0);
            }
            else
            {
                Debug.LogWarning($"Текстура {path} не найдена. Если вы не в выживании, можете игнорировать.");
            }
        }
        
        textureArray.Apply();
        
        string assetPath = "Assets/Textures/CracksArray.asset";
        AssetDatabase.CreateAsset(textureArray, assetPath);

        // Автоматически создаем материал
        Material mat = new Material(Shader.Find("MinecraftEngine/BlockCracks"));
        mat.SetTexture("_MainTex", textureArray);
        AssetDatabase.CreateAsset(mat, "Assets/Materials/BlockCracksMat.mat");

        AssetDatabase.SaveAssets();
        Debug.Log("Атлас трещин и материал успешно созданы!");
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
            
            if (needsReimport) importer.SaveAndReimport();
        }
    }
}