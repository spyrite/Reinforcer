using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Caching
{
    public class WallIntersectionCache : RebarHostCache
    {
        private readonly WallCache _parentWallCache;

        // Конструкторы
        public WallIntersectionCache(WallCache wCache, List<RebarHostCache> attachedRebarHostCaches, XYZ origin)
        {
            _parentWallCache = wCache;
            _doc = wCache.Elem.Document;
            Geom = new GeometryWallIntersectionCache(wCache.Geom as GeometryWallCache, [.. attachedRebarHostCaches.Select(c => c.Geom)], origin);
            Reinf = new ReinforcementWallIntersectionCache(wCache.Elem as Wall);

            AttachedRebarHostCaches = attachedRebarHostCaches;

            if (Geom.Solid == null) Geom.GetSolidData();
            Solid catchSolid = Geom.Faces.Top.Select(f => f.GetExtendedSolidFromFace(0, 0, wCache.CatchDeep, false)).ToList().UnionSolids();
            ElementFilter filter = new ElementIntersectsSolidFilter(catchSolid);

            UpperSlabCaches = [.. wCache.UpperSlabCaches.FindAll(c => filter.PassesFilter(c.Elem))];
            UpperColumnCaches = [.. wCache.UpperColumnCaches.FindAll(c => filter.PassesFilter(c.Elem))];
            UpperWallCaches = [.. wCache.UpperWallCaches.FindAll(c => filter.PassesFilter(c.Elem))];
        }

        //Методы
        public bool AllowCreateRebars()
        {
            if (_parentWallCache.Geom.Solid == null) _parentWallCache.Geom.GetSolidData();
            for (double k = 0; k < 1.5; k = k + 0.5)
            {
                Line cutLine = Line.CreateBound(Geom.Origins.CenterMiddleBottom, Geom.Origins.CenterMiddleBottom + XYZ.BasisZ * (10 / 304.8 + Geom.Dims.H * k));
                if (Geom.Solid.IntersectWithCurve(cutLine, null).Any()) return true;
            }
            return false;
        }



        //Свойства
        public List<RebarHostCache> AttachedRebarHostCaches { get; private set; }
    }
}
