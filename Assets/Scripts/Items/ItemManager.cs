using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MinecraftEngine
{
    public class ItemManager : MonoBehaviour
    {
        public static ItemManager Instance { get; private set; }

        public WorldManager worldManager;
        public Transform playerTransform;
        public PlayerInventory playerInventory;
        public Material chunkMaterial;

        private Queue<ItemEntity> _pool = new Queue<ItemEntity>();
        private Dictionary<ushort, Mesh> _dropMeshCache = new Dictionary<ushort, Mesh>();

        private float _lastDropTime = -999f;
        private const float DropPickupDelay = 0.5f;
        private const float DefaultPickupDelay = 1.0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void SpawnItem(ushort blockID, Vector3 position, float pickupDelay = -1f, Vector3? velocity = null)
        {
            if (blockID == 0) return;

            ItemEntity item = GetFromPool();
            Mesh itemMesh = GetOrCreateDropMesh(blockID);

            if (pickupDelay < 0f)
            {
                pickupDelay = GetEffectivePickupDelay();
            }

            item.Initialize(blockID, position, itemMesh, chunkMaterial, pickupDelay, velocity);
        }

        private float GetEffectivePickupDelay()
        {
            if (Time.time - _lastDropTime < 0.1f)
                return DropPickupDelay;
            return DefaultPickupDelay;
        }

        public void OnInventoryDrop()
        {
            _lastDropTime = Time.time;
        }

        private ItemEntity GetFromPool()
        {
            if (_pool.Count > 0)
            {
                ItemEntity item = _pool.Dequeue();
                item.gameObject.SetActive(true);
                return item;
            }

            GameObject obj = new GameObject("DroppedItem");
            obj.transform.SetParent(this.transform);

            ItemEntity entity = obj.AddComponent<ItemEntity>();

            GameObject model = new GameObject("Model");
            model.transform.SetParent(obj.transform);
            entity.modelTransform = model.transform;

            return entity;
        }

        public void ReturnToPool(ItemEntity item)
        {
            item.gameObject.SetActive(false);
            _pool.Enqueue(item);
        }

        private Mesh GetOrCreateDropMesh(ushort blockID)
        {
            if (_dropMeshCache.TryGetValue(blockID, out Mesh cachedMesh)) return cachedMesh;

            Mesh mesh = new Mesh();

            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3)
            };

            VoxelVertex[] voxelVerts = new VoxelVertex[24];
            int[] triangles = new int[36];

            BlockData bData = BlockDatabase.Instance != null ? BlockDatabase.Instance.GetBlock(blockID) : null;
            int vIndex = 0;
            int tIndex = 0;

            for (int p = 0; p < 6; p++)
            {
                float texIdx = bData != null ? bData.GetTextureIndex(p) : 0;

                Vector3 v0 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 0]] - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 v1 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 1]] - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 v2 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 2]] - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 v3 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 3]] - new Vector3(0.5f, 0.5f, 0.5f);

                Color32 blockColor = new Color32(255, 255, 255, 255);
                if (blockID == 4 && p == 2) blockColor = new Color32(121, 192, 90, 255);
                if (blockID == 10) blockColor = new Color32(121, 192, 90, 255);

                voxelVerts[vIndex + 0] = new VoxelVertex { position = v0, uv = new float3(0, 0, texIdx), color = blockColor };
                voxelVerts[vIndex + 1] = new VoxelVertex { position = v1, uv = new float3(0, 1, texIdx), color = blockColor };
                voxelVerts[vIndex + 2] = new VoxelVertex { position = v2, uv = new float3(1, 0, texIdx), color = blockColor };
                voxelVerts[vIndex + 3] = new VoxelVertex { position = v3, uv = new float3(1, 1, texIdx), color = blockColor };

                triangles[tIndex + 0] = vIndex + 0;
                triangles[tIndex + 1] = vIndex + 1;
                triangles[tIndex + 2] = vIndex + 2;
                triangles[tIndex + 3] = vIndex + 2;
                triangles[tIndex + 4] = vIndex + 1;
                triangles[tIndex + 5] = vIndex + 3;

                vIndex += 4;
                tIndex += 6;
            }

            mesh.SetVertexBufferParams(24, layout);
            mesh.SetVertexBufferData(voxelVerts, 0, 0, 24);
            mesh.SetIndexBufferParams(36, IndexFormat.UInt32);
            mesh.SetIndexBufferData(triangles, 0, 0, 36);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, 36, MeshTopology.Triangles));

            mesh.RecalculateNormals();

            _dropMeshCache.Add(blockID, mesh);
            return mesh;
        }
    }
}