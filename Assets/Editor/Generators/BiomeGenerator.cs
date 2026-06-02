using UnityEditor;
using UnityEngine;
using MinecraftEngine;

public class BiomeGenerator : EditorWindow
{
    [MenuItem("MinecraftEngine/Generate Default Biomes")]
    public static void GenerateBiomes()
    {
        string path = "Assets/Resources/Biomes";
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

        // 1. Plains
        CreateBiome(path, "Plains", (byte)BlockType.GrassBlock, (byte)BlockType.Dirt, (byte)BlockType.Sand, (byte)BlockType.Sand, 0.0f, 0.0f, 0.5f, 0.5f, 0.0f);

        // 2. Desert
        CreateBiome(path, "Desert", (byte)BlockType.Sand, (byte)BlockType.Sand, (byte)BlockType.Sand, (byte)BlockType.Sand, 1.0f, -1.0f, 0.5f, 0.5f, 0.0f);

        // 3. Snowy Mountains
        CreateBiome(path, "SnowyMountains", (byte)BlockType.Snow, (byte)BlockType.Stone, (byte)BlockType.Gravel, (byte)BlockType.Gravel, -1.0f, 0.0f, 0.5f, -0.8f, 0.0f);

        // 4. Ocean
        CreateBiome(path, "Ocean", (byte)BlockType.Gravel, (byte)BlockType.Dirt, (byte)BlockType.Gravel, (byte)BlockType.Dirt, 0.0f, 0.0f, -0.8f, 0.5f, -0.5f);

        // 5. Jungle (Теперь с Подзолом!)
        CreateBiome(path, "Jungle", (byte)BlockType.Podzol, (byte)BlockType.Dirt, (byte)BlockType.Dirt, (byte)BlockType.Dirt, 0.8f, 1.0f, 0.5f, 0.0f, 0.0f);

        // 6. Badlands (Теперь с Красным песком и Терракотой!)
        CreateBiome(path, "Badlands", (byte)BlockType.RedSand, (byte)BlockType.Terracotta, (byte)BlockType.RedSand, (byte)BlockType.Terracotta, 0.9f, -1.0f, 0.5f, -0.5f, 0.0f);

        // 7. Lush Caves
        CreateBiome(path, "LushCaves", (byte)BlockType.MossBlock, (byte)BlockType.Dirt, (byte)BlockType.Clay, (byte)BlockType.Clay, 0.0f, 0.8f, 0.5f, 0.0f, -0.8f);

        // 8. Dripstone Caves
        CreateBiome(path, "DripstoneCaves", (byte)BlockType.DripstoneBlock, (byte)BlockType.Stone, (byte)BlockType.DripstoneBlock, (byte)BlockType.Stone, 0.0f, -0.8f, 0.5f, 0.0f, -0.8f);

        AssetDatabase.SaveAssets();
        Debug.Log("Биомы обновлены новыми блоками!");
    }

    private static void CreateBiome(string path, string name, byte surface, byte subSurface, byte uwSurface, byte uwSubSurface, 
                                    float temp, float hum, float cont, float ero, float dep)
    {
        BiomeData biome = ScriptableObject.CreateInstance<BiomeData>();
        biome.biomeName = name;
        biome.surfaceBlock = surface;
        biome.subSurfaceBlock = subSurface;
        biome.underwaterSurfaceBlock = uwSurface;
        biome.underwaterSubSurfaceBlock = uwSubSurface;
        biome.subSurfaceDepth = 3;
        
        biome.targetTemperature = temp;
        biome.targetHumidity = hum;
        biome.targetContinentalness = cont;
        biome.targetErosion = ero;
        biome.targetDepth = dep;

        AssetDatabase.CreateAsset(biome, $"{path}/{name}.asset");
    }
}