using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace MinecraftEngine
{
    public struct BlockStruct
    {
        public bool isSolid;
        public bool isTransparent;

        public int texBack;
        public int texFront;
        public int texTop;
        public int texBottom;
        public int texLeft;
        public int texRight;
    }

    public class BlockDatabase : MonoBehaviour
    {
        public static BlockDatabase Instance;

        private const string BlocksPath = "Blocks";
        private Dictionary<byte, BlockData> _blocks = new Dictionary<byte, BlockData>();

        public NativeArray<BlockStruct> NativeBlockData;

        public void Initialize()
        {
            Instance = this;
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            _blocks.Clear();
            BlockData[] loadedBlocks = Resources.LoadAll<BlockData>(BlocksPath);

            if (!NativeBlockData.IsCreated)
            {
                NativeBlockData = new NativeArray<BlockStruct>(256, Allocator.Persistent);
            }

            foreach (BlockData block in loadedBlocks)
            {
                if (!_blocks.ContainsKey(block.blockID))
                {
                    _blocks.Add(block.blockID, block);

                    BlockStruct bStruct = new BlockStruct
                    {
                        isSolid = block.isSolid,
                        isTransparent = block.isTransparent,
                        texBack = block.textureIndices[0],
                        texFront = block.textureIndices[1],
                        texTop = block.textureIndices[2],
                        texBottom = block.textureIndices[3],
                        texLeft = block.textureIndices[4],
                        texRight = block.textureIndices[5]
                    };

                    NativeBlockData[block.blockID] = bStruct;
                }
            }
        }

        public BlockData GetBlock(byte id)
        {
            if (_blocks.TryGetValue(id, out BlockData data)) return data;
            return null;
        }

        public bool IsTransparent(byte id)
        {
            if (_blocks.TryGetValue(id, out BlockData data)) return data.isTransparent;
            return false;
        }

        public bool IsSolid(byte id)
        {
            if (_blocks.TryGetValue(id, out BlockData data)) return data.isSolid;
            return false;
        }

        // Вызывается из WorldManager'а ПРИНУДИТЕЛЬНО
        public void Cleanup()
        {
            if (NativeBlockData.IsCreated)
            {
                NativeBlockData.Dispose();
            }
        }
    }
}