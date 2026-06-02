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

        public byte lightEmission;
        public byte opacity;
        public byte shape; // BlockShape cast to byte for blittable
    }

    public class BlockDatabase : MonoBehaviour
    {
        public static BlockDatabase Instance;

        private const string BlocksPath = "Blocks";
        private const int MaxBlockID = 1024;

        private Dictionary<ushort, BlockData> _blocks = new Dictionary<ushort, BlockData>();

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
                NativeBlockData = new NativeArray<BlockStruct>(MaxBlockID, Allocator.Persistent);
            }

            foreach (BlockData block in loadedBlocks)
            {
                if (block.blockID >= MaxBlockID)
                {
                    Debug.LogWarning($"Block {block.blockName} has ID {block.blockID} >= {MaxBlockID}, skipping.");
                    continue;
                }

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
                        texRight = block.textureIndices[5],
                        lightEmission = block.lightEmission,
                        opacity = block.opacity,
                        shape = (byte)block.shape
                    };

                    NativeBlockData[block.blockID] = bStruct;
                }
            }
        }

        public BlockData GetBlock(ushort id)
        {
            if (_blocks.TryGetValue(id, out BlockData data)) return data;
            return null;
        }

        // Overload for byte callers (backward compat)
        public BlockData GetBlock(byte id) => GetBlock((ushort)id);

        public bool IsTransparent(ushort id)
        {
            if (_blocks.TryGetValue(id, out BlockData data)) return data.isTransparent;
            return false;
        }

        public bool IsTransparent(byte id) => IsTransparent((ushort)id);

        public bool IsSolid(ushort id)
        {
            if (_blocks.TryGetValue(id, out BlockData data)) return data.isSolid;
            return false;
        }

        public bool IsSolid(byte id) => IsSolid((ushort)id);

        public void Cleanup()
        {
            if (NativeBlockData.IsCreated)
            {
                NativeBlockData.Dispose();
            }
        }
    }
}