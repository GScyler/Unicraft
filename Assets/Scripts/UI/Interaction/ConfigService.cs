namespace MinecraftEngine.UI.Interaction
{
    public static class ConfigService
    {
        private static MouseTweaksConfig _config;

        public static MouseTweaksConfig Config
        {
            get
            {
                if (_config == null)
                    _config = new MouseTweaksConfig();
                return _config;
            }
        }

        public static void Load() { /* future: load from file */ }
        public static void Save() { /* future: save to file */ }
    }
}