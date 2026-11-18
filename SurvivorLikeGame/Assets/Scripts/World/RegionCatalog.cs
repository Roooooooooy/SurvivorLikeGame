using UnityEngine;

namespace RpgGame
{
    // 运行时按 id 查找 Resources/Regions 下的区域资产。
    public static class RegionCatalog
    {
        const string ResourcePath = "Regions";
        static RegionDefinition[] all;

        public static RegionDefinition[] All
        {
            get
            {
                if (all == null)
                    all = Resources.LoadAll<RegionDefinition>(ResourcePath);
                return all;
            }
        }

        public static RegionDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            foreach (var region in All)
                if (region != null && region.id == id)
                    return region;

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all = null;
    }
}
