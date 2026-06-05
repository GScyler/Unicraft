using System;
using UnityEngine;
using MinecraftEngine;

namespace MinecraftEngine.UI.Interaction
{
    public class WheelTweakHandler : ITweakHandler
    {
        public int Priority => 20;
        public bool IsActive => false;

        private readonly CursorContext _cursor;
        private readonly IInventory _inventory;
        private readonly Action _onRefresh;

        private float _accumulatedDelta = 0f;

        public WheelTweakHandler(CursorContext cursor, IInventory inventory, Action onRefresh)
        {
            _cursor = cursor;
            _inventory = inventory;
            _onRefresh = onRefresh;
        }

        public void OnPress(int slotIdx, bool shift, bool ctrl) { }
        public void OnHold(int slotIdx) { }
        public void OnLMBHold(int slotIdx) { }
        public void OnRMBHold(int slotIdx) { }
        public void OnRelease()
        {
            _accumulatedDelta = 0f;
        }
        public void OnScroll(int slotIdx, float delta)
        {
            if (slotIdx < 0) return;
            if (!ConfigService.Config.wheelTweak) return;

            // Accumulate scroll delta
            _accumulatedDelta += delta;

            // Apply when we have a whole number
            int itemsToMove = (int)_accumulatedDelta;
            if (itemsToMove == 0) return;

            _accumulatedDelta -= (float)itemsToMove;

            bool pushItems = itemsToMove < 0; // negative = push out
            itemsToMove = Mathf.Abs(itemsToMove);

            if (pushItems)
            {
                // Push items: put from cursor into slot
                if (!_cursor.IsEmpty)
                {
                    for (int i = 0; i < itemsToMove; i++)
                    {
                        if (!_cursor.TryPlaceOne(slotIdx)) break;
                    }
                    _onRefresh?.Invoke();
                }
            }
            else
            {
                // Pull items: take from slot into cursor
                if (_cursor.IsEmpty)
                {
                    ItemStack s = _inventory.GetSlot(slotIdx);
                    if (!s.IsEmpty)
                    {
                        byte take = (byte)Mathf.Min(itemsToMove, s.Amount);
                        _cursor.Stack = new ItemStack(s.ItemID, take, s.Durability);
                        s.Amount -= take;
                        _inventory.SetSlot(slotIdx, s.Amount > 0 ? s : new ItemStack(0, 0));
                        _onRefresh?.Invoke();
                    }
                }
                else
                {
                    // Cursor has item, take more of same type
                    ItemStack s = _inventory.GetSlot(slotIdx);
                    if (!s.IsEmpty && s.ItemID == _cursor.ItemID && _cursor.Amount < _cursor.MaxStack)
                    {
                        int space = _cursor.MaxStack - _cursor.Amount;
                        int take = Mathf.Min(itemsToMove, Mathf.Min(space, s.Amount));
                        s.Amount -= (byte)take;
                        _cursor.Stack = new ItemStack(_cursor.ItemID, (byte)(_cursor.Amount + take), _cursor.Stack.Durability);
                        _inventory.SetSlot(slotIdx, s.Amount > 0 ? s : new ItemStack(0, 0));
                        _onRefresh?.Invoke();
                    }
                }
            }
        }
    }
}