using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Caching
{
    public class WallRegionCache : RebarHostCache
    {
        // Поля
        private WallCache _parentWallCache;

        // Конструкторы
        public WallRegionCache(WallCache wCache, XYZ startPoint, XYZ endPoint)
        {
            _parentWallCache = wCache;
            Geom = new GeometryWallRegionCache(wCache.Geom as GeometryWallCache, startPoint, endPoint);
            Reinf = new ReinforcementWallRegionCache(wCache.Elem as Wall);

            ComputeVRebarQuantitiesAndAlign();
        }

        // Методы
        public List<RebarHostCache> GetNearestHostCaches(List<RebarHostCache> hostCacheCandidates, Side side, double deep)
        {
            // Инициализация
            List<RebarHostCache> nearestHostCaches = new List<RebarHostCache>();
            if (Geom.Solid == null) Geom.GetSolidData();
            List<PlanarFace> faces = new List<PlanarFace>();
            switch (side)
            {
                case Side.Top: faces = Geom.Faces.Top; break;
                case Side.Bottom: faces = Geom.Faces.Bottom; break;
                default: return nearestHostCaches;
            }

            // Поиск прилегающих (ближайших) хостов
            foreach (PlanarFace face in faces)
            {
                Solid catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(), face.FaceNormal, deep);
                ElementFilter filter = new ElementIntersectsSolidFilter(catchSolid);
                nearestHostCaches.AddRange(from hostCache in hostCacheCandidates
                                           where filter.PassesFilter(hostCache.Elem)
                                           select hostCache);
            }
            return nearestHostCaches;
        }
        public WallSubCaches SplitByAnotherHost(RebarHostCache anotherHostCache)
        {
            // Инициализация
            WallSubCaches data = new WallSubCaches
            {
                Ends = new List<WallEndCache>(),
                Intersections = new List<WallIntersectionCache>(),
                Regions = new List<WallRegionCache>(),
            };
            List<XYZ> splitPoints = new List<XYZ>();
            Plane planeRegionBot = Plane.CreateByOriginAndBasis(Geom.Origins.CenterMiddleBottom, Geom.Dirs.X, Geom.Dirs.Y);

            switch (anotherHostCache)
            {
                case WallCache:
                    // Деление региона на регионы окончаниями и пересечениями другой стены
                    WallCache anotherWallCache = anotherHostCache as WallCache;
                    splitPoints =
                    [
                        .. anotherWallCache.GetWallRegionsPointsFromEnds(),
                        .. anotherWallCache.GetWallRegionsPointsFromIntersections(),
                    ];
                    splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(splitPoints, Geom);
                    if (splitPoints.Count > 0)
                    {
                        splitPoints = (from point in splitPoints
                                       select GeometryTools.ProjectPointOntoPlane(planeRegionBot, point)).ToList();
                        List<XYZ> newWallRegionsPoints = [Geom.Origins.CenterStartBottom, Geom.Origins.CenterEndBottom, .. splitPoints];
                        newWallRegionsPoints = newWallRegionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
                        for (int i = 0; i < newWallRegionsPoints.Count - 1; i += 2)
                        {
                            WallRegionCache regionCache = new WallRegionCache(_parentWallCache, newWallRegionsPoints[i], newWallRegionsPoints[i + 1]);
                            data.Regions.Add(regionCache);
                        }

                        // Деление регионов на регионы отвестиями и проёмами, прилегающими к нижней части
                        List<XYZ> wallRegionsPointsFromVoids = anotherWallCache.GetWallRegionsPointsFromVoids().OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
                        for (int i = 0; i < data.Regions.Count; i++)
                        {
                            WallRegionCache regionCache = data.Regions[i];
                            splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(wallRegionsPointsFromVoids, regionCache.Geom);
                            if (splitPoints.Count > 0)
                            {
                                List<WallRegionCache> splittedRegionCaches = regionCache.SplitByPoints(splitPoints);
                                if (splittedRegionCaches != null)
                                {
                                    data.Regions.RemoveAt(i);
                                    data.Regions.InsertRange(i, splittedRegionCaches);
                                }
                            }
                        }
                    }

                    // Добавление окончаний с другой стены
                    foreach (WallEndCache anotherEndCache in anotherWallCache.Ends)
                    {
                        planeRegionBot.Project(anotherEndCache.Geom.Origins.CenterStartBottom, out UV uv, out _);
                        if (uv.U > -Geom.Dims.L / 2 & uv.U < Geom.Dims.L / 2)
                        {
                            XYZ origin = GeometryTools.ProjectPointOntoPlane(planeRegionBot, anotherEndCache.Geom.Origins.CenterStartBottom);
                            WallEndCache endCache = new(_parentWallCache, origin, anotherEndCache.Geom.Dirs.X);
                            data.Ends.Add(endCache);
                        }
                    }

                    // Добавление пересечений с другой стены
                    foreach (WallIntersectionCache anotherIntersectionCache in anotherWallCache.Intersections)
                    {
                        planeRegionBot.Project(anotherIntersectionCache.Geom.Origins.CenterMiddleBottom, out UV uv, out _);
                        if (uv.U > -Geom.Dims.L / 2 & uv.U < Geom.Dims.L / 2)
                        {
                            XYZ origin = GeometryTools.ProjectPointOntoPlane(planeRegionBot, anotherIntersectionCache.Geom.Origins.CenterMiddleBottom);
                            WallIntersectionCache intersectionCache = new(_parentWallCache, null, origin);
                            intersectionCache.Geom.Dims = anotherIntersectionCache.Geom.Dims;
                            intersectionCache.Geom.Dirs = anotherIntersectionCache.Geom.Dirs;
                            data.Intersections.Add(intersectionCache);
                        }
                    }
                    return data;

                case ColumnCache:
                    ColumnCache anotherColumnCache = anotherHostCache as ColumnCache;
                    splitPoints = new List<XYZ>
                        {
                            anotherColumnCache.Geom.Origins.CenterStartBottom,
                            anotherColumnCache.Geom.Origins.CenterEndBottom
                        };
                    splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(splitPoints, Geom);
                    if (splitPoints.Count > 0)
                    {
                        splitPoints = (from point in splitPoints
                                       select GeometryTools.ProjectPointOntoPlane(planeRegionBot, point)).ToList();
                        data.Regions = SplitByPoints(splitPoints);
                    }
                    return data;

                default: return data;
            }
        }
        public List<WallRegionCache> SplitByPoints(List<XYZ> splitPoints)
        {
            List<WallRegionCache> regionCaches = new List<WallRegionCache>();
            splitPoints.Add(Geom.Origins.CenterStartBottom);
            splitPoints.Add(Geom.Origins.CenterEndBottom);
            splitPoints = splitPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();

            for (int j = 0; j < splitPoints.Count - 1; j++)
                regionCaches.Add(new WallRegionCache(_parentWallCache, splitPoints[j], splitPoints[j + 1]));

            if (regionCaches.Count > 1) return regionCaches;
            else return null;
        }

        private void ComputeVRebarQuantitiesAndAlign()
        {
            double L0 = Geom.Dims.L - Reinf.DataY.Step;
            int N = (int)Math.Round(L0 / Reinf.DataY.Step) + 1;

            VRebarQuantities = N % 2 == 0
                ? [N / 2, N / 2, N / 2, N / 2]
                : [(N + 1) / 2, N - (N + 1) / 2, (N + 1) / 2, N - (N + 1) / 2];

            VRebarAlign = (Geom.Dims.L - Reinf.DataY.Step * (N - 1)) / 2;
        }

        // Свойства
        public List<int> VRebarQuantities { get; private set; }
        public double VRebarAlign { get; private set; }
    }
}
