using RevitOSA.WallReinforcer.Caching;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct WallSubCaches
    {
        public List<WallCache.EndCache> Ends { get; set; }
        public List<WallCache.IntersectionCache> Intersections { get; set; }
        public List<WallCache.RegionCache> Regions { get; set; }
    }
}
