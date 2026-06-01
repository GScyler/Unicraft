using UnityEngine;

namespace MinecraftEngine
{
    [CreateAssetMenu(fileName = "New Biome", menuName = "MinecraftEngine/Biome Data")]
    public class BiomeData : ScriptableObject
    {
        public string biomeName;
        
        [Header("Блоки ландшафта")]
        public byte surfaceBlock = 4; // GrassBlock
        public byte subSurfaceBlock = 3; // Dirt
        public int subSurfaceDepth = 3; 

        [Header("Подводные/Подземные блоки")]
        public byte underwaterSurfaceBlock = 6; // Sand
        public byte underwaterSubSurfaceBlock = 6; 

        [Header("Multi-Noise Целевые Параметры (Target Points)")]
        [Tooltip("-1 = Ледники, 0 = Умеренно, 1 = Пустыни")]
        [Range(-1f, 1f)] public float targetTemperature = 0f;
        
        [Tooltip("-1 = Сухо (Пустыня), 1 = Влажно (Джунгли)")]
        [Range(-1f, 1f)] public float targetHumidity = 0f;
        
        [Tooltip("-1 = Глубокий Океан, 0 = Побережье, 1 = Глубоко в материке")]
        [Range(-1f, 1f)] public float targetContinentalness = 0f;
        
        [Tooltip("-1 = Высокие горы, 1 = Плоские равнины/болота")]
        [Range(-1f, 1f)] public float targetErosion = 0f;
        
        [Tooltip("-1 = Высоко в небе, 0 = Поверхность, 1 = Глубоко под землей")]
        [Range(-1f, 1f)] public float targetDepth = 0f;
    }
}