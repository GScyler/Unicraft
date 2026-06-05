using System;
using System.Collections.Generic;
using MinecraftEngine.UI.Interaction;

namespace MinecraftEngine.UI.Interaction
{
    public class RMBDragHandler : ITweakHandler, IRMBHandler, IRMBPressHandler
    {
        public int Priority => 50;
        public bool IsActive => _state != DragState.Idle;

        private readonly CursorContext _cursor;
        private readonly IInventory _inventory;
        private readonly Action _onRefresh;

        private DragState _state = DragState.Idle;
        private HashSet<int> _visitedSlots = new HashSet<int>();
        private int _lastPickupSlot = -1; // Slot where we just picked up - don't bonus place here

        public RMBDragHandler(CursorContext cursor, IInventory inventory, Action onRefresh)
        {
            _cursor = cursor;
            _inventory = inventory;
            _onRefresh = onRefresh;
        }

        public void OnPress(int slotIdx, bool shift, bool ctrl)
        {
            DebugService.Instance.LogHandler("RMBDragHandler", "Press",
                $"Slot={slotIdx}, CursorEmpty={_cursor.IsEmpty}, CursorAmt={_cursor.Amount}");

            if (shift)
            {
                DebugService.Instance.LogHandler("RMBDragHandler", "Press-Skip", "Shift pressed");
                return;
            }
            if (!ConfigService.Config.rmbTweak)
            {
                DebugService.Instance.LogHandler("RMBDragHandler", "Press-Skip", "rmbTweak disabled");
                return;
            }

            if (_cursor.IsEmpty)
            {
                // Cursor empty → pick up half the stack
                if (_cursor.TryPickupHalf(slotIdx))
                {
                    _lastPickupSlot = slotIdx;
                    _state = DragState.Dragging;
                    _onRefresh?.Invoke();
                    DebugService.Instance.LogState("RMBDragHandler", _state, $"Picked up half from slot {slotIdx}");
                    DebugService.Instance.LogHandler("RMBDragHandler", "Press-PickupHalf", $"Cursor now has {_cursor.Amount}");
                }
                else
                {
                    DebugService.Instance.LogHandler("RMBDragHandler", "Press-Skip", "Slot empty");
                }
            }
            else
            {
                // Cursor has item → place one
                TryPlaceOne(slotIdx);
                _state = DragState.Dragging;
                DebugService.Instance.LogState("RMBDragHandler", _state, $"Placed at slot {slotIdx}");
            }
        }

        public void OnRMBHold(int slotIdx)
        {
            if (_cursor.IsEmpty)
            {
                _state = DragState.Idle;
                _lastPickupSlot = -1;
                DebugService.Instance.LogState("RMBDragHandler", _state, "Cursor empty");
                return;
            }
            if (!ConfigService.Config.rmbTweak) return;

            // Clear pickup marker on first hold after pickup
            if (_lastPickupSlot == slotIdx)
            {
                _lastPickupSlot = -1; // Clear after first hold
            }

            bool visitedBefore = _visitedSlots.Contains(slotIdx);

            // Always place at least 1 per hold call
            bool placed = _cursor.TryPlaceOne(slotIdx);
            if (placed) _onRefresh?.Invoke();

            // Bonus: repeat-over slot = additional items (only if not freshly picked)
            if (visitedBefore && _lastPickupSlot == -1)
            {
                int bonus = 1;
                for (int i = 0; i < bonus; i++)
                {
                    if (!_cursor.TryPlaceOne(slotIdx)) break;
                    _onRefresh?.Invoke();
                }
            }

            _visitedSlots.Add(slotIdx);
            DebugService.Instance.LogHandler("RMBDragHandler", "Hold",
                $"Slot={slotIdx}, VisitedBefore={visitedBefore}, Placed={placed}, CursorAmt={_cursor.Amount}");
        }

        public void OnLMBHold(int slotIdx) { }
        public void OnHold(int slotIdx) { }

        public void OnRelease()
        {
            _state = DragState.Idle;
            _visitedSlots.Clear();
            _lastPickupSlot = -1;
        }

        public void OnScroll(int slotIdx, float delta) { }

        private void TryPlaceOne(int slotIdx)
        {
            _cursor.TryPlaceOne(slotIdx);
            _onRefresh?.Invoke();
        }
    }
}