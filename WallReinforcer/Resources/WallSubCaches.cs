using RevitOSA.WallReinforcer.Caching;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct WallSubCaches
    {
        public List<WallEndCache> Ends { get; set; }
        public List<WallIntersectionCache> Intersections { get; set; }
        public List<WallRegionCache> Regions { get; set; }
    }
}
