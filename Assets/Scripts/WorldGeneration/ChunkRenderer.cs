using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MinecraftEngine
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class ChunkRenderer : MonoBehaviour
    {
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private Mesh _mesh;

        public NativeArray<ushort> VoxelMap;
        public NativeArray<byte> LightMap;

        private JobHandle _terrainJobHandle;
        private JobHandle _lightJobHandle;
        private JobHandle _meshJobHandle;

        private NativeList<VoxelVertex> _vertices;
        private NativeList<int> _triangles;
        private NativeList<int> _cutoutTriangles;
        private NativeList<int> _transparentTriangles;

        private NativeArray<ushort> _frontSlice;
        private NativeArray<ushort> _backSlice;
        private NativeArray<ushort> _rightSlice;
        private NativeArray<ushort> _leftSlice;

        private NativeArray<byte> _frontLight;
        private NativeArray<byte> _backLight;
        private NativeArray<byte> _rightLight;
        private NativeArray<byte> _leftLight;

        public bool IsGeneratingTerrain { get; private set; } = false;
        public bool IsGeneratingLight { get; private set; } = false;
        public bool IsGeneratingMesh { get; private set; } = false;

        public bool IsReady { get; private set; } = false;
        public bool IsCancelled { get; private set; } = false;
        public bool IsModified { get; private set; } = false;
        public bool HasGeneratedTerrain { get; private set; } = false;
        public bool HasGeneratedLight { get; private set; } = false;

        public int2 Coord;
        private WorldManager _worldManager;

        private NativeArray<BlockStruct> _blockDatabase;
        private NativeArray<BiomeStruct> _biomeDatabase;
        private int _worldSeed;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            _mesh = new Mesh { name = "ChunkMesh" };
            _mesh.MarkDynamic();

            VoxelMap = new NativeArray<ushort>(VoxelSettings.ChunkVolume, Allocator.Persistent);
            LightMap = new NativeArray<byte>(VoxelSettings.ChunkVolume, Allocator.Persistent);

            _vertices = new NativeList<VoxelVertex>(10000, Allocator.Persistent);
            _triangles = new NativeList<int>(15000, Allocator.Persistent);
            _cutoutTriangles = new NativeList<int>(5000, Allocator.Persistent);
            _transparentTriangles = new NativeList<int>(5000, Allocator.Persistent);
        }

        public void Init(int2 coord, WorldManager manager, NativeArray<BlockStruct> blocks, NativeArray<BiomeStruct> biomes, int seed)
        {
            Coord = coord;
            _worldManager = manager;
            _blockDatabase = blocks;
            _biomeDatabase = biomes;
            _worldSeed = seed;

            gameObject.SetActive(true);
            IsReady = false;
            IsCancelled = false;
            IsModified = false;
            HasGeneratedTerrain = false;
            HasGeneratedLight = false;

            CompleteAllJobs();

            for (int i = 0; i < LightMap.Length; i++) LightMap[i] = 0;

            _mesh.Clear();
            _meshRenderer.enabled = false;
        }

        public void StartTerrainJob()
        {
            TerrainJob terrainJob = new TerrainJob
            {
                voxelMap = VoxelMap,
                // ИСПРАВЛЕНИЕ: Мы убрали LightMap из TerrainJob
                chunkWorldPosition = new float2(Coord.x * VoxelSettings.ChunkWidth, Coord.y * VoxelSettings.ChunkDepth),
                biomeDatabase = _biomeDatabase,
                blockDatabase = _blockDatabase,
                worldSeed = _worldSeed
            };
            _terrainJobHandle = terrainJob.Schedule();
            IsGeneratingTerrain = true;
        }

        public bool IsTerrainJobCompleted() => _terrainJobHandle.IsCompleted;

        public void CompleteTerrainJob()
        {
            _terrainJobHandle.Complete();
            IsGeneratingTerrain = false;
            HasGeneratedTerrain = true;
        }

        private NativeArray<ushort> ExtractSlice(NativeArray<ushort> fullMap, bool isXAxis, int offsetAxisVal)
        {
            int sliceVolume = 16 * VoxelSettings.ChunkHeight;
            NativeArray<ushort> slice = new NativeArray<ushort>(sliceVolume, Allocator.Persistent);

            for (int y = 0; y < VoxelSettings.ChunkHeight; y++)
            {
                for (int i = 0; i < 16; i++)
                {
                    int x = isXAxis ? offsetAxisVal : i;
                    int z = isXAxis ? i : offsetAxisVal;
                    int fullIndex = x + VoxelSettings.ChunkWidth * (y + VoxelSettings.ChunkHeight * z);
                    slice[i + 16 * y] = fullMap[fullIndex];
                }
            }
            return slice;
        }

        private NativeArray<byte> ExtractLightSlice(NativeArray<byte> fullLightMap, bool isXAxis, int offsetAxisVal)
        {
            int sliceVolume = 16 * VoxelSettings.ChunkHeight;
            NativeArray<byte> slice = new NativeArray<byte>(sliceVolume, Allocator.Persistent);

            for (int y = 0; y < VoxelSettings.ChunkHeight; y++)
            {
                for (int i = 0; i < 16; i++)
                {
                    int x = isXAxis ? offsetAxisVal : i;
                    int z = isXAxis ? i : offsetAxisVal;
                    int fullIndex = x + VoxelSettings.ChunkWidth * (y + VoxelSettings.ChunkHeight * z);
                    slice[i + 16 * y] = fullLightMap[fullIndex];
                }
            }
            return slice;
        }

        public void StartLightJob(bool forceRecalculation = false)
        {
            if (IsGeneratingLight)
            {
                _lightJobHandle.Complete();
                IsGeneratingLight = false;
            }

            // ИСПРАВЛЕНИЕ ЗАГЛУШЕК СВЕТА В МЕНЕДЖЕРЕ
            // Теперь мы берем соседей только если они HasGeneratedLight
            bool hF = _worldManager.TryGetChunkMap(new int2(Coord.x, Coord.y + 1), out _, out NativeArray<byte> frontLightFull);
            bool hB = _worldManager.TryGetChunkMap(new int2(Coord.x, Coord.y - 1), out _, out NativeArray<byte> backLightFull);
            bool hR = _worldManager.TryGetChunkMap(new int2(Coord.x + 1, Coord.y), out _, out NativeArray<byte> rightLightFull);
            bool hL = _worldManager.TryGetChunkMap(new int2(Coord.x - 1, Coord.y), out _, out NativeArray<byte> leftLightFull);

            if (!hF) frontLightFull = LightMap;
            if (!hB) backLightFull = LightMap;
            if (!hR) rightLightFull = LightMap;
            if (!hL) leftLightFull = LightMap;

            if (_frontLight.IsCreated) _frontLight.Dispose();
            if (_backLight.IsCreated) _backLight.Dispose();
            if (_rightLight.IsCreated) _rightLight.Dispose();
            if (_leftLight.IsCreated) _leftLight.Dispose();

            _frontLight = hF ? ExtractLightSlice(frontLightFull, false, 0) : new NativeArray<byte>(1, Allocator.Persistent);
            _backLight = hB ? ExtractLightSlice(backLightFull, false, 15) : new NativeArray<byte>(1, Allocator.Persistent);
            _rightLight = hR ? ExtractLightSlice(rightLightFull, true, 0) : new NativeArray<byte>(1, Allocator.Persistent);
            _leftLight = hL ? ExtractLightSlice(leftLightFull, true, 15) : new NativeArray<byte>(1, Allocator.Persistent);

            LightUpdateJob lightJob = new LightUpdateJob
            {
                voxelMap = VoxelMap,
                lightMap = LightMap,
                frontLight = _frontLight,
                hasFront = hF,
                backLight = _backLight,
                hasBack = hB,
                rightLight = _rightLight,
                hasRight = hR,
                leftLight = _leftLight,
                hasLeft = hL,
                blockDatabase = _blockDatabase,

                // ВАЖНО: Мы пересчитываем солнце всегда, когда чанк впервые генерируется (!HasGeneratedLight)
                forceSunlightRecalculation = forceRecalculation || !HasGeneratedLight
            };

            _lightJobHandle = lightJob.Schedule();
            IsGeneratingLight = true;
        }

        public bool IsLightJobCompleted() => _lightJobHandle.IsCompleted;

        public void CompleteLightJob()
        {
            _lightJobHandle.Complete();
            IsGeneratingLight = false;
            HasGeneratedLight = true;
        }

        public void StartMeshJob()
        {
            if (IsGeneratingMesh)
            {
                _meshJobHandle.Complete();
                IsGeneratingMesh = false;
            }

            _vertices.Clear();
            _triangles.Clear();
            _cutoutTriangles.Clear();
            _transparentTriangles.Clear();

            bool hF = _worldManager.TryGetChunkMap(new int2(Coord.x, Coord.y + 1), out NativeArray<ushort> fFull, out NativeArray<byte> fLightFull);
            bool hB = _worldManager.TryGetChunkMap(new int2(Coord.x, Coord.y - 1), out NativeArray<ushort> bFull, out NativeArray<byte> bLightFull);
            bool hR = _worldManager.TryGetChunkMap(new int2(Coord.x + 1, Coord.y), out NativeArray<ushort> rFull, out NativeArray<byte> rLightFull);
            bool hL = _worldManager.TryGetChunkMap(new int2(Coord.x - 1, Coord.y), out NativeArray<ushort> lFull, out NativeArray<byte> lLightFull);

            if (!hF) { fFull = VoxelMap; fLightFull = LightMap; }
            if (!hB) { bFull = VoxelMap; bLightFull = LightMap; }
            if (!hR) { rFull = VoxelMap; rLightFull = LightMap; }
            if (!hL) { lFull = VoxelMap; lLightFull = LightMap; }

            if (_frontSlice.IsCreated) _frontSlice.Dispose();
            if (_backSlice.IsCreated) _backSlice.Dispose();
            if (_rightSlice.IsCreated) _rightSlice.Dispose();
            if (_leftSlice.IsCreated) _leftSlice.Dispose();

            if (_frontLight.IsCreated) _frontLight.Dispose();
            if (_backLight.IsCreated) _backLight.Dispose();
            if (_rightLight.IsCreated) _rightLight.Dispose();
            if (_leftLight.IsCreated) _leftLight.Dispose();

            _frontSlice = hF ? ExtractSlice(fFull, false, 0) : new NativeArray<ushort>(1, Allocator.Persistent);
            _backSlice = hB ? ExtractSlice(bFull, false, 15) : new NativeArray<ushort>(1, Allocator.Persistent);
            _rightSlice = hR ? ExtractSlice(rFull, true, 0) : new NativeArray<ushort>(1, Allocator.Persistent);
            _leftSlice = hL ? ExtractSlice(lFull, true, 15) : new NativeArray<ushort>(1, Allocator.Persistent);

            _frontLight = hF ? ExtractLightSlice(fLightFull, false, 0) : new NativeArray<byte>(1, Allocator.Persistent);
            _backLight = hB ? ExtractLightSlice(bLightFull, false, 15) : new NativeArray<byte>(1, Allocator.Persistent);
            _rightLight = hR ? ExtractLightSlice(rLightFull, true, 0) : new NativeArray<byte>(1, Allocator.Persistent);
            _leftLight = hL ? ExtractLightSlice(lLightFull, true, 15) : new NativeArray<byte>(1, Allocator.Persistent);

            ChunkMeshJob meshJob = new ChunkMeshJob
            {
                voxelMap = VoxelMap,
                lightMap = LightMap,
                vertices = _vertices,
                triangles = _triangles,
                cutoutTriangles = _cutoutTriangles,
                transparentTriangles = _transparentTriangles,

                frontSlice = _frontSlice,
                frontLight = _frontLight,
                hasFront = hF,
                backSlice = _backSlice,
                backLight = _backLight,
                hasBack = hB,
                rightSlice = _rightSlice,
                rightLight = _rightLight,
                hasRight = hR,
                leftSlice = _leftSlice,
                leftLight = _leftLight,
                hasLeft = hL,

                blockDatabase = _blockDatabase,
                chunkWorldPosition = new float2(Coord.x * VoxelSettings.ChunkWidth, Coord.y * VoxelSettings.ChunkDepth),

                minLight = _worldManager.minLight,
                maxLight = _worldManager.maxLight,
                lightGamma = _worldManager.lightGamma
            };

            _meshJobHandle = meshJob.Schedule();
            IsGeneratingMesh = true;
        }

        public bool IsMeshJobCompleted() => _meshJobHandle.IsCompleted;

        public void CompleteMeshAndApply()
        {
            _meshJobHandle.Complete();
            IsGeneratingMesh = false;

            if (IsCancelled) return;

            _mesh.Clear();

            int vertexCount = _vertices.Length;
            int solidCount = _triangles.Length;
            int cutoutCount = _cutoutTriangles.Length;
            int transparentCount = _transparentTriangles.Length;
            int totalIndexCount = solidCount + cutoutCount + transparentCount;

            if (vertexCount > 0 && totalIndexCount > 0)
            {
                var layout = new[]
                {
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 3)
                };

                _mesh.SetVertexBufferParams(vertexCount, layout);
                _mesh.SetVertexBufferData(_vertices.AsArray(), 0, 0, vertexCount);

                var combinedIndices = new NativeArray<int>(totalIndexCount, Allocator.Temp);

                if (solidCount > 0) NativeArray<int>.Copy(_triangles.AsArray(), 0, combinedIndices, 0, solidCount);
                if (cutoutCount > 0) NativeArray<int>.Copy(_cutoutTriangles.AsArray(), 0, combinedIndices, solidCount, cutoutCount);
                if (transparentCount > 0) NativeArray<int>.Copy(_transparentTriangles.AsArray(), 0, combinedIndices, solidCount + cutoutCount, transparentCount);

                _mesh.SetIndexBufferParams(totalIndexCount, IndexFormat.UInt32);
                _mesh.SetIndexBufferData(combinedIndices, 0, 0, totalIndexCount);

                if (_meshRenderer.sharedMaterials.Length != 3)
                {
                    Material solidMat = _meshRenderer.sharedMaterials[0];

                    Material cutoutMat = new Material(solidMat);
                    cutoutMat.name = "ChunkCutoutMat";
                    cutoutMat.SetFloat("_ZWrite", 1f);
                    cutoutMat.SetFloat("_SrcBlend", 1f);
                    cutoutMat.SetFloat("_DstBlend", 0f);
                    cutoutMat.renderQueue = 2450;

                    Material transMat = new Material(solidMat);
                    transMat.name = "ChunkTransparentMat";
                    transMat.SetFloat("_ZWrite", 0f);
                    transMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                    transMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    transMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    transMat.renderQueue = 3000;

                    _meshRenderer.sharedMaterials = new Material[] { solidMat, cutoutMat, transMat };
                }

                _mesh.subMeshCount = 3;
                _mesh.SetSubMesh(0, new SubMeshDescriptor(0, solidCount, MeshTopology.Triangles));
                _mesh.SetSubMesh(1, new SubMeshDescriptor(solidCount, cutoutCount, MeshTopology.Triangles));
                _mesh.SetSubMesh(2, new SubMeshDescriptor(solidCount + cutoutCount, transparentCount, MeshTopology.Triangles));

                combinedIndices.Dispose();

                Bounds chunkBounds = new Bounds(
                    new Vector3(VoxelSettings.ChunkWidth / 2f, 0f, VoxelSettings.ChunkDepth / 2f),
                    new Vector3(VoxelSettings.ChunkWidth, VoxelSettings.ChunkHeight, VoxelSettings.ChunkDepth)
                );
                _mesh.bounds = chunkBounds;
                _mesh.RecalculateNormals();

                _meshFilter.mesh = _mesh;
                _meshRenderer.enabled = true;
            }

            IsReady = true;
        }

        public void ModifyBlock(int3 localPos, ushort blockData, bool immediateUpdate = true)
        {
            if (IsGeneratingMesh) { _meshJobHandle.Complete(); IsGeneratingMesh = false; }
            if (IsGeneratingLight) { _lightJobHandle.Complete(); IsGeneratingLight = false; HasGeneratedLight = true; }
            if (IsGeneratingTerrain) { _terrainJobHandle.Complete(); IsGeneratingTerrain = false; HasGeneratedTerrain = true; }

            int index = localPos.x + VoxelSettings.ChunkWidth * (localPos.y + VoxelSettings.ChunkHeight * localPos.z);
            if (VoxelMap[index] == blockData) return;

            VoxelMap[index] = blockData;
            IsModified = true;
            IsReady = false;

            StartLightJob(true);

            if (immediateUpdate)
            {
                _lightJobHandle.Complete();
                IsGeneratingLight = false;
                StartMeshJob();
                CompleteMeshAndApply();
            }
        }

        public void RebuildMeshImmediate()
        {
            if (IsGeneratingMesh) { _meshJobHandle.Complete(); IsGeneratingMesh = false; }
            if (IsGeneratingLight) { _lightJobHandle.Complete(); IsGeneratingLight = false; }

            StartLightJob(true);
            _lightJobHandle.Complete();
            IsGeneratingLight = false;

            StartMeshJob();
            CompleteMeshAndApply();
        }

        public void CancelGeneration()
        {
            IsCancelled = true;
            IsReady = false;
            gameObject.SetActive(false);
        }

        private void CompleteAllJobs()
        {
            if (IsGeneratingTerrain) { _terrainJobHandle.Complete(); IsGeneratingTerrain = false; }
            if (IsGeneratingLight) { _lightJobHandle.Complete(); IsGeneratingLight = false; }
            if (IsGeneratingMesh) { _meshJobHandle.Complete(); IsGeneratingMesh = false; }
        }

        public void CancelAndCompleteJobs()
        {
            IsCancelled = true;
            CompleteAllJobs();
        }

        private void OnDestroy()
        {
            CompleteAllJobs();
            if (VoxelMap.IsCreated) VoxelMap.Dispose();
            if (LightMap.IsCreated) LightMap.Dispose();

            if (_vertices.IsCreated) _vertices.Dispose();
            if (_triangles.IsCreated) _triangles.Dispose();
            if (_cutoutTriangles.IsCreated) _cutoutTriangles.Dispose();
            if (_transparentTriangles.IsCreated) _transparentTriangles.Dispose();

            if (_frontSlice.IsCreated) _frontSlice.Dispose();
            if (_backSlice.IsCreated) _backSlice.Dispose();
            if (_rightSlice.IsCreated) _rightSlice.Dispose();
            if (_leftSlice.IsCreated) _leftSlice.Dispose();

            if (_frontLight.IsCreated) _frontLight.Dispose();
            if (_backLight.IsCreated) _backLight.Dispose();
            if (_rightLight.IsCreated) _rightLight.Dispose();
            if (_leftLight.IsCreated) _leftLight.Dispose();
        }
    }
}