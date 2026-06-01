using UnityEditor;
using UnityEngine;

public class SetupMaterialsTool : EditorWindow
{
    [MenuItem("MinecraftEngine/Setup Materials")]
    public static void CreateMaterials()
    {
        string materialPath = "Assets/Materials/SelectionOutlineMat.mat";

        Shader shader = Shader.Find("MinecraftEngine/SelectionOutline");
        if (shader == null)
        {
            Debug.LogError("Не найден шейдер MinecraftEngine/SelectionOutline!");
            return;
        }

        Material mat = new Material(shader);
        mat.color = new Color(0f, 0f, 0f, 0.4f); // Полупрозрачный черный

        AssetDatabase.CreateAsset(mat, materialPath);
        AssetDatabase.SaveAssets();

        Debug.Log("Материал SelectionOutlineMat успешно создан!");
    }
}