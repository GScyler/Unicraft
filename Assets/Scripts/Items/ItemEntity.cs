using Unity.Mathematics;
using UnityEngine;

namespace MinecraftEngine
{
    public class ItemEntity : MonoBehaviour
    {
        public ushort ItemID;
        public int Amount = 1;

        [Header("Physics Settings")]
        private Vector3 _velocity;
        private float _gravity = -20f;
        private bool _isGrounded;
        private float _pickupDelay = 1.0f; // Нельзя подобрать сразу после разрушения
        private float _spawnTime;

        [Header("Animation")]
        public Transform modelTransform; // Внутренний объект с MeshRenderer
        private float _bobOffset;

        public void Initialize(ushort id, Vector3 spawnPosition, Mesh itemMesh, Material itemMaterial)
        {
            ItemID = id;
            Amount = 1; // По умолчанию падает 1 блок
            transform.position = spawnPosition;
            _spawnTime = Time.time;

            // Выбрасываем предмет в случайном направлении, немного вверх
            _velocity = new Vector3(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(3f, 5f),
                UnityEngine.Random.Range(-2f, 2f)
            );

            _isGrounded = false;

            // Настраиваем визуальную часть
            MeshFilter mf = modelTransform.GetComponent<MeshFilter>();
            if (mf == null) mf = modelTransform.gameObject.AddComponent<MeshFilter>();
            mf.mesh = itemMesh;

            MeshRenderer mr = modelTransform.GetComponent<MeshRenderer>();
            if (mr == null) mr = modelTransform.gameObject.AddComponent<MeshRenderer>();
            mr.material = itemMaterial;

            // В Minecraft лежащие предметы в 4 раза меньше обычных блоков (Scale 0.25)
            modelTransform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            _bobOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f); // Случайное начало анимации
        }

        private void Update()
        {
            // --- ФИЗИКА ---
            if (!_isGrounded)
            {
                _velocity.y += _gravity * Time.deltaTime;
            }

            Vector3 nextPos = transform.position + _velocity * Time.deltaTime;

            // Простая воксельная коллизия (проверяем только точку под центром предмета)
            WorldManager wm = ItemManager.Instance.worldManager;
            if (wm != null)
            {
                // Проверяем пол (сдвиг вниз на размер иконки)
                if (wm.IsSolidBlockAt(nextPos + new Vector3(0, -0.125f, 0)))
                {
                    _isGrounded = true;
                    _velocity = Vector3.zero;
                    // Выравниваем по поверхности блока
                    nextPos.y = Mathf.Floor(nextPos.y + 0.125f) + 0.125f;
                }
                else
                {
                    _isGrounded = false;
                    // Трение в воздухе (чтобы не улетел далеко)
                    _velocity.x = Mathf.Lerp(_velocity.x, 0, Time.deltaTime * 2f);
                    _velocity.z = Mathf.Lerp(_velocity.z, 0, Time.deltaTime * 2f);
                }
            }

            transform.position = nextPos;

            // --- АНИМАЦИЯ (Левитация и вращение) ---
            float bobbing = Mathf.Sin(Time.time * 3f + _bobOffset) * 0.05f;
            modelTransform.localPosition = new Vector3(0, bobbing + 0.15f, 0); // Парит чуть выше пола
            modelTransform.Rotate(0, 90f * Time.deltaTime, 0); // Медленно крутится по оси Y

            // --- ПОДБОР ---
            if (Time.time - _spawnTime > _pickupDelay)
            {
                Transform player = ItemManager.Instance.playerTransform;
                if (player != null)
                {
                    float dist = Vector3.Distance(transform.position, player.position);
                    if (dist < 1.5f) // Радиус подбора
                    {
                        // Пытаемся положить в инвентарь
                        if (ItemManager.Instance.playerInventory.AddItem(ItemID, Amount))
                        {
                            ItemManager.Instance.ReturnToPool(this);
                        }
                    }
                    else if (dist < 3.0f) // Радиус "засасывания" (летит к игроку)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, player.position, Time.deltaTime * 5f);
                    }
                }
            }
        }
    }
}