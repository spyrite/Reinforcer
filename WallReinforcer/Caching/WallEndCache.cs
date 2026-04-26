using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Caching
{
    public class WallEndCache : RebarHostCache
    {
        //Поля
        private readonly WallCache _parentWallCache;

        //Конструкторы
        public WallEndCache(WallCache wCache, XYZ origin, XYZ xDir)
        {
            _doc = wCache.Elem.Document;
            _parentWallCache = wCache;
            Geom = new GeometryWallEndCache(wCache.Geom as GeometryWallCache, origin, xDir);
            Reinf = new ReinforcementWallEndCache(wCache.Elem as Wall);
        }
    }
}
