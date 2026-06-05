using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MinecraftEngine
{
    public class SlotView
    {
        public RectTransform Rect;
        public RawImage Icon;
        public TextMeshProUGUI AmountText;
        public TextMeshProUGUI AmountShadow;
        public Image Highlight;
        public int SlotIndex;

        public static SlotView Create(Transform parent, int slotIndex, Vector2 guiPos, float guiScale, TMP_FontAsset font)
        {
            var sv = new SlotView();
            sv.SlotIndex = slotIndex;

            GameObject obj = new GameObject($"Slot_{slotIndex}");
            obj.transform.SetParent(parent, false);

            sv.Rect = obj.AddComponent<RectTransform>();
            sv.Rect.anchorMin = new Vector2(0, 1);
            sv.Rect.anchorMax = new Vector2(0, 1);
            sv.Rect.pivot = new Vector2(0, 1);
            sv.Rect.sizeDelta = new Vector2(16 * guiScale, 16 * guiScale);
            sv.Rect.anchoredPosition = new Vector2(guiPos.x * guiScale, -guiPos.y * guiScale);

            GameObject hlObj = new GameObject("Highlight");
            hlObj.transform.SetParent(obj.transform, false);
            sv.Highlight = hlObj.AddComponent<Image>();
            sv.Highlight.color = new Color(1f, 1f, 1f, sv.IsArmorSlot ? 0.3f : 0f);
            sv.Highlight.raycastTarget = false;

            Sprite slotSprite = sv.GetSlotBackgroundSprite();
            if (slotSprite != null)
            {
                sv.Highlight.sprite = slotSprite;
                sv.Highlight.type = Image.Type.Sliced;
            }

            RectTransform hlRect = hlObj.GetComponent<RectTransform>();
            hlRect.anchorMin = Vector2.zero;
            hlRect.anchorMax = Vector2.one;
            hlRect.sizeDelta = Vector2.zero;

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(obj.transform, false);
            sv.Icon = iconObj.AddComponent<RawImage>();
            sv.Icon.color = Color.clear;
            sv.Icon.raycastTarget = false;
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

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

        public bool IsArmorSlot => SlotIndex >= 36 && SlotIndex <= 39;
        public bool IsOffhandSlot => SlotIndex == 40;
        public bool IsCraftingSlot => SlotIndex >= 41 && SlotIndex <= 44;
        public bool IsCraftingResult => SlotIndex == 45;

        private Sprite GetSlotBackgroundSprite()
        {
            if (IsArmorSlot)
            {
                int idx = SlotIndex - 36;
                string name = idx switch
                {
                    0 => "slot_helmet",
                    1 => "slot_chestplate",
                    2 => "slot_leggings",
                    3 => "slot_boots",
                    _ => "slot_armor"
                };
                Sprite s = Resources.Load<Sprite>($"GUI/{name}");
                if (s != null) return s;
            }
            else if (IsOffhandSlot)
            {
                Sprite s = Resources.Load<Sprite>("GUI/slot_offhand");
                if (s != null) return s;
            }
            return null;
        }


        public void SetHighlight(bool on)
        {
            float baseAlpha = (IsArmorSlot || IsOffhandSlot) ? 0.3f : 0f;
            Highlight.color = on ? new Color(1f, 1f, 1f, baseAlpha + 0.4f) : new Color(1f, 1f, 1f, baseAlpha);
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
                if (IsArmorSlot && ItemDatabase.Instance != null)
                {
                    ItemData data = ItemDatabase.Instance.GetItem(stack.ItemID);
                    if (data is ArmorData armor)
                    {
                        ushort displayID = GetArmorDisplayID(stack.ItemID, armor);
                        Texture2D armorIcon = ContainerScreen.GetItemIcon(displayID);
                        if (armorIcon != null) itemTexture = armorIcon;
                    }
                }

                Icon.texture = itemTexture;
                Icon.color = Color.white;

                string amt = stack.Amount > 1 ? stack.Amount.ToString() : "";
                AmountText.text = amt;
                AmountShadow.text = amt;
            }
        }

        private ushort GetArmorDisplayID(ushort itemID, ArmorData armor)
        {
            return armor.slot switch
            {
                ArmorSlot.Helmet => 500,
                ArmorSlot.Chestplate => 500,
                ArmorSlot.Leggings => 500,
                ArmorSlot.Boots => 500,
                _ => itemID
            };
        }
    }
}