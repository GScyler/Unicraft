using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

public class GUIDownloader : EditorWindow
{
    private const string WidgetsUrl = "https://raw.githubusercontent.com/InventivetalentDev/minecraft-assets/1.18.2/assets/minecraft/textures/gui/widgets.png";

    [MenuItem("MinecraftEngine/Download GUI Textures")]
    public static async void DownloadGUI()
    {
        string dir = "Assets/Resources/GUI";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string savePath = $"{dir}/widgets.png";

        if (!File.Exists(savePath))
        {
            Debug.Log("Скачивание widgets.png...");
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    byte[] data = await client.GetByteArrayAsync(WidgetsUrl);
                    File.WriteAllBytes(savePath, data);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Ошибка скачивания: {e.Message}");
                    return;
                }
            }
        }

        AssetDatabase.Refresh();

        // Простая настройка для Pixel Art (Без нарезки на спрайты!)
        TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default; // Нам нужна просто текстура
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Debug.Log("GUI текстура успешно загружена!");
    }
}