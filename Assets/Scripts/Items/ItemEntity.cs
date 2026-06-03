using Unity.Mathematics;
using UnityEngine;

namespace MinecraftEngine
{
    public class ItemEntity : MonoBehaviour
    {
        public ushort ItemID;
        public int Amount = 1;

        private Vector3 _velocity;
        private float _gravity = -20f;
        private bool _isGrounded;
        private float _spawnTime;
        private float _pickupDelay = 1.0f;
        private float _customPickupDelay = -1f;

        public Transform modelTransform;
        private float _bobOffset;

        public void Initialize(ushort id, Vector3 spawnPosition, Mesh itemMesh, Material itemMaterial, float pickupDelay = -1f, Vector3? overrideVelocity = null)
        {
            ItemID = id;
            Amount = 1;
            transform.position = spawnPosition;
            _spawnTime = Time.time;

            if (pickupDelay >= 0f)
                _customPickupDelay = pickupDelay;

            if (overrideVelocity.HasValue)
            {
                _velocity = overrideVelocity.Value;
            }
            else
            {
                // Default: slightly upward + random spread
                _velocity = new Vector3(
                    UnityEngine.Random.Range(-0.5f, 0.5f),
                    UnityEngine.Random.Range(2.5f, 4f),
                    UnityEngine.Random.Range(-0.5f, 0.5f)
                );
            }

            _isGrounded = false;

            MeshFilter mf = modelTransform.GetComponent<MeshFilter>();
            if (mf == null) mf = modelTransform.gameObject.AddComponent<MeshFilter>();
            mf.mesh = itemMesh;

            MeshRenderer mr = modelTransform.GetComponent<MeshRenderer>();
            if (mr == null) mr = modelTransform.gameObject.AddComponent<MeshRenderer>();
            mr.material = itemMaterial;

            modelTransform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            _bobOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        public void SetPickupDelay(float delay)
        {
            _customPickupDelay = delay;
        }

        private float GetPickupDelay()
        {
            return _customPickupDelay >= 0f ? _customPickupDelay : _pickupDelay;
        }

        private void Update()
        {
            if (!_isGrounded)
            {
                _velocity.y += _gravity * Time.deltaTime;
            }

            Vector3 nextPos = transform.position + _velocity * Time.deltaTime;

            WorldManager wm = ItemManager.Instance != null ? ItemManager.Instance.worldManager : null;
            if (wm != null)
            {
                if (wm.IsSolidBlockAt(nextPos + new Vector3(0, -0.125f, 0)))
                {
                    _isGrounded = true;
                    _velocity = Vector3.zero;
                    nextPos.y = Mathf.Floor(nextPos.y + 0.125f) + 0.125f;
                }
                else
                {
                    _isGrounded = false;
                    _velocity.x = Mathf.Lerp(_velocity.x, 0, Time.deltaTime * 2f);
                    _velocity.z = Mathf.Lerp(_velocity.z, 0, Time.deltaTime * 2f);
                }
            }

            transform.position = nextPos;

            float bobbing = Mathf.Sin(Time.time * 3f + _bobOffset) * 0.05f;
            modelTransform.localPosition = new Vector3(0, bobbing + 0.15f, 0);
            modelTransform.Rotate(0, 90f * Time.deltaTime, 0);

            float delay = GetPickupDelay();
            if (Time.time - _spawnTime > delay)
            {
                Transform player = ItemManager.Instance != null ? ItemManager.Instance.playerTransform : null;
                if (player != null)
                {
                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist < 1.5f)
                    {
                        if (ItemManager.Instance.playerInventory.AddItem(ItemID, Amount))
                        {
                            ItemManager.Instance.ReturnToPool(this);
                        }
                    }
                    else if (dist < 3.0f)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, player.position, Time.deltaTime * 5f);
                    }
                }
            }
        }
    }
}