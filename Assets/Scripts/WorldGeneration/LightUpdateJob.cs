using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MinecraftEngine
{
    public struct LightNode
    {
        public int index;
        public byte light;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, CompileSynchronously = false)]
    public struct LightUpdateJob : IJob
    {
        [ReadOnly] public NativeArray<ushort> voxelMap;
        public NativeArray<byte> lightMap;

        [ReadOnly] public NativeArray<byte> frontLight;
        [ReadOnly] public NativeArray<byte> backLight;
        [ReadOnly] public NativeArray<byte> rightLight;
        [ReadOnly] public NativeArray<byte> leftLight;

        public bool hasFront, hasBack, hasRight, hasLeft;

        [ReadOnly] public NativeArray<BlockStruct> blockDatabase;

        public bool forceSunlightRecalculation;

        public void Execute()
        {
            int chunkWidth = VoxelSettings.ChunkWidth;
            int chunkHeight = VoxelSettings.ChunkHeight;
            int chunkDepth = VoxelSettings.ChunkDepth;

            NativeList<LightNode> bfsQueue = new NativeList<LightNode>(VoxelSettings.ChunkVolume / 2, Allocator.Temp);

            // 1. ПЕРЕСЧЕТ СОЛНЦА (Вертикальный луч)
            if (forceSunlightRecalculation)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    for (int z = 0; z < chunkDepth; z++)
                    {
                        byte currentLight = 15;

                        for (int y = chunkHeight - 1; y >= 0; y--)
                        {
                            int index = x + chunkWidth * (y + chunkHeight * z);
                            int blockID = voxelMap[index] & 0x0FFF;

                            if (blockID != 0 && blockDatabase[blockID].isSolid)
                            {
                                currentLight = 0; // Твердый блок перекрывает солнце
                            }
                            else if (blockID == 11 || blockID == 9)
                            {
                                if (currentLight >= 2) currentLight -= 2;
                                else currentLight = 0;
                            }

                            // ИСПРАВЛЕНИЕ: Мы записываем свет ТОЛЬКО в полупрозрачные блоки/воздух!
                            // В твердых блоках всегда будет храниться 0.
                            if (blockID != 0 && blockDatabase[blockID].isSolid)
                            {
                                lightMap[index] = 0;
                            }
                            else
                            {
                                lightMap[index] = currentLight;
                            }
                        }
                    }
                }
            }

            // 2. ДОБАВЛЕНИЕ ВСЕХ ИСТОЧНИКОВ СВЕТА В ОЧЕРЕДЬ
            // Ищем все блоки (Воздух/Вода), в которых сейчас есть свет от солнца.
            for (int i = 0; i < lightMap.Length; i++)
            {
                if (lightMap[i] > 1)
                {
                    bfsQueue.Add(new LightNode { index = i, light = lightMap[i] });
                }
            }

            // 3. ЧТЕНИЕ СВЕТА ОТ СОСЕДЕЙ (С краев чанка)
            for (int y = 0; y < chunkHeight; y++)
            {
                for (int i = 0; i < chunkWidth; i++)
                {
                    int sliceIndex = i + chunkWidth * y;

                    if (hasRight && rightLight[sliceIndex] > 1)
                        CheckAndQueueLight((chunkWidth - 1) + chunkWidth * (y + chunkHeight * i), (byte)(rightLight[sliceIndex] - 1), ref bfsQueue);

                    if (hasLeft && leftLight[sliceIndex] > 1)
                        CheckAndQueueLight(0 + chunkWidth * (y + chunkHeight * i), (byte)(leftLight[sliceIndex] - 1), ref bfsQueue);

                    if (hasFront && frontLight[sliceIndex] > 1)
                        CheckAndQueueLight(i + chunkWidth * (y + chunkHeight * (chunkDepth - 1)), (byte)(frontLight[sliceIndex] - 1), ref bfsQueue);

                    if (hasBack && backLight[sliceIndex] > 1)
                        CheckAndQueueLight(i + chunkWidth * (y + chunkHeight * 0), (byte)(backLight[sliceIndex] - 1), ref bfsQueue);
                }
            }

            // 4. FLOOD FILL
            int queueHead = 0;
            while (queueHead < bfsQueue.Length)
            {
                LightNode node = bfsQueue[queueHead++];
                int currentIndex = node.index;
                byte currentLight = node.light;

                // Защита от дубликатов в очереди
                if (lightMap[currentIndex] != currentLight) continue;
                if (currentLight <= 1) continue;

                int cx = currentIndex % chunkWidth;
                int cy = (currentIndex / chunkWidth) % chunkHeight;
                int cz = currentIndex / (chunkWidth * chunkHeight);

                // Распространяем свет в 6 сторон. 
                PropagateLight(cx + 1, cy, cz, currentLight, ref bfsQueue);
                PropagateLight(cx - 1, cy, cz, currentLight, ref bfsQueue);
                PropagateLight(cx, cy + 1, cz, currentLight, ref bfsQueue);

                // ВНИМАНИЕ: Солнечный свет (15) падает вниз без потерь!
                byte downwardLight = currentLight == 15 ? (byte)16 : currentLight;
                PropagateLight(cx, cy - 1, cz, downwardLight, ref bfsQueue);

                PropagateLight(cx, cy, cz + 1, currentLight, ref bfsQueue);
                PropagateLight(cx, cy, cz - 1, currentLight, ref bfsQueue);
            }

            bfsQueue.Dispose();
        }

        private void CheckAndQueueLight(int index, byte proposedLight, ref NativeList<LightNode> queue)
        {
            int blockID = voxelMap[index] & 0x0FFF;

            // ИСПРАВЛЕНИЕ: Свет НЕ МОЖЕТ зайти в твердый блок.
            // Мы просто игнорируем его. Грань этого блока будет освещена за счет того, 
            // что воздух перед ним получил свет!
            if (blockID != 0 && blockDatabase[blockID].isSolid) return;

            // Воздух съедает 1, Вода 2
            byte absorption = (byte)((blockID == 11 || blockID == 9) ? 2 : 1);
            if (absorption >= proposedLight) return;

            byte finalLight = (byte)(proposedLight - absorption);

            if (finalLight > lightMap[index])
            {
                lightMap[index] = finalLight;
                queue.Add(new LightNode { index = index, light = finalLight });
            }
        }

        private void PropagateLight(int nx, int ny, int nz, byte currentLight, ref NativeList<LightNode> queue)
        {
            if (nx >= 0 && nx < VoxelSettings.ChunkWidth && ny >= 0 && ny < VoxelSettings.ChunkHeight && nz >= 0 && nz < VoxelSettings.ChunkDepth)
            {
                int nIndex = nx + VoxelSettings.ChunkWidth * (ny + VoxelSettings.ChunkHeight * nz);
                CheckAndQueueLight(nIndex, currentLight, ref queue);
            }
        }
    }
}