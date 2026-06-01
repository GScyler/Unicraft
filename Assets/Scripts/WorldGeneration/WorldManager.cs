using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System.Diagnostics;

namespace MinecraftEngine
{
    public class WorldManager : MonoBehaviour
    {
        [Header("References")]
        public Material chunkMaterial;
        public Camera viewerCamera;
        public BlockDatabase blockDatabase;
        public BiomeDatabase biomeDatabase;

        [Header("World Settings")]
        public int worldSeed = 12345;
        [Range(2, 32)] public int viewDistance = 8;

        [Header("Lighting Settings")]
        [Range(0f, 1f)] public float minLight = 0.05f;
        [Range(0f, 1f)] public float maxLight = 1.0f;
        [Range(0.1f, 3f)] public float lightGamma = 1.5f;

        private readonly int _spawnLoadDistance = 4;
        private const float MaxTimePerFrameMs = 4.0f;

        private int2 _currentViewerChunkCoord;

        private Dictionary<int2, ChunkRenderer> _activeChunks = new Dictionary<int2, ChunkRenderer>();
        private Queue<ChunkRenderer> _chunkPool = new Queue<ChunkRenderer>();

        private List<ChunkRenderer> _terrainGenerationQueue = new List<ChunkRenderer>();
        private List<ChunkRenderer> _lightGenerationQueue = new List<ChunkRenderer>();
        private List<ChunkRenderer> _meshGenerationQueue = new List<ChunkRenderer>();
        private List<ChunkRenderer> _cancelledChunksQueue = new List<ChunkRenderer>();

        private Plane[] _cameraFrustum = new Plane[6];
        private Stopwatch _frameTimer = new Stopwatch();

        public bool IsGameStarted { get; private set; } = false;
        private int _totalChunksToLoadAtSpawn;
        private int _chunksLoadedAtSpawn;

        private bool _isDatabasesLoaded = false;
        private bool _isShuttingDown = false;

        private void Start()
        {
            Application.runInBackground = true;
            if (viewerCamera == null) viewerCamera = Camera.main;

            int side = (_spawnLoadDistance * 2) + 1;
            _totalChunksToLoadAtSpawn = side * side;
            _chunksLoadedAtSpawn = 0;
        }

        private void Update()
        {
            if (_isShuttingDown) return;

            if (!_isDatabasesLoaded)
            {
                if (BlockDatabase.Instance != null && BiomeDatabase.Instance != null &&
                    BlockDatabase.Instance.NativeBlockData.IsCreated && BiomeDatabase.Instance.NativeBiomeData.IsCreated)
                {
                    _isDatabasesLoaded = true;
                    ForceUpdateWorld(true);
                }
                else
                {
                    if (blockDatabase == null) blockDatabase = FindAnyObjectByType<BlockDatabase>();
                    if (biomeDatabase == null) biomeDatabase = FindAnyObjectByType<BiomeDatabase>();

                    if (blockDatabase != null && BlockDatabase.Instance == null) blockDatabase.Initialize();
                    if (biomeDatabase != null && BiomeDatabase.Instance == null) biomeDatabase.Initialize();

                    return;
                }
            }

            if (viewerCamera == null) return;

            ProcessQueues();

            if (!IsGameStarted)
            {
                float progress = (float)_chunksLoadedAtSpawn / _totalChunksToLoadAtSpawn;
                if (UIManager.Instance != null) UIManager.Instance.UpdateProgress(progress);

                if (_chunksLoadedAtSpawn >= _totalChunksToLoadAtSpawn)
                {
                    StartGame();
                }
                return;
            }

            int2 viewerCoord = new int2(
                Mathf.FloorToInt(viewerCamera.transform.position.x / VoxelSettings.ChunkWidth),
                Mathf.FloorToInt(viewerCamera.transform.position.z / VoxelSettings.ChunkDepth)
            );

            if (!viewerCoord.Equals(_currentViewerChunkCoord))
            {
                _currentViewerChunkCoord = viewerCoord;
                ForceUpdateWorld(false);
            }

            PerformFrustumCulling();
        }

        private void StartGame()
        {
            IsGameStarted = true;
            if (UIManager.Instance != null) UIManager.Instance.HideLoadingScreen();

            float spawnY = GetHighestBlock(viewerCamera.transform.position.x, viewerCamera.transform.position.z) + 2f;
            viewerCamera.transform.parent.position = new Vector3(viewerCamera.transform.position.x, spawnY, viewerCamera.transform.position.z);

            ForceUpdateWorld(false);
        }

        public bool TryGetChunkMap(int2 coord, out NativeArray<ushort> voxelMap, out NativeArray<byte> lightMap)
        {
            if (_activeChunks.TryGetValue(coord, out ChunkRenderer chunk) && chunk.HasGeneratedTerrain)
            {
                // ИСПРАВЛЕНИЕ: Вызываем актуальный метод
                chunk.CancelAndCompleteJobs();

                voxelMap = chunk.VoxelMap;
                lightMap = chunk.LightMap;
                return true;
            }
            voxelMap = default;
            lightMap = default;
            return false;
        }

        private bool GetBlockContext(int3 worldPos, out ChunkRenderer chunk, out int3 localPos)
        {
            localPos = int3.zero;
            chunk = null;

            int2 chunkCoord = new int2(
                Mathf.FloorToInt((float)worldPos.x / VoxelSettings.ChunkWidth),
                Mathf.FloorToInt((float)worldPos.z / VoxelSettings.ChunkDepth)
            );

            if (!_activeChunks.TryGetValue(chunkCoord, out chunk)) return false;

            if (!chunk.HasGeneratedTerrain || chunk.IsGeneratingTerrain || chunk.IsGeneratingLight || chunk.IsGeneratingMesh)
                return false;

            localPos.x = worldPos.x - (chunkCoord.x * VoxelSettings.ChunkWidth);
            localPos.y = worldPos.y + VoxelSettings.WorldYOffset;
            localPos.z = worldPos.z - (chunkCoord.y * VoxelSettings.ChunkDepth);

            return localPos.x >= 0 && localPos.x < VoxelSettings.ChunkWidth &&
                   localPos.y >= 0 && localPos.y < VoxelSettings.ChunkHeight &&
                   localPos.z >= 0 && localPos.z < VoxelSettings.ChunkDepth;
        }

        public ushort GetBlock(Vector3 worldPos)
        {
            int3 pos = new int3(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y), Mathf.FloorToInt(worldPos.z));
            if (GetBlockContext(pos, out ChunkRenderer chunk, out int3 localPos))
            {
                int index = localPos.x + VoxelSettings.ChunkWidth * (localPos.y + VoxelSettings.ChunkHeight * localPos.z);
                return chunk.VoxelMap[index];
            }
            return 0;
        }

        public bool IsSolidBlockAt(Vector3 worldPos)
        {
            ushort data = GetBlock(worldPos);
            byte blockID = (byte)(data & 0x0FFF);
            return blockID != 0 && BlockDatabase.Instance.IsSolid(blockID);
        }

        public void SetBlock(int3 worldPos, ushort blockData)
        {
            if (GetBlockContext(worldPos, out ChunkRenderer chunk, out int3 localPos))
            {
                int index = localPos.x + VoxelSettings.ChunkWidth * (localPos.y + VoxelSettings.ChunkHeight * localPos.z);
                byte currentID = (byte)(chunk.VoxelMap[index] & 0x0FFF);
                byte newID = (byte)(blockData & 0x0FFF);

                if (currentID == (byte)BlockType.Bedrock && newID == (byte)BlockType.Air) return;

                chunk.ModifyBlock(localPos, blockData, true);

                if (_meshGenerationQueue.Contains(chunk)) _meshGenerationQueue.Remove(chunk);
                if (_lightGenerationQueue.Contains(chunk)) _lightGenerationQueue.Remove(chunk);

                if (localPos.x == 0) UpdateNeighbourChunk(new int2(chunk.Coord.x - 1, chunk.Coord.y));
                if (localPos.x == VoxelSettings.ChunkWidth - 1) UpdateNeighbourChunk(new int2(chunk.Coord.x + 1, chunk.Coord.y));
                if (localPos.z == 0) UpdateNeighbourChunk(new int2(chunk.Coord.x, chunk.Coord.y - 1));
                if (localPos.z == VoxelSettings.ChunkDepth - 1) UpdateNeighbourChunk(new int2(chunk.Coord.x, chunk.Coord.y + 1));
            }
        }

        private void UpdateNeighbourChunk(int2 coord)
        {
            if (_activeChunks.TryGetValue(coord, out ChunkRenderer neighbourChunk) && neighbourChunk.HasGeneratedTerrain)
            {
                neighbourChunk.RebuildMeshImmediate();

                if (_meshGenerationQueue.Contains(neighbourChunk)) _meshGenerationQueue.Remove(neighbourChunk);
                if (_lightGenerationQueue.Contains(neighbourChunk)) _lightGenerationQueue.Remove(neighbourChunk);
            }
        }

        public float GetHighestBlock(float x, float z)
        {
            for (float y = 300; y > -60; y--)
            {
                if (IsSolidBlockAt(new Vector3(x, y, z))) return y;
            }
            return 100f;
        }

        private void ForceUpdateWorld(bool isSpawnLoading)
        {
            int loadDistance = isSpawnLoading ? _spawnLoadDistance : viewDistance;
            int terrainLoadDistance = loadDistance + 1;

            List<int2> chunksToRemove = new List<int2>();
            foreach (var kvp in _activeChunks)
            {
                if (math.abs(kvp.Key.x - _currentViewerChunkCoord.x) > terrainLoadDistance ||
                    math.abs(kvp.Key.y - _currentViewerChunkCoord.y) > terrainLoadDistance)
                {
                    chunksToRemove.Add(kvp.Key);
                }
            }

            foreach (int2 coord in chunksToRemove)
            {
                ChunkRenderer chunk = _activeChunks[coord];
                chunk.CancelGeneration();
                _activeChunks.Remove(coord);

                if (chunk.IsGeneratingTerrain || chunk.IsGeneratingLight || chunk.IsGeneratingMesh)
                {
                    _cancelledChunksQueue.Add(chunk);
                }
                else
                {
                    _terrainGenerationQueue.Remove(chunk);
                    _lightGenerationQueue.Remove(chunk);
                    _meshGenerationQueue.Remove(chunk);
                    _chunkPool.Enqueue(chunk);
                }
            }

            List<int2> coordsToLoad = new List<int2>();
            for (int x = -terrainLoadDistance; x <= terrainLoadDistance; x++)
            {
                for (int z = -terrainLoadDistance; z <= terrainLoadDistance; z++)
                {
                    int2 coord = new int2(_currentViewerChunkCoord.x + x, _currentViewerChunkCoord.y + z);
                    if (!_activeChunks.ContainsKey(coord))
                    {
                        coordsToLoad.Add(coord);
                    }
                }
            }

            coordsToLoad.Sort((a, b) =>
            {
                float distA = math.lengthsq(new float2(a.x - _currentViewerChunkCoord.x, a.y - _currentViewerChunkCoord.y));
                float distB = math.lengthsq(new float2(b.x - _currentViewerChunkCoord.x, b.y - _currentViewerChunkCoord.y));
                return distA.CompareTo(distB);
            });

            foreach (int2 coord in coordsToLoad)
            {
                ChunkRenderer chunk = GetChunkFromPool();
                chunk.transform.position = new Vector3(coord.x * VoxelSettings.ChunkWidth, -VoxelSettings.WorldYOffset, coord.y * VoxelSettings.ChunkDepth);
                chunk.name = $"Chunk_{coord.x}_{coord.y}";

                chunk.Init(coord, this, BlockDatabase.Instance.NativeBlockData, BiomeDatabase.Instance.NativeBiomeData, worldSeed);

                _activeChunks.Add(coord, chunk);
                _terrainGenerationQueue.Add(chunk);
            }
        }

        private bool HasAllTerrainNeighbors(int2 coord)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    int2 neighborCoord = new int2(coord.x + x, coord.y + y);
                    if (!_activeChunks.TryGetValue(neighborCoord, out ChunkRenderer neighbor)) return false;
                    if (!neighbor.HasGeneratedTerrain) return false;
                }
            }
            return true;
        }

        private bool HasAllLightNeighbors(int2 coord)
        {
            bool hasRight = _activeChunks.TryGetValue(new int2(coord.x + 1, coord.y), out ChunkRenderer r);
            bool hasLeft = _activeChunks.TryGetValue(new int2(coord.x - 1, coord.y), out ChunkRenderer l);
            bool hasFront = _activeChunks.TryGetValue(new int2(coord.x, coord.y + 1), out ChunkRenderer f);
            bool hasBack = _activeChunks.TryGetValue(new int2(coord.x, coord.y - 1), out ChunkRenderer b);

            bool rightSafe = !hasRight || (r.HasGeneratedLight);
            bool leftSafe = !hasLeft || (l.HasGeneratedLight);
            bool frontSafe = !hasFront || (f.HasGeneratedLight);
            bool backSafe = !hasBack || (b.HasGeneratedLight);

            return rightSafe && leftSafe && frontSafe && backSafe;
        }

        private void ProcessQueues()
        {
            _frameTimer.Restart();

            for (int i = _cancelledChunksQueue.Count - 1; i >= 0; i--)
            {
                ChunkRenderer chunk = _cancelledChunksQueue[i];
                if ((!chunk.IsGeneratingTerrain || chunk.IsTerrainJobCompleted()) &&
                    (!chunk.IsGeneratingLight || chunk.IsLightJobCompleted()) &&
                    (!chunk.IsGeneratingMesh || chunk.IsMeshJobCompleted()))
                {
                    _terrainGenerationQueue.Remove(chunk);
                    _lightGenerationQueue.Remove(chunk);
                    _meshGenerationQueue.Remove(chunk);
                    _chunkPool.Enqueue(chunk);
                    _cancelledChunksQueue.RemoveAt(i);
                }
            }

            for (int i = 0; i < _terrainGenerationQueue.Count; i++)
            {
                ChunkRenderer chunk = _terrainGenerationQueue[i];
                if (!chunk.IsGeneratingTerrain && !chunk.HasGeneratedTerrain && !chunk.IsCancelled)
                {
                    chunk.StartTerrainJob();
                }
            }

            for (int i = _terrainGenerationQueue.Count - 1; i >= 0; i--)
            {
                ChunkRenderer chunk = _terrainGenerationQueue[i];

                if (chunk.IsGeneratingTerrain && chunk.IsTerrainJobCompleted())
                {
                    chunk.CompleteTerrainJob();
                }

                if (chunk.HasGeneratedTerrain && !chunk.IsCancelled)
                {
                    int loadDistance = !IsGameStarted ? _spawnLoadDistance : viewDistance;
                    bool isBorderChunk = math.abs(chunk.Coord.x - _currentViewerChunkCoord.x) > loadDistance ||
                                         math.abs(chunk.Coord.y - _currentViewerChunkCoord.y) > loadDistance;

                    if (isBorderChunk) continue;

                    if (HasAllTerrainNeighbors(chunk.Coord))
                    {
                        _terrainGenerationQueue.RemoveAt(i);
                        chunk.StartLightJob();
                        _lightGenerationQueue.Add(chunk);
                    }
                }
            }

            for (int i = _lightGenerationQueue.Count - 1; i >= 0; i--)
            {
                ChunkRenderer chunk = _lightGenerationQueue[i];
                if (chunk.IsGeneratingLight && chunk.IsLightJobCompleted())
                {
                    chunk.CompleteLightJob();
                }

                if (chunk.HasGeneratedLight && !chunk.IsCancelled)
                {
                    int loadDistance = !IsGameStarted ? _spawnLoadDistance : viewDistance;
                    bool isMeshBorderChunk = math.abs(chunk.Coord.x - _currentViewerChunkCoord.x) > loadDistance ||
                                             math.abs(chunk.Coord.y - _currentViewerChunkCoord.y) > loadDistance;

                    if (isMeshBorderChunk) continue;

                    if (HasAllLightNeighbors(chunk.Coord))
                    {
                        _lightGenerationQueue.RemoveAt(i);
                        chunk.StartMeshJob();
                        _meshGenerationQueue.Add(chunk);
                    }
                }
            }

            for (int i = _meshGenerationQueue.Count - 1; i >= 0; i--)
            {
                if (IsTimeBudgetExceeded()) break;

                ChunkRenderer chunk = _meshGenerationQueue[i];
                if (chunk.IsGeneratingMesh && chunk.IsMeshJobCompleted())
                {
                    chunk.CompleteMeshAndApply();
                    if (chunk.IsReady || chunk.IsCancelled)
                    {
                        _meshGenerationQueue.RemoveAt(i);

                        if (!IsGameStarted && chunk.IsReady)
                        {
                            _chunksLoadedAtSpawn++;
                        }
                    }
                }
            }

            _frameTimer.Stop();
        }

        private bool IsTimeBudgetExceeded()
        {
            if (!IsGameStarted) return false;
            return _frameTimer.ElapsedMilliseconds >= MaxTimePerFrameMs;
        }

        private void PerformFrustumCulling()
        {
            GeometryUtility.CalculateFrustumPlanes(viewerCamera, _cameraFrustum);

            foreach (var kvp in _activeChunks)
            {
                ChunkRenderer chunk = kvp.Value;
                if (!chunk.IsReady || chunk.IsCancelled) continue;

                MeshRenderer renderer = chunk.GetComponent<MeshRenderer>();
                bool isVisible = GeometryUtility.TestPlanesAABB(_cameraFrustum, renderer.bounds);

                if (renderer.enabled != isVisible)
                {
                    renderer.enabled = isVisible;
                }
            }
        }

        private ChunkRenderer GetChunkFromPool()
        {
            if (_chunkPool.Count > 0) return _chunkPool.Dequeue();

            GameObject chunkObject = new GameObject("Chunk");
            chunkObject.transform.SetParent(this.transform);
            ChunkRenderer renderer = chunkObject.AddComponent<ChunkRenderer>();
            chunkObject.GetComponent<MeshRenderer>().material = chunkMaterial;
            return renderer;
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
            CleanupWorld();
        }

        private void OnDestroy()
        {
            if (!_isShuttingDown) CleanupWorld();
        }

        private void CleanupWorld()
        {
            foreach (var chunk in _activeChunks.Values) chunk.CancelAndCompleteJobs();
            foreach (var chunk in _chunkPool) chunk.CancelAndCompleteJobs();
            foreach (var chunk in _cancelledChunksQueue) chunk.CancelAndCompleteJobs();

            if (BlockDatabase.Instance != null) BlockDatabase.Instance.Cleanup();
            if (BiomeDatabase.Instance != null) BiomeDatabase.Instance.Cleanup();
        }
    }
}