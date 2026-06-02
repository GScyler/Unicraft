using UnityEngine;

namespace MinecraftEngine
{
    /// <summary>
    /// Food item with nutrition, saturation, eat speed, and optional effects.
    /// </summary>
    [CreateAssetMenu(fileName = "New Food", menuName = "MinecraftEngine/Food Data")]
    public class FoodData : ItemData
    {
        [Header("Food Properties")]
        [Tooltip("Hunger points restored (Apple=4, Bread=5, CookedBeef=8, GoldenCarrot=6)")]
        public int nutrition = 4;

        [Tooltip("Saturation modifier (Apple=0.3, Bread=0.6, CookedBeef=0.8, GoldenCarrot=1.2)")]
        public float saturationModifier = 0.3f;

        [Tooltip("Time to eat in seconds (default 1.61, Dried Kelp=0.865)")]
        public float eatDuration = 1.61f;

        [Tooltip("Can eat when hunger is full (Golden Apple, Chorus Fruit)")]
        public bool canAlwaysEat = false;

        // Future: StatusEffectInstance[] effectsOnEat
    }
}
