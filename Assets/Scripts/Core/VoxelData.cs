using Unity.Mathematics;

namespace MinecraftEngine
{
    public static class VoxelData
    {
        public static readonly float3[] VoxelVertices = new float3[8]
        {
            new float3(0f, 0f, 0f), new float3(1f, 0f, 0f), new float3(1f, 1f, 0f), new float3(0f, 1f, 0f),
            new float3(0f, 0f, 1f), new float3(1f, 0f, 1f), new float3(1f, 1f, 1f), new float3(0f, 1f, 1f)
        };

        public static readonly int3[] FaceChecks = new int3[6]
        {
            new int3(0, 0, -1), new int3(0, 0, 1), new int3(0, 1, 0), new int3(0, -1, 0), new int3(-1, 0, 0), new int3(1, 0, 0)
        };

        // ИСПРАВЛЕНИЕ ДЛЯ BURST: Превратили двумерный массив [6,4] в плоский одномерный массив [24].
        public static readonly int[] VoxelTriangles = new int[24]
        {
            0, 3, 1, 2, // Back (Face 0)
            5, 6, 4, 7, // Front (Face 1)
            3, 7, 2, 6, // Top (Face 2)
            1, 5, 0, 4, // Bottom (Face 3)
            4, 7, 0, 3, // Left (Face 4)
            1, 2, 5, 6  // Right (Face 5)
        };

        public static readonly float2[] VoxelUVs = new float2[4]
        {
            new float2(0f, 0f), new float2(0f, 1f), new float2(1f, 0f), new float2(1f, 1f)
        };
    }
}