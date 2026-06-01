using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace MinecraftEngine
{
    [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = false)]
    public struct TerrainJob : IJob
    {
        public NativeArray<ushort> voxelMap;

        // ИСПРАВЛЕНИЕ: Мы полностью удалили lightMap отсюда. Ландшафт занимается ТОЛЬКО ландшафтом!

        public float2 chunkWorldPosition;
        [ReadOnly] public NativeArray<BiomeStruct> biomeDatabase;
        [ReadOnly] public NativeArray<BlockStruct> blockDatabase;

        public int worldSeed;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetRandom(int3 pos)
        {
            uint hash = (uint)((pos.x + worldSeed) * 73856093 ^ pos.y * 19349663 ^ (pos.z + worldSeed) * 83492791);
            hash = (hash ^ (hash >> 16)) * 0x85ebca6b;
            hash = (hash ^ (hash >> 13)) * 0xc2b2ae35;
            hash = hash ^ (hash >> 16);
            return (float)hash / uint.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetNoise(float x, float z, float scale, float offset = 0)
        {
            return noise.snoise(new float2(x * scale + offset, z * scale + offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetNoise3D(float3 pos, float scale, float offset = 0)
        {
            return noise.snoise(pos * scale + new float3(offset, offset, offset));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetBiomeDistance(BiomeStruct biome, float temp, float hum, float cont, float ero, float dep)
        {
            float dt = biome.temp - temp;
            float dh = biome.hum - hum;
            float dc = biome.cont - cont;
            float de = biome.ero - ero;
            float dd = biome.dep - dep;
            return (dt * dt) + (dh * dh) + (dc * dc) + (de * de) + (dd * dd);
        }

        public void Execute()
        {
            int chunkWidth = VoxelSettings.ChunkWidth;
            int chunkHeight = VoxelSettings.ChunkHeight;
            int chunkDepth = VoxelSettings.ChunkDepth;
            int worldYOffset = VoxelSettings.WorldYOffset;
            int seaLevel = VoxelSettings.SeaLevel;

            float2 seedOffset = new float2(worldSeed % 10000, (worldSeed / 10000f) % 10000);

            float scaleCont = 0.001f;
            float scaleEro = 0.0015f;
            float scaleTemp = 0.0005f;
            float scaleHum = 0.0005f;

            for (int z = 0; z < chunkDepth; z++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float worldX = chunkWorldPosition.x + x + seedOffset.x;
                    float worldZ = chunkWorldPosition.y + z + seedOffset.y;

                    float continentalness = GetNoise(worldX, worldZ, scaleCont, 1000f);
                    float erosion = GetNoise(worldX, worldZ, scaleEro, 2000f);
                    float temperature = GetNoise(worldX, worldZ, scaleTemp, 3000f);
                    float humidity = GetNoise(worldX, worldZ, scaleHum, 4000f);
                    float peaks = GetNoise(worldX, worldZ, 0.005f, 5000f);

                    float baseTerrainHeight = 64f + (continentalness * 60f);

                    if (erosion < 0f)
                    {
                        baseTerrainHeight += peaks * 60f * math.abs(erosion);
                    }

                    int dirtDepth = -1;
                    BiomeStruct currentBiome = biomeDatabase[0];

                    for (int y = chunkHeight - 1; y >= 0; y--)
                    {
                        int index = x + chunkWidth * (y + chunkHeight * z);
                        int worldY = y - worldYOffset;

                        int3 pos = new int3((int)worldX, worldY, (int)worldZ);
                        float3 posFloat3 = new float3(worldX, worldY, worldZ);

                        float depth = math.clamp(worldY / 100f, -1f, 1f);

                        float minDistance = float.MaxValue;
                        int bestBiomeIndex = 0;

                        for (int i = 0; i < biomeDatabase.Length; i++)
                        {
                            float dist = GetBiomeDistance(biomeDatabase[i], temperature, humidity, continentalness, erosion, depth);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestBiomeIndex = i;
                            }
                        }
                        currentBiome = biomeDatabase[bestBiomeIndex];

                        if (worldY <= -60)
                        {
                            if (worldY <= -64 || GetRandom(pos) < (-60f - worldY) * 0.25f)
                            {
                                voxelMap[index] = 1;
                                continue;
                            }
                        }

                        float density = (baseTerrainHeight - worldY) + (GetNoise3D(posFloat3, 0.02f) * 10f);

                        float caveThreshold = math.lerp(0.5f, 0.8f, math.saturate((worldY + 20f) / 80f));
                        bool isCheeseCave = GetNoise3D(posFloat3, 0.015f) > caveThreshold;
                        bool isSpaghettiCave = math.abs(GetNoise3D(posFloat3, 0.01f, 500f)) < 0.03f;

                        if (density > 0f && !isCheeseCave && !isSpaghettiCave)
                        {
                            if (dirtDepth == -1)
                            {
                                dirtDepth = 0;
                                voxelMap[index] = (ushort)(worldY >= seaLevel - 1 ? currentBiome.surfaceBlock : currentBiome.underwaterSurfaceBlock);
                            }
                            else if (dirtDepth > -1 && dirtDepth < currentBiome.subSurfaceDepth)
                            {
                                dirtDepth++;
                                voxelMap[index] = (ushort)(worldY >= seaLevel - 1 ? currentBiome.subSurfaceBlock : currentBiome.underwaterSubSurfaceBlock);
                            }
                            else
                            {
                                if (worldY <= 0)
                                {
                                    if (worldY < -8 || GetRandom(pos) < (0f - worldY) * 0.125f)
                                    {
                                        voxelMap[index] = (ushort)(GetNoise3D(posFloat3, 0.05f) > 0.6f ? 20 : 19);
                                        continue;
                                    }
                                }
                                voxelMap[index] = 2;
                            }
                        }
                        else
                        {
                            dirtDepth = -1;
                            voxelMap[index] = (ushort)(worldY <= seaLevel ? 11 : 0);
                        }
                    }
                }
            }
        }
    }
}