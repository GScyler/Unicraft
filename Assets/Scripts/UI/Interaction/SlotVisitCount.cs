using System.Collections.Generic;

namespace MinecraftEngine.UI.Interaction
{
    public class SlotVisitCount
    {
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();

        public void Record(int slotIdx)
        {
            if (!_counts.ContainsKey(slotIdx))
                _counts[slotIdx] = 0;
            _counts[slotIdx]++;
        }

        public int GetCount(int slotIdx)
        {
            return _counts.TryGetValue(slotIdx, out int count) ? count : 0;
        }

        public void Clear()
        {
            _counts.Clear();
        }
    }
}