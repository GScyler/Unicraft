namespace MinecraftEngine.UI.Interaction
{
    public enum MouseButton
    {
        Left,
        Right
    }

    public interface ITweakHandler
    {
        int Priority { get; }
        bool IsActive { get; }

        void OnPress(int slotIdx, bool shift, bool ctrl);
        void OnHold(int slotIdx);
        void OnRelease();
        void OnScroll(int slotIdx, float delta);
    }

    public interface ILMBHandler
    {
        void OnLMBHold(int slotIdx, bool shift);
    }

    public interface IRMBHandler
    {
        void OnRMBHold(int slotIdx);
    }

    public interface ILMBPressHandler
    {
        void OnPress(int slotIdx, bool shift, bool ctrl);
    }

    public interface IRMBPressHandler
    {
        void OnPress(int slotIdx, bool shift, bool ctrl);
    }
}