using System;
using System.Collections.Generic;
using MinecraftEngine.UI.Interaction;

namespace MinecraftEngine.UI.Interaction
{
    public class LMBDragWithoutItemHandler : ITweakHandler, ILMBHandler, ILMBPressHandler
    {
        public int Priority => 30;
        public bool IsActive => _state != DragState.Idle && _cursor.IsEmpty;

        private readonly CursorContext _cursor;
        private readonly IInventory _inventory;
        private readonly Action _onRefresh;

        private DragState _state = DragState.Idle;
        private HashSet<int> _visitedSlots = new HashSet<int>();

        public LMBDragWithoutItemHandler(CursorContext cursor, IInventory inventory, Action onRefresh)
        {
            _cursor = cursor;
            _inventory = inventory;
            _onRefresh = onRefresh;
        }

        public void OnPress(int slotIdx, bool shift, bool ctrl)
        {
            if (slotIdx < 0) return;
            if (!shift || !_cursor.IsEmpty) return;
            if (!ConfigService.Config.lmbTweakWithoutItem) return;

            _inventory.ShiftClickSlot(slotIdx);
            _visitedSlots.Add(slotIdx);
            _state = DragState.Dragging;
            _onRefresh?.Invoke();
        }

        public void OnLMBHold(int slotIdx, bool shift)
        {
            if (!shift || !_cursor.IsEmpty) return;
            if (!ConfigService.Config.lmbTweakWithoutItem) return;
            if (_visitedSlots.Contains(slotIdx)) return;
            _visitedSlots.Add(slotIdx);

            _inventory.ShiftClickSlot(slotIdx);
            _onRefresh?.Invoke();
        }

        public void OnRMBHold(int slotIdx) { }
        public void OnHold(int slotIdx) { }

        public void OnRelease()
        {
            _state = DragState.Idle;
            _visitedSlots.Clear();
        }

        public void OnScroll(int slotIdx, float delta) { }
    }
}