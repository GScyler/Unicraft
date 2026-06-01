using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MinecraftEngine
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("References")]
        public WorldManager worldManager;
        public Camera playerCamera;
        public PlayerInventory playerInventory;

        [Header("Interaction Settings")]
        public float reachDistance = 5f;

        [Header("Creative Timers")]
        public float breakCooldown = 0.25f;
        public float placeCooldown = 0.2f;

        [Header("Survival Settings")]
        public bool isSurvivalMode = true;

        private float _lastBreakTime = 0f;
        private float _lastPlaceTime = 0f;
        private int3 _currentBreakingBlock = new int3(-999, -999, -999);
        private float _breakProgress = 0f;
        private float _totalBreakTime = 0f;

        private GameObject _selectionOutline;
        private GameObject _cracksObject;
        private Material _cracksMaterial;
        private PlayerController _playerController;

        private void Start()
        {
            _playerController = GetComponent<PlayerController>();
            if (playerInventory == null) playerInventory = GetComponent<PlayerInventory>();

            CreateOutlineMesh();
            CreateCracksMesh();
        }

        private void CreateOutlineMesh()
        {
            _selectionOutline = new GameObject("SelectionOutline");
            MeshFilter filter = _selectionOutline.AddComponent<MeshFilter>();
            MeshRenderer renderer = _selectionOutline.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("MinecraftEngine/ThickLines");
            if (shader != null)
            {
                Material lineMat = new Material(shader);
                lineMat.SetColor("_Color", new Color(0f, 0f, 0f, 0.4f));
                lineMat.SetFloat("_Thickness", 2.0f);
                renderer.material = lineMat;
            }

            Mesh wireMesh = new Mesh();
            float e = 0.005f;
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-e, -e, -e), new Vector3(1+e, -e, -e),
                new Vector3(1+e, 1+e, -e), new Vector3(-e, 1+e, -e),
                new Vector3(-e, -e, 1+e), new Vector3(1+e, -e, 1+e),
                new Vector3(1+e, 1+e, 1+e), new Vector3(-e, 1+e, 1+e)
            };

            int[] indices = new int[]
            {
                0,1, 1,2, 2,3, 3,0,
                4,5, 5,6, 6,7, 7,4,
                0,4, 1,5, 2,6, 3,7
            };

            wireMesh.vertices = vertices;
            wireMesh.SetIndices(indices, MeshTopology.Lines, 0);
            filter.mesh = wireMesh;
            _selectionOutline.SetActive(false);
        }

        private void CreateCracksMesh()
        {
            _cracksObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cracksObject.name = "BlockCracks";
            Destroy(_cracksObject.GetComponent<BoxCollider>());

            _cracksMaterial = Resources.Load<Material>("Materials/BlockCracksMat");
            if (_cracksMaterial == null)
            {
                _cracksMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlockCracksMat.mat");
            }

            if (_cracksMaterial != null)
            {
                _cracksObject.GetComponent<MeshRenderer>().material = _cracksMaterial;
            }

            _cracksObject.SetActive(false);
        }

        private void Update()
        {
            if (worldManager == null || !worldManager.IsGameStarted) return;
            HandleRaycastAndBreaking();
        }

        private void HandleRaycastAndBreaking()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool isLookingAtBlock = VoxelRaycast(playerCamera.transform.position, playerCamera.transform.forward, reachDistance, out int3 hitBlock, out int3 hitNormal);

            if (isLookingAtBlock)
            {
                _selectionOutline.SetActive(true);
                _selectionOutline.transform.position = new Vector3(hitBlock.x, hitBlock.y, hitBlock.z);
            }
            else
            {
                _selectionOutline.SetActive(false);
            }

            if (mouse.leftButton.isPressed)
            {
                if (isLookingAtBlock)
                {
                    ushort blockData = worldManager.GetBlock(new Vector3(hitBlock.x, hitBlock.y, hitBlock.z));
                    byte blockID = (byte)(blockData & 0x0FFF);

                    if (isSurvivalMode)
                    {
                        if (!hitBlock.Equals(_currentBreakingBlock))
                        {
                            _currentBreakingBlock = hitBlock;
                            _breakProgress = 0f;

                            float hardness = BlockDatabase.Instance.GetBlock(blockID).hardness;
                            _totalBreakTime = (hardness < 0) ? float.PositiveInfinity : hardness * 1.5f;
                        }

                        if (_totalBreakTime < float.PositiveInfinity)
                        {
                            _breakProgress += Time.deltaTime;
                            int crackStage = Mathf.FloorToInt((_breakProgress / _totalBreakTime) * 10f);

                            if (crackStage < 10 && _cracksObject != null)
                            {
                                _cracksObject.SetActive(true);
                                _cracksObject.transform.position = new Vector3(hitBlock.x + 0.5f, hitBlock.y + 0.5f, hitBlock.z + 0.5f);
                                _cracksMaterial.SetFloat("_Stage", crackStage);
                            }

                            if (_breakProgress >= _totalBreakTime)
                            {
                                // ИСПРАВЛЕНИЕ: Вызываем ItemManager для спавна дропа!
                                if (ItemManager.Instance != null)
                                {
                                    // Точка спавна - центр сломанного блока
                                    Vector3 spawnPos = new Vector3(hitBlock.x + 0.5f, hitBlock.y + 0.5f, hitBlock.z + 0.5f);

                                    // Узнаем, что должно выпасть из этого блока (DropItemBlockID)
                                    byte dropID = BlockDatabase.Instance.GetBlock(blockID).dropItemBlockID;

                                    // Если дроп не настроен в SO, выпадает сам блок (например, Земля)
                                    // Если выпадает 0 (Воздух) - ничего не дропаем
                                    if (dropID == 0 && blockID != 0) dropID = blockID;

                                    ItemManager.Instance.SpawnItem(dropID, spawnPos);
                                }

                                worldManager.SetBlock(hitBlock, 0);
                                ResetBreaking();
                            }
                        }
                    }
                    else
                    {
                        if (Time.time - _lastBreakTime >= breakCooldown)
                        {
                            worldManager.SetBlock(hitBlock, 0);
                            _lastBreakTime = Time.time;
                        }
                    }
                }
                else
                {
                    ResetBreaking();
                }
            }
            else
            {
                ResetBreaking();
            }

            if (mouse.rightButton.wasPressedThisFrame || (mouse.rightButton.isPressed && Time.time - _lastPlaceTime >= placeCooldown))
            {
                if (isLookingAtBlock)
                {
                    int3 placePosition = hitBlock + hitNormal;
                    if (_playerController != null && _playerController.IsBlockIntersectingPlayer(placePosition)) return;

                    byte selectedBlock = playerInventory != null ? playerInventory.GetSelectedBlockID() : (byte)0;

                    if (selectedBlock != 0)
                    {
                        int state = 0;

                        if (selectedBlock == 8) // OakLog
                        {
                            if (math.abs(hitNormal.x) > 0) state = 1;
                            else if (math.abs(hitNormal.z) > 0) state = 2;
                            else state = 0;
                        }

                        ushort blockDataToPlace = (ushort)(selectedBlock | (state << 12));

                        worldManager.SetBlock(placePosition, blockDataToPlace);

                        if (isSurvivalMode && playerInventory != null)
                        {
                            playerInventory.RemoveItemFromSelectedSlot();
                        }

                        _lastPlaceTime = Time.time;
                    }
                }
            }
        }

        private void ResetBreaking()
        {
            _currentBreakingBlock = new int3(-999, -999, -999);
            _breakProgress = 0f;
            if (_cracksObject != null) _cracksObject.SetActive(false);
        }

        private bool VoxelRaycast(Vector3 start, Vector3 dir, float maxDist, out int3 hitBlock, out int3 hitNormal)
        {
            hitBlock = int3.zero;
            hitNormal = int3.zero;

            int x = Mathf.FloorToInt(start.x);
            int y = Mathf.FloorToInt(start.y);
            int z = Mathf.FloorToInt(start.z);

            int stepX = (int)math.sign(dir.x);
            int stepY = (int)math.sign(dir.y);
            int stepZ = (int)math.sign(dir.z);

            float tMaxX = (dir.x == 0) ? float.PositiveInfinity : IntBound(start.x, dir.x);
            float tMaxY = (dir.y == 0) ? float.PositiveInfinity : IntBound(start.y, dir.y);
            float tMaxZ = (dir.z == 0) ? float.PositiveInfinity : IntBound(start.z, dir.z);

            float tDeltaX = (dir.x == 0) ? float.PositiveInfinity : stepX / dir.x;
            float tDeltaY = (dir.y == 0) ? float.PositiveInfinity : stepY / dir.y;
            float tDeltaZ = (dir.z == 0) ? float.PositiveInfinity : stepZ / dir.z;

            float dist = 0;

            while (dist < maxDist)
            {
                if (worldManager.IsSolidBlockAt(new Vector3(x, y, z)))
                {
                    hitBlock = new int3(x, y, z);
                    return true;
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        x += stepX;
                        dist = tMaxX;
                        tMaxX += tDeltaX;
                        hitNormal = new int3(-stepX, 0, 0);
                    }
                    else
                    {
                        z += stepZ;
                        dist = tMaxZ;
                        tMaxZ += tDeltaZ;
                        hitNormal = new int3(0, 0, -stepZ);
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        y += stepY;
                        dist = tMaxY;
                        tMaxY += tDeltaY;
                        hitNormal = new int3(0, -stepY, 0);
                    }
                    else
                    {
                        z += stepZ;
                        dist = tMaxZ;
                        tMaxZ += tDeltaZ;
                        hitNormal = new int3(0, 0, -stepZ);
                    }
                }
            }
            return false;
        }

        private float IntBound(float s, float ds)
        {
            if (ds < 0) return IntBound(-s, -ds);
            s = s - Mathf.Floor(s);
            return (1 - s) / ds;
        }
    }
}