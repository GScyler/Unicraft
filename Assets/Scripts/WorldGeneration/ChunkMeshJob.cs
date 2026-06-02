using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MinecraftEngine
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VoxelVertex
    {
        public float3 position;
        public Color32 color;
        public float3 uv;
    }

    [BurstCompile]
    public struct ChunkMeshJob : IJob
    {
        [ReadOnly] public NativeArray<ushort> voxelMap;
        [ReadOnly] public NativeArray<byte> lightMap;

        [ReadOnly] public NativeArray<ushort> frontSlice;
        [ReadOnly] public NativeArray<ushort> backSlice;
        [ReadOnly] public NativeArray<ushort> rightSlice;
        [ReadOnly] public NativeArray<ushort> leftSlice;

        [ReadOnly] public NativeArray<byte> frontLight;
        [ReadOnly] public NativeArray<byte> backLight;
        [ReadOnly] public NativeArray<byte> rightLight;
        [ReadOnly] public NativeArray<byte> leftLight;

        public bool hasFront, hasBack, hasRight, hasLeft;
        public bool hasLightFront, hasLightBack, hasLightRight, hasLightLeft;

        [ReadOnly] public NativeArray<BlockStruct> blockDatabase;

        public float2 chunkWorldPosition;

        public float minLight;
        public float maxLight;
        public float lightGamma;

        public NativeList<VoxelVertex> vertices;
        public NativeList<int> triangles;
        public NativeList<int> cutoutTriangles;
        public NativeList<int> transparentTriangles;

        public void Execute()
        {
            int vertexIndex = 0;

            for (int y = 0; y < VoxelSettings.ChunkHeight; y++)
            {
                for (int x = 0; x < VoxelSettings.ChunkWidth; x++)
                {
                    for (int z = 0; z < VoxelSettings.ChunkDepth; z++)
                    {
                        int index = GetIndex(x, y, z);
                        ushort blockData = voxelMap[index];

                        int blockID = blockData & 0x0FFF;
                        int blockState = (blockData >> 12) & 0xF;

                        if (blockID == 0) continue;

                        int3 pos = new int3(x, y, z);
                        bool isFluid = (blockID == 11);
                        bool isCutout = (blockID == 10 || blockID == 9);

                        bool isWaterAbove = false;
                        if (isFluid && y < VoxelSettings.ChunkHeight - 1)
                        {
                            isWaterAbove = ((voxelMap[GetIndex(x, y + 1, z)] & 0x0FFF) == 11);
                        }

                        for (int p = 0; p < 6; p++)
                        {
                            int3 neighborPos = pos + VoxelData.FaceChecks[p];
                            byte neighborType = GetNeighborBlockType(neighborPos);

                            if (CheckIfFaceIsVisible((byte)blockID, neighborType, p))
                            {
                                int mappedFace = p;
                                int uvRotation = 0;

                                if (blockID == 8)
                                {
                                    if (blockState == 1)
                                    {
                                        if (p == 4 || p == 5) mappedFace = 2;
                                        else
                                        {
                                            mappedFace = 4;
                                            if (p == 0 || p == 1) uvRotation = 1;
                                            if (p == 2 || p == 3) uvRotation = 1;
                                        }
                                    }
                                    else if (blockState == 2)
                                    {
                                        if (p == 0 || p == 1) mappedFace = 2;
                                        else
                                        {
                                            mappedFace = 4;
                                            if (p == 4 || p == 5) uvRotation = 1;
                                        }
                                    }
                                }

                                float textureIndex = GetTextureIndexFromDatabase((byte)blockID, mappedFace);

                                float3 v0 = VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 0]];
                                float3 v1 = VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 1]];
                                float3 v2 = VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 2]];
                                float3 v3 = VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 3]];

                                if (isFluid)
                                {
                                    if (!isWaterAbove)
                                    {
                                        if (v0.y == 1f) v0.y -= 0.125f;
                                        if (v1.y == 1f) v1.y -= 0.125f;
                                        if (v2.y == 1f) v2.y -= 0.125f;
                                        if (v3.y == 1f) v3.y -= 0.125f;
                                    }

                                    if (neighborType == 10 || neighborType == 9)
                                    {
                                        float shrink = 0.001f;
                                        float3 normal = new float3(-VoxelData.FaceChecks[p].x, -VoxelData.FaceChecks[p].y, -VoxelData.FaceChecks[p].z);
                                        v0 += normal * shrink;
                                        v1 += normal * shrink;
                                        v2 += normal * shrink;
                                        v3 += normal * shrink;
                                    }
                                }

                                float2 uv0_2d = VoxelData.VoxelUVs[0];
                                float2 uv1_2d = VoxelData.VoxelUVs[1];
                                float2 uv2_2d = VoxelData.VoxelUVs[2];
                                float2 uv3_2d = VoxelData.VoxelUVs[3];

                                for (int i = 0; i < uvRotation; i++)
                                {
                                    uv0_2d = new float2(uv0_2d.y, 1f - uv0_2d.x);
                                    uv1_2d = new float2(uv1_2d.y, 1f - uv1_2d.x);
                                    uv2_2d = new float2(uv2_2d.y, 1f - uv2_2d.x);
                                    uv3_2d = new float2(uv3_2d.y, 1f - uv3_2d.x);
                                }

                                float3 uv0 = new float3(uv0_2d.x, uv0_2d.y, textureIndex);
                                float3 uv1 = new float3(uv1_2d.x, uv1_2d.y, textureIndex);
                                float3 uv2 = new float3(uv2_2d.x, uv2_2d.y, textureIndex);
                                float3 uv3 = new float3(uv3_2d.x, uv3_2d.y, textureIndex);

                                // ИСПРАВЛЕНИЕ: МЫ ЧИТАЕМ СВЕТ ТОЛЬКО ИЗ ВОЗДУХА ПРЯМО ПЕРЕД НАМИ!
                                // Теперь твердые блоки всегда имеют свет 0. 
                                // Если перед стеной туннеля стоит воздух, стена окрашивается в его цвет.
                                byte lightLevel = GetNeighborLight(neighborPos);

                                float linearLight = lightLevel / 15f;
                                float curveLight = math.pow(linearLight, lightGamma);
                                float lightIntensity = math.clamp(curveLight * maxLight + minLight, minLight, maxLight);

                                if (p == 0 || p == 1) lightIntensity *= 0.8f;
                                else if (p == 4 || p == 5) lightIntensity *= 0.6f;
                                else if (p == 3) lightIntensity *= 0.5f;

                                byte colorByte = (byte)(lightIntensity * 255);
                                Color32 finalColor = new Color32(colorByte, colorByte, colorByte, 255);

                                vertices.Add(new VoxelVertex { position = pos + v0, uv = uv0, color = finalColor });
                                vertices.Add(new VoxelVertex { position = pos + v1, uv = uv1, color = finalColor });
                                vertices.Add(new VoxelVertex { position = pos + v2, uv = uv2, color = finalColor });
                                vertices.Add(new VoxelVertex { position = pos + v3, uv = uv3, color = finalColor });

                                if (isFluid)
                                {
                                    transparentTriangles.Add(vertexIndex); transparentTriangles.Add(vertexIndex + 1); transparentTriangles.Add(vertexIndex + 2);
                                    transparentTriangles.Add(vertexIndex + 2); transparentTriangles.Add(vertexIndex + 1); transparentTriangles.Add(vertexIndex + 3);
                                }
                                else if (isCutout)
                                {
                                    cutoutTriangles.Add(vertexIndex); cutoutTriangles.Add(vertexIndex + 1); cutoutTriangles.Add(vertexIndex + 2);
                                    cutoutTriangles.Add(vertexIndex + 2); cutoutTriangles.Add(vertexIndex + 1); cutoutTriangles.Add(vertexIndex + 3);
                                }
                                else
                                {
                                    triangles.Add(vertexIndex); triangles.Add(vertexIndex + 1); triangles.Add(vertexIndex + 2);
                                    triangles.Add(vertexIndex + 2); triangles.Add(vertexIndex + 1); triangles.Add(vertexIndex + 3);
                                }

                                vertexIndex += 4;
                            }
                        }
                    }
                }
            }
        }

        private byte GetNeighborBlockType(int3 neighborPos)
        {
            if (neighborPos.y < 0 || neighborPos.y >= VoxelSettings.ChunkHeight) return 0;

            ushort data = 0;
            if (neighborPos.x < 0) { if (hasLeft) data = leftSlice[GetSliceIndex(neighborPos.z, neighborPos.y)]; else return 0; }
            else if (neighborPos.x >= VoxelSettings.ChunkWidth) { if (hasRight) data = rightSlice[GetSliceIndex(neighborPos.z, neighborPos.y)]; else return 0; }
            else if (neighborPos.z < 0) { if (hasBack) data = backSlice[GetSliceIndex(neighborPos.x, neighborPos.y)]; else return 0; }
            else if (neighborPos.z >= VoxelSettings.ChunkDepth) { if (hasFront) data = frontSlice[GetSliceIndex(neighborPos.x, neighborPos.y)]; else return 0; }
            else data = voxelMap[GetIndex(neighborPos.x, neighborPos.y, neighborPos.z)];

            return (byte)(data & 0x0FFF);
        }

        // ИСПРАВЛЕНИЕ: Чтение света с краев чанка
        // Если соседа нет, мы возвращаем 0, а не 15! Если чанк еще не прогрузился, пусть лучше там будет чернота,
        // чем яркий солнечный квадрат в середине подземелья.
        private byte GetNeighborLight(int3 neighborPos)
        {
            if (neighborPos.y < 0 || neighborPos.y >= VoxelSettings.ChunkHeight) return 15; // Небо всегда светлое

            if (neighborPos.x < 0) { if (hasLightLeft) return leftLight[GetSliceIndex(neighborPos.z, neighborPos.y)]; else return 0; }
            else if (neighborPos.x >= VoxelSettings.ChunkWidth) { if (hasLightRight) return rightLight[GetSliceIndex(neighborPos.z, neighborPos.y)]; else return 0; }
            else if (neighborPos.z < 0) { if (hasLightBack) return backLight[GetSliceIndex(neighborPos.x, neighborPos.y)]; else return 0; }
            else if (neighborPos.z >= VoxelSettings.ChunkDepth) { if (hasLightFront) return frontLight[GetSliceIndex(neighborPos.x, neighborPos.y)]; else return 0; }

            return lightMap[GetIndex(neighborPos.x, neighborPos.y, neighborPos.z)];
        }

        private bool CheckIfFaceIsVisible(byte currentBlockType, byte neighborBlockType, int faceIndex)
        {
            bool isCurrentTransparent = blockDatabase[currentBlockType].isTransparent;
            bool isNeighborTransparent = blockDatabase[neighborBlockType].isTransparent;

            if (currentBlockType == 11)
            {
                if (neighborBlockType == 11) return false;
                if (neighborBlockType == 10 || neighborBlockType == 9)
                {
                    if (faceIndex != 2) return false;
                }
            }
            if (currentBlockType == neighborBlockType && isCurrentTransparent) return false;

            return isNeighborTransparent;
        }

        private float GetTextureIndexFromDatabase(byte blockID, int faceIndex)
        {
            BlockStruct data = blockDatabase[blockID];
            switch (faceIndex)
            {
                case 0: return data.texBack;
                case 1: return data.texFront;
                case 2: return data.texTop;
                case 3: return data.texBottom;
                case 4: return data.texLeft;
                case 5: return data.texRight;
                default: return 0;
            }
        }

        private int GetIndex(int x, int y, int z) => x + VoxelSettings.ChunkWidth * (y + VoxelSettings.ChunkHeight * z);
        private int GetSliceIndex(int xOrZ, int y) => xOrZ + VoxelSettings.ChunkWidth * y;
    }
}