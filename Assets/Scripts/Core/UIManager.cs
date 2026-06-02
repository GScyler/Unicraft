using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MinecraftEngine
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UIVertex
    {
        public float3 position;
        public float3 uv;
    }

    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance;

        [Header("Шрифты TextMeshPro")]
        public TMP_FontAsset regularFont;
        public TMP_FontAsset boldFont;

        private GameObject _loadingScreen;
        private TextMeshProUGUI _percentageText;

        private GameObject _hotbarPanel;
        private RawImage[] _slotImages = new RawImage[9];
        private TextMeshProUGUI[] _amountTexts = new TextMeshProUGUI[9];
        // Добавляем массив для теней цифр
        private TextMeshProUGUI[] _amountShadows = new TextMeshProUGUI[9];
        private RawImage _selectionHighlight;

        private GameObject _crosshair;

        // --- 3D INVENTORY RENDERER ---
        private Camera _itemRenderCamera;
        private GameObject _itemRenderModel;
        private MeshFilter _itemMeshFilter;
        private MeshRenderer _itemMeshRenderer;
        private RenderTexture[] _slotTextures = new RenderTexture[9];

        private System.Collections.Generic.Dictionary<ushort, Mesh> _blockMeshCache = new System.Collections.Generic.Dictionary<ushort, Mesh>();
        private System.Collections.Generic.Dictionary<ushort, Texture2D> _shadedIconCache = new System.Collections.Generic.Dictionary<ushort, Texture2D>();

        private const int UIRenderLayer = 31;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (regularFont == null) regularFont = TMP_Settings.defaultFontAsset;
            if (boldFont == null) boldFont = TMP_Settings.defaultFontAsset;

            SetupItemRenderer();
            CreateLoadingScreen();
            CreateHotbarUI();
            CreateCrosshair();
        }

        private void SetupItemRenderer()
        {
            GameObject camObj = new GameObject("ItemRenderCamera");
            camObj.transform.position = new Vector3(0, -1000, 0);
            float isoAngle = Mathf.Asin(Mathf.Tan(Mathf.PI / 6)) * Mathf.Rad2Deg;
            camObj.transform.eulerAngles = new Vector3(isoAngle, 0, 0);
            camObj.transform.Translate(Vector3.back * 10f, Space.Self);

            _itemRenderCamera = camObj.AddComponent<Camera>();
            _itemRenderCamera.clearFlags = CameraClearFlags.SolidColor;
            _itemRenderCamera.backgroundColor = new Color(0, 0, 0, 0);
            _itemRenderCamera.orthographic = true;
            _itemRenderCamera.orthographicSize = 0.82f;
            _itemRenderCamera.nearClipPlane = 0.1f;
            _itemRenderCamera.farClipPlane = 20f;
            _itemRenderCamera.enabled = false;
            _itemRenderCamera.cullingMask = 1 << UIRenderLayer;

            _itemRenderModel = new GameObject("ItemModel");
            _itemRenderModel.layer = UIRenderLayer;
            _itemRenderModel.transform.position = new Vector3(0, -1000, 0);
            _itemRenderModel.transform.eulerAngles = new Vector3(0, 45, 0);

            _itemMeshFilter = _itemRenderModel.AddComponent<MeshFilter>();
            _itemMeshRenderer = _itemRenderModel.AddComponent<MeshRenderer>();

            Material atlasMat = Resources.Load<Material>("Materials/ChunkMaterial");
            if (atlasMat == null) atlasMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ChunkMaterial.mat");
            _itemMeshRenderer.material = atlasMat;

            for (int i = 0; i < 9; i++)
            {
                _slotTextures[i] = new RenderTexture(64, 64, 16, RenderTextureFormat.ARGB32);
                _slotTextures[i].filterMode = FilterMode.Point;
                _slotTextures[i].Create();
            }
        }

        private void CreateLoadingScreen()
        {
            GameObject canvasObj = new GameObject("UICanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            _loadingScreen = new GameObject("LoadingScreen");
            _loadingScreen.transform.SetParent(canvasObj.transform, false);
            Image bg = _loadingScreen.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            RectTransform bgRect = _loadingScreen.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(_loadingScreen.transform, false);
            TextMeshProUGUI loadingText = textObj.AddComponent<TextMeshProUGUI>();
            loadingText.text = "Генерация ландшафта...";
            loadingText.font = boldFont;
            loadingText.fontSize = 40;
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchoredPosition = new Vector2(0, 50);
            textRect.sizeDelta = new Vector2(800, 80);

            GameObject pctObj = new GameObject("PercentageText");
            pctObj.transform.SetParent(_loadingScreen.transform, false);
            _percentageText = pctObj.AddComponent<TextMeshProUGUI>();
            _percentageText.text = "0%";
            _percentageText.font = regularFont;
            _percentageText.fontSize = 32;
            _percentageText.alignment = TextAlignmentOptions.Center;
            _percentageText.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            RectTransform pctRect = pctObj.GetComponent<RectTransform>();
            pctRect.anchoredPosition = new Vector2(0, -20);
            pctRect.sizeDelta = new Vector2(200, 50);
        }

        private void CreateHotbarUI()
        {
            Canvas hudCanvas = GameObject.Find("UICanvas").GetComponent<Canvas>();

            Texture2D guiTex = Resources.Load<Texture2D>("GUI/widgets");
            if (guiTex == null) guiTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/GUI/widgets.png");

            _hotbarPanel = new GameObject("HotbarPanel");
            _hotbarPanel.transform.SetParent(hudCanvas.transform, false);

            RectTransform hotbarRect = _hotbarPanel.AddComponent<RectTransform>();
            hotbarRect.anchorMin = new Vector2(0.5f, 0f);
            hotbarRect.anchorMax = new Vector2(0.5f, 0f);
            hotbarRect.pivot = new Vector2(0.5f, 0f);

            float guiScale = 3f;
            hotbarRect.sizeDelta = new Vector2(182 * guiScale, 22 * guiScale);
            hotbarRect.anchoredPosition = new Vector2(0, 10);

            RawImage panelImg = _hotbarPanel.AddComponent<RawImage>();
            if (guiTex != null)
            {
                panelImg.texture = guiTex;
                panelImg.uvRect = new Rect(0f, 1f - (22f / 256f), 182f / 256f, 22f / 256f);
            }
            else
            {
                panelImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            }

            _selectionHighlight = new GameObject("Selection").AddComponent<RawImage>();
            _selectionHighlight.transform.SetParent(_hotbarPanel.transform, false);

            if (guiTex != null)
            {
                _selectionHighlight.texture = guiTex;
                _selectionHighlight.uvRect = new Rect(0f, 1f - (46f / 256f), 24f / 256f, 24f / 256f);
            }
            else
            {
                _selectionHighlight.color = new Color(1f, 1f, 1f, 0.5f);
            }

            RectTransform selRect = _selectionHighlight.GetComponent<RectTransform>();
            selRect.anchorMin = new Vector2(0.5f, 0.5f);
            selRect.anchorMax = new Vector2(0.5f, 0.5f);
            selRect.pivot = new Vector2(0.5f, 0.5f);
            selRect.sizeDelta = new Vector2(24 * guiScale, 24 * guiScale);

            float slotPixelSize = 16f;
            float startPixelX = -91f + 3f + 8f;

            // Идеальный каноничный размер шрифта
            float fontSize = 9f * guiScale;

            for (int i = 0; i < 9; i++)
            {
                GameObject slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(_hotbarPanel.transform, false);

                RectTransform slotRect = slot.AddComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 0.5f);
                slotRect.anchorMax = new Vector2(0.5f, 0.5f);
                slotRect.pivot = new Vector2(0.5f, 0.5f);

                float slotCenter = (startPixelX + (i * 20f)) * guiScale;
                slotRect.anchoredPosition = new Vector2(slotCenter, 0);
                slotRect.sizeDelta = new Vector2(slotPixelSize * guiScale, slotPixelSize * guiScale);

                GameObject itemIcon = new GameObject("Icon");
                itemIcon.transform.SetParent(slot.transform, false);
                _slotImages[i] = itemIcon.AddComponent<RawImage>();
                _slotImages[i].color = Color.clear;
                _slotImages[i].texture = _slotTextures[i];

                RectTransform iconRect = itemIcon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(16 * guiScale, 16 * guiScale);
                iconRect.anchoredPosition = Vector2.zero;

                // --- ИСПРАВЛЕНИЕ: ЖЕСТКАЯ ДУБЛИРУЮЩАЯ ТЕНЬ (AmountShadow) ---
                GameObject shadowObj = new GameObject("AmountShadow");
                shadowObj.transform.SetParent(_hotbarPanel.transform, false);
                _amountShadows[i] = shadowObj.AddComponent<TextMeshProUGUI>();
                _amountShadows[i].text = "";
                _amountShadows[i].font = regularFont;
                _amountShadows[i].fontSize = fontSize;
                _amountShadows[i].alignment = TextAlignmentOptions.BottomRight;
                _amountShadows[i].color = new Color32(63, 63, 63, 255); // Тот самый цвет тени MC

                RectTransform shadowRect = shadowObj.GetComponent<RectTransform>();
                shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
                shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
                shadowRect.pivot = new Vector2(1f, 0f);
                shadowRect.sizeDelta = new Vector2(30 * guiScale, 20 * guiScale);

                // Тень упирается ровно в черные границы ячейки (+10 и -10 от центра)
                shadowRect.anchoredPosition = new Vector2(slotCenter + (10f * guiScale), -10f * guiScale);

                // --- ИСПРАВЛЕНИЕ: ОСНОВНОЙ ТЕКСТ (AmountText) ---
                GameObject amountObj = new GameObject("AmountText");
                amountObj.transform.SetParent(_hotbarPanel.transform, false);
                _amountTexts[i] = amountObj.AddComponent<TextMeshProUGUI>();
                _amountTexts[i].text = "";
                _amountTexts[i].font = regularFont;
                _amountTexts[i].fontSize = fontSize;
                _amountTexts[i].alignment = TextAlignmentOptions.BottomRight;
                _amountTexts[i].color = Color.white;

                RectTransform amountRect = amountObj.GetComponent<RectTransform>();
                amountRect.anchorMin = new Vector2(0.5f, 0.5f);
                amountRect.anchorMax = new Vector2(0.5f, 0.5f);
                amountRect.pivot = new Vector2(1f, 0f);
                amountRect.sizeDelta = new Vector2(30 * guiScale, 20 * guiScale);

                // Основной текст смещен на 1 пиксель вверх и влево от тени (+9 и -9)
                amountRect.anchoredPosition = new Vector2(slotCenter + (9f * guiScale), -9f * guiScale);
            }
        }

        private void CreateCrosshair()
        {
            Canvas hudCanvas = GameObject.Find("UICanvas").GetComponent<Canvas>();

            _crosshair = new GameObject("Crosshair");
            _crosshair.transform.SetParent(hudCanvas.transform, false);

            RectTransform rect = _crosshair.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(20, 20);

            GameObject hLine = new GameObject("H_Line");
            hLine.transform.SetParent(_crosshair.transform, false);
            Image hImg = hLine.AddComponent<Image>();
            hImg.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            RectTransform hRect = hLine.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.5f, 0.5f);
            hRect.anchorMax = new Vector2(0.5f, 0.5f);
            hRect.sizeDelta = new Vector2(20, 2);

            GameObject vLine = new GameObject("V_Line");
            vLine.transform.SetParent(_crosshair.transform, false);
            Image vImg = vLine.AddComponent<Image>();
            vImg.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            RectTransform vRect = vLine.GetComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.5f, 0.5f);
            vRect.anchorMax = new Vector2(0.5f, 0.5f);
            vRect.sizeDelta = new Vector2(2, 20);

            Shadow hShadow = hLine.AddComponent<Shadow>();
            hShadow.effectColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            hShadow.effectDistance = new Vector2(2, -2);

            Shadow vShadow = vLine.AddComponent<Shadow>();
            vShadow.effectColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            vShadow.effectDistance = new Vector2(2, -2);
        }

        public void UpdateProgress(float percentage)
        {
            if (_percentageText != null)
            {
                int val = Mathf.Clamp(Mathf.RoundToInt(percentage * 100), 0, 100);
                _percentageText.text = $"{val}%";
            }
        }

        public void HideLoadingScreen()
        {
            if (_loadingScreen != null)
            {
                _loadingScreen.SetActive(false);
            }
        }

        public void UpdateHotbarUI(ItemStack[] hotbar, int selectedSlot)
        {
            if (_hotbarPanel == null) return;

            for (int i = 0; i < 9; i++)
            {
                if (hotbar[i].IsEmpty)
                {
                    _slotImages[i].color = Color.clear;
                    _amountTexts[i].text = "";
                    _amountShadows[i].text = "";
                }
                else
                {
                    RenderBlockToTexture(hotbar[i].ItemID, i);
                    _slotImages[i].color = Color.white;

                    string amt = hotbar[i].Amount > 1 ? hotbar[i].Amount.ToString() : "";
                    _amountTexts[i].text = amt;
                    _amountShadows[i].text = amt;
                }
            }

            float guiScale = 3f;
            float startPixelX = -91f + 3f + 8f;
            float slotCenter = (startPixelX + (selectedSlot * 20f)) * guiScale;
            _selectionHighlight.rectTransform.anchoredPosition = new Vector2(slotCenter, 0);

            // ИСПРАВЛЕНИЕ: Выстраиваем правильную иерархию отрисовки.
            // 1. Сначала рисуется рамка (на фоне)
            _selectionHighlight.transform.SetAsLastSibling();

            // 2. Затем рисуются тени и цифры (поверх рамки выделения!)
            for (int i = 0; i < 9; i++)
            {
                _amountShadows[i].transform.SetAsLastSibling();
                _amountTexts[i].transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Public API: renders a 3D block icon and returns the cached Texture2D.
        /// Used by ContainerScreen for inventory slot icons.
        /// </summary>
        public Texture2D RenderBlockIcon(ushort blockID)
        {
            if (_shadedIconCache.TryGetValue(blockID, out Texture2D cached))
                return cached;

            // Render it by calling internal method with dummy slot, then return cached
            RenderBlockToTexture(blockID, 0);

            _shadedIconCache.TryGetValue(blockID, out Texture2D result);
            return result;
        }

        private void RenderBlockToTexture(ushort blockID, int slotIndex)
        {
            if (_shadedIconCache.TryGetValue(blockID, out Texture2D cachedTex))
            {
                _slotImages[slotIndex].texture = cachedTex;
                return;
            }

            RenderTexture rt = RenderTexture.GetTemporary(64, 64, 16, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;

            _itemRenderCamera.targetTexture = rt;
            _itemMeshFilter.mesh = GetBlockMesh(blockID);
            _itemRenderCamera.Render();
            _itemRenderCamera.targetTexture = null;

            RenderTexture.active = rt;
            Texture2D tex2D = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex2D.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            tex2D.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            Color[] pixels = tex2D.GetPixels();

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    int idx = y * 64 + x;
                    Color col = pixels[idx];

                    if (col.a > 0.1f)
                    {
                        int cx = x - 32;
                        int cy = y - 32;

                        bool isTop = (cy > Mathf.Abs(cx) * 0.5f);
                        bool isLeft = (cx < 0 && cy <= -cx * 0.5f);
                        bool isRight = (cx >= 0 && cy <= cx * 0.5f);

                        if (isLeft)
                        {
                            col.r *= 0.6f; col.g *= 0.6f; col.b *= 0.6f;
                        }
                        else if (isRight)
                        {
                            col.r *= 0.4f; col.g *= 0.4f; col.b *= 0.4f;
                        }

                        pixels[idx] = col;
                    }
                }
            }

            tex2D.SetPixels(pixels);
            tex2D.filterMode = FilterMode.Point;
            tex2D.Apply();

            _shadedIconCache.Add(blockID, tex2D);
            _slotImages[slotIndex].texture = tex2D;
        }

        private Mesh GetBlockMesh(ushort blockID)
        {
            if (_blockMeshCache.TryGetValue(blockID, out Mesh cachedMesh)) return cachedMesh;

            Mesh mesh = new Mesh();

            var layout = new[]
            {
                new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.Position, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3),
                new UnityEngine.Rendering.VertexAttributeDescriptor(UnityEngine.Rendering.VertexAttribute.TexCoord0, UnityEngine.Rendering.VertexAttributeFormat.Float32, 3)
            };

            UIVertex[] voxelVerts = new UIVertex[24];
            int[] triangles = new int[36];

            BlockData bData = BlockDatabase.Instance.GetBlock(blockID);

            int vIndex = 0;
            int tIndex = 0;

            for (int p = 0; p < 6; p++)
            {
                float texIdx = bData != null ? bData.GetTextureIndex(p) : 0;

                Vector3 v0 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 0]] - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 v1 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 1]] - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 v2 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 2]] - new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 v3 = (Vector3)VoxelData.VoxelVertices[VoxelData.VoxelTriangles[p * 4 + 3]] - new Vector3(0.5f, 0.5f, 0.5f);

                voxelVerts[vIndex + 0] = new UIVertex { position = v0, uv = new float3(0, 0, texIdx) };
                voxelVerts[vIndex + 1] = new UIVertex { position = v1, uv = new float3(0, 1, texIdx) };
                voxelVerts[vIndex + 2] = new UIVertex { position = v2, uv = new float3(1, 0, texIdx) };
                voxelVerts[vIndex + 3] = new UIVertex { position = v3, uv = new float3(1, 1, texIdx) };

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
            mesh.SetIndexBufferParams(36, UnityEngine.Rendering.IndexFormat.UInt32);
            mesh.SetIndexBufferData(triangles, 0, 0, 36);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, 36, MeshTopology.Triangles));

            mesh.RecalculateNormals();

            _blockMeshCache.Add(blockID, mesh);
            return mesh;
        }

        private void OnDestroy()
        {
            if (_itemRenderCamera != null) Destroy(_itemRenderCamera.gameObject);
            foreach (var tex in _shadedIconCache.Values) if (tex != null) Destroy(tex);
        }
    }
}