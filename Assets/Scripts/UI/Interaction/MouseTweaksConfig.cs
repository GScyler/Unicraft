namespace MinecraftEngine.UI.Interaction
{
    public class MouseTweaksConfig
    {
        public bool rmbTweak = true;
        public bool lmbTweakWithItem = true;
        public bool lmbTweakWithoutItem = true;
        public bool wheelTweak = true;

        public bool scrollUpPushesItems = true; // true = scroll up = put back, false = invert
        public bool searchFromEnd = true; // true = last to first, false = first to last

        public bool debug = false;
    }
}