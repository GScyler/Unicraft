using System;
using UnityEngine;
using MinecraftEngine;

namespace MinecraftEngine.UI.Interaction
{
    public class QuickActionsHandler : ITweakHandler
    {
        public int Priority => 10;
        public bool IsActive => false;

        private readonly CursorContext _cursor;
        private readonly IInventory _inventory;
        private readonly Action _onRefresh;

        private float _lastClickTime = 0f;
        private int _lastClickSlot = -1;
        private const float DOUBLE_CLICK_WINDOW = 0.4f;

        public QuickActionsHandler(
            CursorContext cursor,
            IInventory inventory,
            Action onRefresh)
        {
            _cursor = cursor;
            _inventory = inventory;
            _onRefresh = onRefresh;
        }

        public void OnPress(int slotIdx, bool shift, bool ctrl)
        {
            if (slotIdx < 0) return;

            float now = Time.time;

            // Double-click: collect all matching
            if (slotIdx == _lastClickSlot && (now - _lastClickTime) < DOUBLE_CLICK_WINDOW && !_cursor.IsEmpty)
            {
                DoDoubleClick(slotIdx);
                _lastClickSlot = -1;
                return;
            }

            _lastClickTime = now;
            _lastClickSlot = slotIdx;
        }

        public void OnHold(int slotIdx) { }
        public void OnLMBHold(int slotIdx) { }
        public void OnRMBHold(int slotIdx) { }
        public void OnRelease() { }
        public void OnScroll(int slotIdx, float delta) { }

        public void HandleNumberKey(int hovered, int hotbarSlot)
        {
            if (hovered < 0 || hotbarSlot < 0 || hovered == hotbarSlot) return;
            _inventory.SwapSlots(hovered, hotbarSlot);
            _onRefresh?.Invoke();
        }

        public void HandleQKey(int slotIdx, bool ctrl)
        {
            if (slotIdx < 0 || slotIdx == 45) return;
            DoDrop(slotIdx, ctrl);
        }

        private (Vector3 position, Vector3 velocity) GetDropPositionAndVelocity()
        {
            Camera cam = Camera.main;
            Vector3 dir;
            Vector3 origin;

            if (cam != null)
            {
                dir = cam.transform.forward;
                origin = cam.transform.position;
            }
            else
            {
                dir = Vector3.forward;
                origin = Vector3.zero;
            }

            Vector3 pos = origin + dir * 1.5f;
            Vector3 vel = dir * 3f + Vector3.up * 2f
                + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, UnityEngine.Random.Range(-0.3f, 0.3f));

            return (pos, vel);
        }

        private void DoDoubleClick(int slotIdx)
        {
            if (_cursor.IsEmpty) return;

            int maxStack = _cursor.MaxStack;
            if (_cursor.Amount >= maxStack) return;

            for (int i = 0; i < 46; i++)
            {
                if (i == 45 || i == slotIdx) continue;

                ItemStack s = _inventory.GetSlot(i);
                if (s.ItemID == _cursor.ItemID && s.Amount > 0)
                {
                    int space = maxStack - _cursor.Amount;
                    int take = Mathf.Min(s.Amount, space);
                    _cursor.Stack = new ItemStack(_cursor.ItemID, (byte)(_cursor.Amount + take), _cursor.Stack.Durability);
                    s.Amount -= (byte)take;
                    _inventory.SetSlot(i, s.Amount > 0 ? s : new ItemStack(0, 0));

                    if (_cursor.Amount >= maxStack) break;
                }
            }
            _onRefresh?.Invoke();
        }

        private void DoDrop(int slotIdx, bool ctrl)
        {
            ItemStack slotItem = _inventory.GetSlot(slotIdx);
            if (slotItem.IsEmpty) return;

            ushort itemID = slotItem.ItemID;
            short durability = slotItem.Durability;
            byte currentAmount = slotItem.Amount;

            (Vector3 pos, Vector3 vel) = GetDropPositionAndVelocity();

            if (ItemManager.Instance != null)
            {
                if (ctrl)
                {
                    for (byte b = 0; b < currentAmount; b++)
                        ItemManager.Instance.SpawnItem(itemID, pos, -1f, vel);
                    _inventory.SetSlot(slotIdx, new ItemStack(0, 0));
                }
                else
                {
                    ItemManager.Instance.SpawnItem(itemID, pos, -1f, vel);
                    byte newAmount = (byte)(currentAmount - 1);
                    _inventory.SetSlot(slotIdx, newAmount > 0
                        ? new ItemStack(itemID, newAmount, durability)
                        : new ItemStack(0, 0));
                }
                ItemManager.Instance.OnInventoryDrop();
            }
            _onRefresh?.Invoke();
        }
    }
}