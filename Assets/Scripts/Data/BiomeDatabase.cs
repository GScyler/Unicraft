using Unity.Collections;
using UnityEngine;

namespace MinecraftEngine
{
    public struct BiomeStruct
    {
        public byte surfaceBlock;
        public byte subSurfaceBlock;
        public byte underwaterSurfaceBlock;
        public byte underwaterSubSurfaceBlock;
        public int subSurfaceDepth;
        public float temp;
        public float hum;
        public float cont;
        public float ero;
        public float dep;
    }

    public class BiomeDatabase : MonoBehaviour
    {
        public static BiomeDatabase Instance;
        private const string BiomesPath = "Biomes";

        public NativeArray<BiomeStruct> NativeBiomeData;

        public void Initialize()
        {
            Instance = this;
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            BiomeData[] loadedBiomes = Resources.LoadAll<BiomeData>(BiomesPath);

            if (!NativeBiomeData.IsCreated)
            {
                NativeBiomeData = new NativeArray<BiomeStruct>(loadedBiomes.Length, Allocator.Persistent);
            }

            for (int i = 0; i < loadedBiomes.Length; i++)
            {
                BiomeData data = loadedBiomes[i];
                NativeBiomeData[i] = new BiomeStruct
                {
                    surfaceBlock = data.surfaceBlock,
                    subSurfaceBlock = data.subSurfaceBlock,
                    underwaterSurfaceBlock = data.underwaterSurfaceBlock,
                    underwaterSubSurfaceBlock = data.underwaterSubSurfaceBlock,
                    subSurfaceDepth = data.subSurfaceDepth,
                    temp = data.targetTemperature,
                    hum = data.targetHumidity,
                    cont = data.targetContinentalness,
                    ero = data.targetErosion,
                    dep = data.targetDepth
                };
            }
        }

        public void Cleanup()
        {
            if (NativeBiomeData.IsCreated)
            {
                NativeBiomeData.Dispose();
            }
        }
    }
}