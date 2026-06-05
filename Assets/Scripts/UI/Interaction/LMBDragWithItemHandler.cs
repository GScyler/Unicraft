using System;
using System.Collections.Generic;
using UnityEngine;
using MinecraftEngine;
using MinecraftEngine.UI.Interaction;

namespace MinecraftEngine.UI.Interaction
{
    public class LMBDragWithItemHandler : ITweakHandler, ILMBHandler, ILMBPressHandler
    {
        public int Priority => 40;
        public bool IsActive => _state != DragState.Idle && !_cursor.IsEmpty;

        private readonly CursorContext _cursor;
        private readonly IInventory _inventory;
        private readonly Action _onRefresh;

        private DragState _state = DragState.Idle;
        private int _pickupSlot = -1;
        private int _lastProcessedSlot = -1;
        private int _emptyTargetCount = 0;
        private HashSet<int> _holdTargets = new HashSet<int>();

        public LMBDragWithItemHandler(CursorContext cursor, IInventory inventory, Action onRefresh)
        {
            _cursor = cursor;
            _inventory = inventory;
            _onRefresh = onRefresh;
        }

        public void OnPress(int slotIdx, bool shift, bool ctrl)
        {
            DebugService.Instance.LogHandler("LMBDragWithItem", "Press",
                $"Slot={slotIdx}, Shift={shift}, CursorEmpty={_cursor.IsEmpty}, CursorAmt={_cursor.Amount}");

            if (slotIdx < 0) return;
            if (!ConfigService.Config.lmbTweakWithItem) return;

            if (!_cursor.IsEmpty)
            {
                if (shift) return; // shift handled elsewhere
                // Cursor has item → place/merge
                _cursor.TryMerge(slotIdx);
                _state = DragState.Dragging;
                _onRefresh?.Invoke();
                return;
            }

            // Cursor empty → pick up the stack
            if (_cursor.TryPickupAll(slotIdx))
            {
                _pickupSlot = slotIdx;
                _lastProcessedSlot = slotIdx;
                _state = DragState.Dragging;
                _emptyTargetCount = 0;
                _holdTargets.Clear();
                _holdTargets.Add(slotIdx);
                _onRefresh?.Invoke();
                DebugService.Instance.LogState("LMBDragWithItem", _state, $"Picked up from slot {slotIdx}");
            }
        }

        public void OnLMBHold(int slotIdx, bool shift)
        {
            if (_cursor.IsEmpty)
            {
                _state = DragState.Idle;
                DebugService.Instance.LogState("LMBDragWithItem", _state, "Cursor empty");
                return;
            }
            if (!ConfigService.Config.lmbTweakWithItem) return;

            if (slotIdx == _lastProcessedSlot)
            {
                DebugService.Instance.LogHandler("LMBDragWithItem", "Hold-Skip", "Same slot");
                return;
            }
            if (_holdTargets.Contains(slotIdx))
            {
                DebugService.Instance.LogHandler("LMBDragWithItem", "Hold-Skip", "Already visited");
                return;
            }

            _holdTargets.Add(slotIdx);
            _lastProcessedSlot = slotIdx;

            ItemStack target = _inventory.GetSlot(slotIdx);
            string targetInfo = target.IsEmpty ? "Empty" : $"ItemID={target.ItemID}, Amt={target.Amount}";

            if (shift)
            {
                DebugService.Instance.LogHandler("LMBDragWithItem", "Hold-Shift", $"Slot={slotIdx}");
                ShiftClickAllSimilar();
                return;
            }

            if (target.IsEmpty)
            {
                _emptyTargetCount++;
                DebugService.Instance.LogHandler("LMBDragWithItem", "Hold-Empty",
                    $"Slot={slotIdx}, EmptyCount={_emptyTargetCount}");
                if (_emptyTargetCount >= 3)
                {
                    DebugService.Instance.LogHandler("LMBDragWithItem", "REDISTRIBUTE!",
                        $"Total={_cursor.Amount}, Slots={_holdTargets.Count}");
                    RedistributeAcrossVisited();
                }
            }
            else if (target.ItemID == _cursor.ItemID)
            {
                DebugService.Instance.LogHandler("LMBDragWithItem", "Hold-SameType", targetInfo);
                PickupSimilarItems();
            }
            else
            {
                DebugService.Instance.LogHandler("LMBDragWithItem", "Hold-Merge", targetInfo);
                _cursor.TryMerge(slotIdx);
                if (_cursor.IsEmpty) _state = DragState.Idle;
            }

            _onRefresh?.Invoke();
            DebugService.Instance.LogCursor("After Hold", _cursor.ItemID, _cursor.Amount);
        }

        public void OnRMBHold(int slotIdx) { }
        public void OnHold(int slotIdx) { }

        public void OnRelease()
        {
            _state = DragState.Idle;
            _pickupSlot = -1;
            _lastProcessedSlot = -1;
            _holdTargets.Clear();
        }

        public void OnScroll(int slotIdx, float delta) { }

        private void PickupSimilarItems()
        {
            int maxStack = _cursor.MaxStack;
            if (_cursor.Amount >= maxStack) return;

            for (int i = 0; i < 46; i++)
            {
                if (i == _pickupSlot) continue;
                if (_holdTargets.Contains(i)) continue;

                ItemStack s = _inventory.GetSlot(i);
                if (s.ItemID == _cursor.ItemID && s.Amount > 0)
                {
                    int space = maxStack - _cursor.Amount;
                    int take = Mathf.Min(s.Amount, space);
                    _cursor.Stack = new ItemStack(_cursor.ItemID, (byte)(_cursor.Amount + take), _cursor.Stack.Durability);
                    s.Amount -= (byte)take;
                    _inventory.SetSlot(i, s.Amount > 0 ? s : new ItemStack(0, 0));
                    _holdTargets.Add(i);

                    if (_cursor.Amount >= maxStack) break;
                }
            }
        }

        private void RedistributeAcrossVisited()
        {
            List<int> emptySlots = new List<int>();
            foreach (int s in _holdTargets)
            {
                if (s != _pickupSlot && _inventory.GetSlot(s).IsEmpty)
                    emptySlots.Add(s);
            }

            if (emptySlots.Count == 0) return;

            int total = _cursor.Amount;
            int perSlot = total / emptySlots.Count;
            int remainder = total % emptySlots.Count;

            for (int i = 0; i < emptySlots.Count; i++)
            {
                int amount = perSlot + (i < remainder ? 1 : 0);
                _inventory.SetSlot(emptySlots[i],
                    new ItemStack(_cursor.ItemID, (byte)amount, _cursor.Stack.Durability));
                _cursor.Stack = new ItemStack(_cursor.ItemID, (byte)(_cursor.Amount - amount), _cursor.Stack.Durability);
            }
        }

        private void ShiftClickAllSimilar()
        {
            int maxStack = _cursor.MaxStack;
            if (_cursor.Amount >= maxStack) return;

            for (int i = 0; i < 46; i++)
            {
                if (_holdTargets.Contains(i)) continue;

                ItemStack s = _inventory.GetSlot(i);
                if (s.ItemID == _cursor.ItemID && s.Amount > 0)
                {
                    int space = maxStack - _cursor.Amount;
                    int take = Mathf.Min(s.Amount, space);
                    _cursor.Stack = new ItemStack(_cursor.ItemID, (byte)(_cursor.Amount + take), _cursor.Stack.Durability);
                    s.Amount -= (byte)take;
                    _inventory.SetSlot(i, s.Amount > 0 ? s : new ItemStack(0, 0));
                    _holdTargets.Add(i);

                    if (_cursor.Amount >= maxStack) break;
                }
            }
        }
    }
}