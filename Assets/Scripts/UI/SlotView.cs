using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MinecraftEngine
{
    /// <summary>
    /// Lightweight visual representation of an inventory slot.
    /// Created programmatically by ContainerScreen. No Prefab needed.
    /// </summary>
    public class SlotView
    {
        public RectTransform Rect;
        public RawImage Icon;
        public TextMeshProUGUI AmountText;
        public TextMeshProUGUI AmountShadow;
        public Image Highlight;       // hover highlight
        public int SlotIndex;         // index into PlayerInventory.GetSlot()

        /// <summary>
        /// Creates a slot view as child of parent at given pixel position (MC GUI coordinates).
        /// </summary>
        public static SlotView Create(Transform parent, int slotIndex, Vector2 guiPos, float guiScale, TMP_FontAsset font)
        {
            var sv = new SlotView();
            sv.SlotIndex = slotIndex;

            // Root object
            GameObject obj = new GameObject($"Slot_{slotIndex}");
            obj.transform.SetParent(parent, false);

            sv.Rect = obj.AddComponent<RectTransform>();
            sv.Rect.anchorMin = new Vector2(0, 1); // top-left anchor
            sv.Rect.anchorMax = new Vector2(0, 1);
            sv.Rect.pivot = new Vector2(0, 1);
            sv.Rect.sizeDelta = new Vector2(16 * guiScale, 16 * guiScale);
            // Position: MC GUI coords start top-left. Slot is 16x16 with 1px border = 18px step.
            // guiPos is the top-left of the 16x16 icon area (inside the slot border).
            sv.Rect.anchoredPosition = new Vector2(guiPos.x * guiScale, -guiPos.y * guiScale);

            // Hover highlight (white semi-transparent overlay, hidden by default)
            GameObject hlObj = new GameObject("Highlight");
            hlObj.transform.SetParent(obj.transform, false);
            sv.Highlight = hlObj.AddComponent<Image>();
            sv.Highlight.color = new Color(1f, 1f, 1f, 0f); // invisible by default
            RectTransform hlRect = hlObj.GetComponent<RectTransform>();
            hlRect.anchorMin = Vector2.zero;
            hlRect.anchorMax = Vector2.one;
            hlRect.sizeDelta = Vector2.zero;
            sv.Highlight.raycastTarget = false;

            // Item icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(obj.transform, false);
            sv.Icon = iconObj.AddComponent<RawImage>();
            sv.Icon.color = Color.clear; // hidden until item set
            sv.Icon.raycastTarget = false;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

            // Amount shadow
            GameObject shadowObj = new GameObject("AmountShadow");
            shadowObj.transform.SetParent(obj.transform, false);
            sv.AmountShadow = shadowObj.AddComponent<TextMeshProUGUI>();
            sv.AmountShadow.font = font;
            sv.AmountShadow.fontSize = 8 * guiScale;
            sv.AmountShadow.alignment = TextAlignmentOptions.BottomRight;
            sv.AmountShadow.color = new Color32(63, 63, 63, 255);
            sv.AmountShadow.raycastTarget = false;
            sv.AmountShadow.text = "";
            RectTransform shRect = shadowObj.GetComponent<RectTransform>();
            shRect.anchorMin = Vector2.zero;
            shRect.anchorMax = Vector2.one;
            shRect.sizeDelta = Vector2.zero;
            shRect.anchoredPosition = new Vector2(1 * guiScale, -1 * guiScale);

            // Amount text
            GameObject amtObj = new GameObject("AmountText");
            amtObj.transform.SetParent(obj.transform, false);
            sv.AmountText = amtObj.AddComponent<TextMeshProUGUI>();
            sv.AmountText.font = font;
            sv.AmountText.fontSize = 8 * guiScale;
            sv.AmountText.alignment = TextAlignmentOptions.BottomRight;
            sv.AmountText.color = Color.white;
            sv.AmountText.raycastTarget = false;
            sv.AmountText.text = "";
            RectTransform amtRect = amtObj.GetComponent<RectTransform>();
            amtRect.anchorMin = Vector2.zero;
            amtRect.anchorMax = Vector2.one;
            amtRect.sizeDelta = Vector2.zero;

            return sv;
        }

        public void SetHighlight(bool on)
        {
            Highlight.color = on ? new Color(1f, 1f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0f);
        }

        public void UpdateVisual(ItemStack stack, Texture2D itemTexture)
        {
            if (stack.IsEmpty)
            {
                Icon.color = Color.clear;
                AmountText.text = "";
                AmountShadow.text = "";
            }
            else
            {
                Icon.texture = itemTexture;
                Icon.color = Color.white;
                string amt = stack.Amount > 1 ? stack.Amount.ToString() : "";
                AmountText.text = amt;
                AmountShadow.text = amt;
            }
        }
    }
}
