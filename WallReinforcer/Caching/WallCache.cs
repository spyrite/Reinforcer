using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Parameter = Autodesk.Revit.DB.Parameter;
using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Tools;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Revit.Filters;




#if COMPANY_FP
using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

#elif COMPANY_OLP
using static RevitOSA.WallReinforcer.ResourcesOLP.RevitParameters;
using RevitOSA.WallReinforcer.ResourcesOLP;

#else
#endif

namespace RevitOSA.WallReinforcer.Caching
{
    public class WallCache : RebarHostCache
    {
        // Конструкторы
        public WallCache(Element elem) : base(elem)
        {
            Wall = elem as Wall;
            Geom = new GeometryWallCache(Wall);
            Reinf = new ReinforcementWallCache(Wall);

            CompoundStructure compoundStructure = Wall.WallType.GetCompoundStructure();
            if (compoundStructure != null)
            {
                Layers = compoundStructure.GetLayers().ToList();
                Material material = doc.GetElement(Wall.WallType.GetCompoundStructure().GetLayers().ToList().First().MaterialId) as Material;
                if (ElementParametersAssistant.IsParameterExistAndHasValue(material, pp_BClass))
                {
                    Parameter par = material.GetParameters(pp_BClass).First();
                    switch (par.StorageType)
                    {
                        case StorageType.Integer:
                            BClass = "B" + par.AsInteger().ToString();
                            break;
                        case StorageType.Double:
                            BClass = "B" + par.AsDouble().ToString();
                            break;
                        case StorageType.String:
                            BClass = par.AsString();
                            break;
                    }
                }
            }
        }

        // Методы
        public void AnalyzeForSubCaches()
        {
            // Поиск вышележащих элементов, распределение
            if (Geom.Solid == null) Geom.GetSolidData();
            List<RebarHostCache> allUpperHostCaches = ExtractingTools.GetNearestHostCaches(Geom, Geom.Faces.Top, Side.Top, 500 / 304.8);
            UpperSlabCaches = [];
            UpperWallCaches = [];
            UpperColumnCaches = [];
            foreach (RebarHostCache hostCache in allUpperHostCaches)
            {
                switch (hostCache.GetType().Name)
                {
                    case "SlabCache": UpperSlabCaches.Add(hostCache as SlabCache); break;
                    case "WallCache": UpperWallCaches.Add(hostCache as WallCache); break;
                    case "ColumnCache": UpperColumnCaches.Add(hostCache as ColumnCache); break;
                }
            }

            // Поиск нижележащих элементов
            List<RebarHostCache> allLowerHostCaches = ExtractingTools.GetNearestHostCaches(Geom, Geom.Faces.Bottom, Side.Bottom, 500 / 304.8);

            // Поиск пересечений, окончаний, деление на регионы
            Intersections = ExtractingTools.GetWallIntersectionCaches(Geom as GeometryWallCache);
            Ends = ExtractingTools.GetWallEndCaches(Geom as GeometryWallCache);
            Regions = GetWallRegionCaches();

            // Деление каждого региона вышележащими элементами
            for (int i = 0; i < Regions.Count; i++)
            {
                RegionCache regionCache = Regions[i];
                List<RegionCache> splittedRegionCaches = new List<RegionCache>();
                List<RebarHostCache> upperHostCaches = regionCache.GetNearestHostCaches(allUpperHostCaches, Side.Top, 500 / 304.8);
                foreach (RebarHostCache upperHostCache in upperHostCaches)
                {
                    WallSubCaches upperSubCaches = regionCache.SplitByAnotherHost(upperHostCache);
                    splittedRegionCaches.AddRange(upperSubCaches.Regions);
                    if (splittedRegionCaches.Count > 0)
                    {
                        Regions.RemoveAt(i);
                        Regions.InsertRange(i, splittedRegionCaches);
                    }
                    Ends.AddRange(upperSubCaches.Ends);
                    Intersections.AddRange(upperSubCaches.Intersections);
                }
            }

            // Деление каждого региона нижележащими элементами
            for (int i = 0; i < Regions.Count; i++)
            {
                RegionCache regionCache = Regions[i];
                List<RegionCache> splittedRegionCaches = new List<RegionCache>();
                List<RebarHostCache> lowerHostCaches = regionCache.GetNearestHostCaches(allLowerHostCaches, Side.Bottom, 500 / 304.8);
                foreach (RebarHostCache lowerHostCache in lowerHostCaches)
                {
                    WallSubCaches lowerSubCaches = regionCache.SplitByAnotherHost(lowerHostCache);
                    splittedRegionCaches.AddRange(lowerSubCaches.Regions);
                    if (splittedRegionCaches.Count > 0)
                    {
                        Regions.RemoveAt(i);
                        Regions.InsertRange(i, splittedRegionCaches);
                    }
                    Ends.AddRange(lowerSubCaches.Ends);
                    Intersections.AddRange(lowerSubCaches.Intersections);
                }
            }
        }

        public void GetVoidCaches()
        {
            List<VoidCache> voidCaches = ExtractingTools.ExtractVoidCaches(Elem);
            Voids = (from voidCache in voidCaches
                     where !voidCache.IsOpening
                     select voidCache).ToList();
        }

        private List<RegionCache> GetWallRegionCaches()
        {
            // Инициализация
            List<RegionCache> regionCaches = new List<RegionCache>();
            List<XYZ> newWallRegionsPoints = new List<XYZ>();

            // Деление на регионы окончаниями и пересечениями
            newWallRegionsPoints.AddRange(GetWallRegionsPointsFromEnds());
            newWallRegionsPoints.AddRange(GetWallRegionsPointsFromIntersections());
            newWallRegionsPoints = newWallRegionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            for (int i = 1; i < newWallRegionsPoints.Count - 1; i += 2)
            {
                RegionCache regionCache = new RegionCache(Geom as GeometryWallCache, newWallRegionsPoints[i], newWallRegionsPoints[i + 1]);
                regionCaches.Add(regionCache);
            }

            // Деление регионов на регионы отвестиями и проёмами, прилегающими к нижней части
            List<XYZ> wallRegionsPointsFromVoids = GetWallRegionsPointsFromVoids().OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            for (int i = 0; i < regionCaches.Count; i++)
            {
                RegionCache regionCache = regionCaches[i];
                List<XYZ> splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(wallRegionsPointsFromVoids, regionCache.Geom);
                if (splitPoints.Count > 0)
                {
                    List<RegionCache> splittedRegionCaches = regionCache.SplitByPoints(splitPoints);
                    if (splittedRegionCaches != null)
                    {
                        regionCaches.RemoveAt(i);
                        regionCaches.InsertRange(i, splittedRegionCaches);
                    }
                }
            }

            return regionCaches;
        }
        private protected List<XYZ> GetWallRegionsPointsFromEnds()
        {
            List<XYZ> regionsPoints = new List<XYZ>();
            if (Ends == null) Ends = ExtractingTools.GetWallEndCaches(Geom as GeometryWallCache);
            foreach (EndCache end in Ends)
            {
                if (end != null)
                {
                    List<XYZ> splitPoints = new List<XYZ>
                    {
                        end.Geom.Origins.CenterStartBottom,
                        end.Geom.Origins.CenterEndBottom
                    };
                    regionsPoints.AddRange(splitPoints);
                }
            }
            regionsPoints = regionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            return regionsPoints;
        }
        private protected List<XYZ> GetWallRegionsPointsFromIntersections()
        {
            List<XYZ> regionsPoints = new List<XYZ>();
            if (Intersections == null) Intersections = ExtractingTools.GetWallIntersectionCaches(Geom as GeometryWallCache);
            foreach (IntersectionCache intersection in Intersections)
            {
                List<XYZ> splitPoints = new List<XYZ>
                {
                    intersection.Geom.Origins.CenterStartBottom,
                    intersection.Geom.Origins.CenterEndBottom
                };
                regionsPoints.AddRange(splitPoints);
            }
            regionsPoints = regionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            return regionsPoints;
        }
        private protected List<XYZ> GetWallRegionsPointsFromVoids()
        {
            List<XYZ> regionsPoints = new List<XYZ>();
            if (Reinf.Anchors == null) Reinf.GetAnchors(Geom);
            if (Voids == null) GetVoidCaches();
            foreach (VoidCache voidCache in Voids)
            {
                double offsetVoidBot = voidCache.Geom.GetOffsetFromWallBottom();
                Plane planeVoidBot = Plane.CreateByOriginAndBasis(voidCache.Geom.Origins.CenterMiddleBottom, Geom.Dirs.X, Geom.Dirs.Y);
                double maxAnchorLengthUnderVoid = 0;
                foreach (RebarAnchorCache anchorCache in Reinf.Anchors)
                {
                    foreach (AnchorPartData anchorPartData in anchorCache.AnchorPartDatas)
                    {
                        planeVoidBot.Project(anchorPartData.BottomPoint, out UV uv, out _);
                        if ((uv.U >= -voidCache.Geom.Dims.L / 2 & uv.U <= voidCache.Geom.Dims.L / 2)
                            && anchorPartData.Length > maxAnchorLengthUnderVoid)
                            maxAnchorLengthUnderVoid = anchorPartData.Length;
                    }
                }
                if (Math.Round(offsetVoidBot * 304.8) < Math.Round((maxAnchorLengthUnderVoid + ReinfSettings.Default.reinf_RebarCover_Edge) * 304.8))
                {
                    regionsPoints.Add(voidCache.Geom.Origins.CenterStartBottom);
                    regionsPoints.Add(voidCache.Geom.Origins.CenterEndBottom);
                }
            }
            regionsPoints = regionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            return regionsPoints;
        }

        // Подклассы
        public class RegionCache : RebarHostCache
        {
            // Поля
            private protected GeometryWallCache geomWallCache;

            // Конструкторы
            public RegionCache(GeometryWallCache geomWallCache, XYZ startPoint, XYZ endPoint)
            {
                this.geomWallCache = geomWallCache;
                Geom = new GeometryWallCache.RegionCache(geomWallCache, startPoint, endPoint);
                Reinf = new ReinforcementWallCache.RegionCache(geomWallCache.Elem as Wall);
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
                    Ends = new List<EndCache>(),
                    Intersections = new List<IntersectionCache>(),
                    Regions = new List<RegionCache>(),
                };
                List<XYZ> splitPoints = new List<XYZ>();
                Plane planeRegionBot = Plane.CreateByOriginAndBasis(Geom.Origins.CenterMiddleBottom, Geom.Dirs.X, Geom.Dirs.Y);

                switch (anotherHostCache.GetType().Name)
                {
                    case "WallCache":
                        // Деление региона на регионы окончаниями и пересечениями другой стены
                        WallCache anotherWallCache = anotherHostCache as WallCache;
                        splitPoints = new List<XYZ>();
                        splitPoints.AddRange(anotherWallCache.GetWallRegionsPointsFromEnds());
                        splitPoints.AddRange(anotherWallCache.GetWallRegionsPointsFromIntersections());
                        splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(splitPoints, Geom);
                        if (splitPoints.Count > 0)
                        {
                            splitPoints = (from point in splitPoints
                                           select GeometryTools.ProjectPointOntoPlane(planeRegionBot, point)).ToList();
                            List<XYZ> newWallRegionsPoints = new List<XYZ> { Geom.Origins.CenterStartBottom, Geom.Origins.CenterEndBottom };
                            newWallRegionsPoints.AddRange(splitPoints);
                            newWallRegionsPoints = newWallRegionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
                            for (int i = 0; i < newWallRegionsPoints.Count - 1; i += 2)
                            {
                                RegionCache regionCache = new RegionCache(Geom as GeometryWallCache, newWallRegionsPoints[i], newWallRegionsPoints[i + 1]);
                                data.Regions.Add(regionCache);
                            }

                            // Деление регионов на регионы отвестиями и проёмами, прилегающими к нижней части
                            List<XYZ> wallRegionsPointsFromVoids = anotherWallCache.GetWallRegionsPointsFromVoids().OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
                            for (int i = 0; i < data.Regions.Count; i++)
                            {
                                RegionCache regionCache = data.Regions[i];
                                splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(wallRegionsPointsFromVoids, regionCache.Geom);
                                if (splitPoints.Count > 0)
                                {
                                    List<RegionCache> splittedRegionCaches = regionCache.SplitByPoints(splitPoints);
                                    if (splittedRegionCaches != null)
                                    {
                                        data.Regions.RemoveAt(i);
                                        data.Regions.InsertRange(i, splittedRegionCaches);
                                    }
                                }
                            }
                        }

                        // Добавление окончаний с другой стены
                        foreach (EndCache anotherEndCache in anotherWallCache.Ends)
                        {
                            planeRegionBot.Project(anotherEndCache.Geom.Origins.CenterStartBottom, out UV uv, out _);
                            if (uv.U > -Geom.Dims.L / 2 & uv.U < Geom.Dims.L / 2)
                            {
                                XYZ origin = GeometryTools.ProjectPointOntoPlane(planeRegionBot, anotherEndCache.Geom.Origins.CenterStartBottom);
                                EndCache endCache = new EndCache(geomWallCache, origin, anotherEndCache.Geom.Dirs.X);
                                data.Ends.Add(endCache);
                            }
                        }

                        // Добавление пересечений с другой стены
                        foreach (IntersectionCache anotherIntersectionCache in anotherWallCache.Intersections)
                        {
                            planeRegionBot.Project(anotherIntersectionCache.Geom.Origins.CenterMiddleBottom, out UV uv, out _);
                            if (uv.U > -Geom.Dims.L / 2 & uv.U < Geom.Dims.L / 2)
                            {
                                XYZ origin = GeometryTools.ProjectPointOntoPlane(planeRegionBot, anotherIntersectionCache.Geom.Origins.CenterMiddleBottom);
                                IntersectionCache intersectionCache = new IntersectionCache(geomWallCache, null, origin);
                                intersectionCache.Geom.Dims = anotherIntersectionCache.Geom.Dims;
                                intersectionCache.Geom.Dirs = anotherIntersectionCache.Geom.Dirs;
                                data.Intersections.Add(intersectionCache);
                            }
                        }
                        return data;

                    case "ColumnCache":
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
            public List<RegionCache> SplitByPoints(List<XYZ> splitPoints)
            {
                List<RegionCache> regionCaches = new List<RegionCache>();
                splitPoints.Add(Geom.Origins.CenterStartBottom);
                splitPoints.Add(Geom.Origins.CenterEndBottom);
                splitPoints = splitPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();

                for (int j = 0; j < splitPoints.Count - 1; j++)
                    regionCaches.Add(new RegionCache(Geom as GeometryWallCache, splitPoints[j], splitPoints[j + 1]));

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
        public class IntersectionCache : RebarHostCache
        {
            // Конструкторы
            public IntersectionCache(GeometryWallCache geomWallCache, List<GeometryCache> attachedGeometryCaches, XYZ origin)
            {
                doc = geomWallCache.Elem.Document;
                Geom = new GeometryWallCache.IntersectionCache(geomWallCache, attachedGeometryCaches, origin);
                Reinf = new ReinforcementWallCache.IntersectionCache(geomWallCache.Elem as Wall);
            }
        }
        public class EndCache : RebarHostCache
        {
            // Конструкторы
            public EndCache(GeometryWallCache geomWallCache, XYZ origin, XYZ xDir)
            {
                doc = geomWallCache.Elem.Document;
                Geom = new GeometryWallCache.EndCache(geomWallCache, origin, xDir);
                Reinf = new ReinforcementWallCache.EndCache(geomWallCache.Elem as Wall);
            }
        }

        // Свойства
        public Wall Wall { get; set; }
        public List<CompoundStructureLayer> Layers { get; set; }
        public List<RegionCache> Regions { get; set; }
        public List<IntersectionCache> Intersections { get; set; }
        public List<EndCache> Ends { get; set; }
        public List<VoidCache> Voids { get; set; }
    }
}
