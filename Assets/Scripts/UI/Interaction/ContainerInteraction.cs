using System;
using UnityEngine;

namespace MinecraftEngine.UI.Interaction
{
    public class ContainerInteraction
    {
        private readonly CursorContext _cursor;
        private readonly InputMediator _mediator;
        private readonly Action _onRefresh;

        private RMBDragHandler _rmbHandler;
        private LMBDragWithItemHandler _lmbWithItemHandler;
        private LMBDragWithoutItemHandler _lmbWithoutItemHandler;
        private WheelTweakHandler _wheelHandler;
        private QuickActionsHandler _quickHandler;

        public CursorContext Cursor => _cursor;

        public ContainerInteraction(IInventory inventory, Action onRefresh)
        {
            _onRefresh = onRefresh;
            _cursor = new CursorContext(inventory);
            _mediator = new InputMediator();

            _rmbHandler = new RMBDragHandler(_cursor, inventory, onRefresh);
            _lmbWithItemHandler = new LMBDragWithItemHandler(_cursor, inventory, onRefresh);
            _lmbWithoutItemHandler = new LMBDragWithoutItemHandler(_cursor, inventory, onRefresh);
            _wheelHandler = new WheelTweakHandler(_cursor, inventory, onRefresh);

            Vector3 dropDir = Vector3.forward;
            Camera cam = Camera.main;
            if (cam != null) dropDir = cam.transform.forward;

            Vector3 dropVel = dropDir * 3f + Vector3.up * 2f
                + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0, UnityEngine.Random.Range(-0.3f, 0.3f));

            _quickHandler = new QuickActionsHandler(
                _cursor,
                inventory,
                onRefresh);

            // Register in priority order (lowest first, highest last for iteration)
            _mediator.Register(_quickHandler);
            _mediator.Register(_wheelHandler);
            _mediator.Register(_lmbWithoutItemHandler);
            _mediator.Register(_lmbWithItemHandler);
            _mediator.Register(_rmbHandler);
        }

        public void OnMousePress(int slotIdx, MouseButton button, bool shift, bool ctrl)
        {
            _mediator.OnMousePress(slotIdx, button, shift, ctrl);
        }

        public void OnMouseHold(int slotIdx, MouseButton button, bool shift)
        {
            _mediator.OnMouseHold(slotIdx, button, shift);
        }

        public void OnMouseRelease(MouseButton button)
        {
            _mediator.OnMouseRelease(button);
        }

        public void OnScroll(int slotIdx, float delta)
        {
            _mediator.OnScroll(slotIdx, delta);
        }

        public void HandleNumberKey(int hovered, int hotbarSlot)
        {
            _quickHandler.HandleNumberKey(hovered, hotbarSlot);
        }

        public void HandleQKey(int slotIdx, bool ctrl)
        {
            _quickHandler.HandleQKey(slotIdx, ctrl);
        }

        public void ReturnCursor()
        {
            _cursor.ReturnToInventory();
            _mediator.ResetAll();
            _onRefresh?.Invoke();
        }
    }
}