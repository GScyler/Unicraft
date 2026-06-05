using System.Collections.Generic;

namespace MinecraftEngine.UI.Interaction
{
    public class InputMediator
    {
        private readonly List<ITweakHandler> _handlers = new List<ITweakHandler>();

        public void Register(ITweakHandler handler)
        {
            _handlers.Add(handler);
            _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void OnMousePress(int slotIdx, MouseButton button, bool shift, bool ctrl)
        {
            DebugService.Instance.LogInput(button, "OnMousePress", slotIdx, shift);
            DebugService.Instance.LogHandler("InputMediator", "Press-Start", $"Count={_handlers.Count}, Button={button}");

            foreach (var handler in _handlers)
            {
                bool isLMBPress = handler is ILMBPressHandler;
                bool isRMBPress = handler is IRMBPressHandler;
                DebugService.Instance.LogHandler("InputMediator", "Handler",
                    $"Type={handler.GetType().Name}, ILMBPress={isLMBPress}, IRMBPress={isRMBPress}");

                // Button-specific press handlers - only call if matches
                if (handler is ILMBPressHandler lmbHandler && button == MouseButton.Left)
                {
                    DebugService.Instance.LogHandler("InputMediator", "Calling", $"ILMBPressHandler.{handler.GetType().Name}");
                    lmbHandler.OnPress(slotIdx, shift, ctrl);
                }
                else if (handler is IRMBPressHandler rmbHandler && button == MouseButton.Right)
                {
                    DebugService.Instance.LogHandler("InputMediator", "Calling", $"IRMBPressHandler.{handler.GetType().Name}");
                    rmbHandler.OnPress(slotIdx, shift, ctrl);
                }
                // Handlers WITHOUT button-specific interfaces (QuickActions, etc.) - call normally
                else if (!isLMBPress && !isRMBPress)
                {
                    DebugService.Instance.LogHandler("InputMediator", "Calling", $"Generic.{handler.GetType().Name}");
                    handler.OnPress(slotIdx, shift, ctrl);
                }
                else
                {
                    DebugService.Instance.LogHandler("InputMediator", "Skipped", $"{handler.GetType().Name} (wrong button)");
                }
            }
        }

        public void OnMouseHold(int slotIdx, MouseButton button, bool shift)
        {
            DebugService.Instance.LogInput(button, "OnMouseHold", slotIdx, shift);
            foreach (var handler in _handlers)
            {
                if (handler is ILMBHandler lmb && button == MouseButton.Left)
                {
                    lmb.OnLMBHold(slotIdx, shift);
                }
                else if (handler is IRMBHandler rmb && button == MouseButton.Right)
                {
                    rmb.OnRMBHold(slotIdx);
                }
            }
        }

        public void OnMouseRelease(MouseButton button)
        {
            DebugService.Instance.LogInput(button, "OnMouseRelease", -1);
            foreach (var handler in _handlers)
            {
                handler.OnRelease();
            }
        }

        public void OnScroll(int slotIdx, float delta)
        {
            foreach (var handler in _handlers)
            {
                handler.OnScroll(slotIdx, delta);
            }
        }

        public void ResetAll()
        {
            foreach (var handler in _handlers)
            {
                handler.OnRelease();
            }
        }
    }
}