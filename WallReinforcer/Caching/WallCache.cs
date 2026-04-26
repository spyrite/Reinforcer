using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Parameter = Autodesk.Revit.DB.Parameter;
using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Tools;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Revit.Filters;
using static RevitOSA.WallReinforcer.Assistants.Sorting;



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
        //Поля
        private protected bool _isAnalyzedForSubCaches;

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
                Material material = _doc.GetElement(Wall.WallType.GetCompoundStructure().GetLayers().ToList().First().MaterialId) as Material;
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

            _isAnalyzedForSubCaches = false;
        }

        // Методы
        /// <summary>
        /// Анализирует стену для создания подкэшей: регионов, пересечений, окончаний
        /// </summary>
        /// <param name="catchDeep">Глубина захвата соседних элементов</param>
        public void AnalyzeForSubCaches(double catchDeep)
        {
            Debug.WriteLine($"[WallCache] Начало анализа подкэшей для стены {Element.Id}");

            if (catchDeep <= 0)
            {
                Debug.WriteLine($"[WallCache] Ошибка: некорректное значение catchDeep={catchDeep}");
                throw new ArgumentOutOfRangeException(nameof(catchDeep), catchDeep, "Глубина захвата должна быть положительным числом");
            }

            if (_isAnalyzedForSubCaches)
            {
                Debug.WriteLine($"[WallCache] Пропуск: стена уже проанализирована");
                return;
            }

            CatchDeep = catchDeep;
            Debug.WriteLine($"[WallCache] Глубина захвата установлена: {catchDeep}");

            // Инициализация геометрии
            EnsureGeometryInitialized();
            Debug.WriteLine($"[WallCache] Геометрия инициализирована");

            // Извлечение соседних элементов
            var neighborElements = ExtractNeighborElements(catchDeep);
            Debug.WriteLine($"[WallCache] Найдено вышележащих элементов: {neighborElements.UpperHostCaches.Count}, нижележащих: {neighborElements.LowerHostCaches.Count}");

            // Инициализация базовых подкэшей
            InitializeBaseSubCaches();
            Debug.WriteLine($"[WallCache] Базовые подкэши инициализированы: регионов={Regions?.Count ?? 0}, пересечений={Intersections?.Count ?? 0}, окончаний={Ends?.Count ?? 0}");

            // Разбиение регионов соседними элементами
            SplitRegionsByNeighbors(neighborElements.UpperHostCaches, Side.Top);
            SplitRegionsByNeighbors(neighborElements.LowerHostCaches, Side.Bottom);
            Debug.WriteLine($"[WallCache] После разбиения: регионов={Regions?.Count ?? 0}, пересечений={Intersections?.Count ?? 0}, окончаний={Ends?.Count ?? 0}");

            _isAnalyzedForSubCaches = true;
            Debug.WriteLine($"[WallCache] Анализ подкэшей завершён успешно");
        }

        /// <summary>
        /// Гарантирует инициализацию геометрических данных
        /// </summary>
        private void EnsureGeometryInitialized()
        {
            if (Geom.Solid == null)
                Geom.GetSolidData();
        }

        /// <summary>
        /// Данные о соседних элементах стены
        /// </summary>
        private class NeighborElements
        {
            public List<RebarHostCache> UpperHostCaches { get; }
            public List<RebarHostCache> LowerHostCaches { get; }

            public NeighborElements(List<RebarHostCache> upperHostCaches, List<RebarHostCache> lowerHostCaches)
            {
                UpperHostCaches = upperHostCaches;
                LowerHostCaches = lowerHostCaches;
            }
        }

        /// <summary>
        /// Извлекает вышележащие и нижележащие элементы
        /// </summary>
        private NeighborElements ExtractNeighborElements(double catchDeep)
        {
            Debug.WriteLine($"[WallCache] Извлечение соседних элементов (глубина={catchDeep})");

            var allUpperHostCaches = ExtractingTools.GetNearestHostCaches(
                Geom, Geom.Faces.Top, Side.Top, catchDeep);
            Debug.WriteLine($"[WallCache] Найдено вышележащих элементов: {allUpperHostCaches.Count}");

            // Кэширование вышележащих элементов по типам
            UpperSlabCaches = [.. allUpperHostCaches.OfType<SlabCache>()];
            UpperWallCaches = [.. allUpperHostCaches.OfType<WallCache>()];
            UpperColumnCaches = [.. allUpperHostCaches.OfType<ColumnCache>()];
            Debug.WriteLine($"[WallCache] Распределение по типам: плиты={UpperSlabCaches.Count}, стены={UpperWallCaches.Count}, колонны={UpperColumnCaches.Count}");

            var allLowerHostCaches = ExtractingTools.GetNearestHostCaches(
                Geom, Geom.Faces.Bottom, Side.Bottom, catchDeep);
            Debug.WriteLine($"[WallCache] Найдено нижележащих элементов: {allLowerHostCaches.Count}");

            return new NeighborElements(allUpperHostCaches, allLowerHostCaches);
        }

        /// <summary>
        /// Инициализирует базовые подкэши: пересечения, окончания, регионы
        /// </summary>
        private void InitializeBaseSubCaches()
        {
            Debug.WriteLine($"[WallCache] Инициализация базовых подкэшей");

            Intersections = GetWallIntersectionCaches();
            Debug.WriteLine($"[WallCache] Найдено пересечений: {Intersections.Count}");

            Ends = ExtractingTools.GetWallEndCaches(Geom as GeometryWallCache);
            Debug.WriteLine($"[WallCache] Найдено окончаний: {Ends.Count}");

            Regions = GetWallRegionCaches();
            Debug.WriteLine($"[WallCache] Создано регионов: {Regions.Count}");
        }

        /// <summary>
        /// Разбивает регионы на под-регионы соседними элементами указанной стороны
        /// </summary>
        /// <param name="neighborHostCaches">Кэш соседних элементов</param>
        /// <param name="side">Сторона (Top или Bottom)</param>
        private void SplitRegionsByNeighbors(List<RebarHostCache> neighborHostCaches, Side side)
        {
            Debug.WriteLine($"[WallCache] Разбиение регионов для стороны {side}");

            if (Regions == null || Regions.Count == 0)
            {
                Debug.WriteLine($"[WallCache] Пропуск: нет регионов для разбиения");
                return;
            }

            if (neighborHostCaches == null || neighborHostCaches.Count == 0)
            {
                Debug.WriteLine($"[WallCache] Пропуск: нет соседних элементов для разбиения");
                return;
            }

            int initialRegionsCount = Regions.Count;
            var newRegions = new List<WallRegionCache>();
            var newEnds = new List<WallEndCache>();
            var newIntersections = new List<WallIntersectionCache>();

            foreach (var regionCache in Regions)
            {
                var regionNeighborCaches = regionCache.GetNearestHostCaches(
                    neighborHostCaches, side, CatchDeep);
                Debug.WriteLine($"[WallCache] Регион {regionCache.Elem.Id}: найдено {regionNeighborCaches.Count} соседних элементов");

                foreach (var neighborCache in regionNeighborCaches)
                {
                    var subCaches = regionCache.SplitByAnotherHost(neighborCache);

                    if (subCaches?.Regions != null && subCaches.Regions.Count > 0)
                    {
                        Debug.WriteLine($"[WallCache] Регион разбит на {subCaches.Regions.Count} подрегионов");
                        newRegions.AddRange(subCaches.Regions);
                        newEnds.AddRange(subCaches.Ends ?? []);
                        newIntersections.AddRange(subCaches.Intersections ?? []);
                    }
                }
            }

            // Замена старых регионов новыми только если есть разбиения
            if (newRegions.Count > 0)
            {
                Regions = newRegions;
                Ends.AddRange(newEnds);
                Intersections.AddRange(newIntersections);
                Debug.WriteLine($"[WallCache] Разбиение завершено: было {initialRegionsCount} регионов, стало {Regions.Count} (+{newEnds.Count} окончаний, +{newIntersections.Count} пересечений)");
            }
            else
            {
                Debug.WriteLine($"[WallCache] Разбиение не потребовалось: количество регионов не изменилось");
            }
        }
        public void GetVoidCaches()
        {
            List<VoidCache> voidCaches = ExtractingTools.ExtractVoidCaches(Elem);
            Voids = [.. (from voidCache in voidCaches
                     where !voidCache.IsOpening
                     select voidCache)];
        }

        private List<WallRegionCache> GetWallRegionCaches()
        {
            // Инициализация
            List<WallRegionCache> regionCaches = [];
            List<XYZ> newWallRegionsPoints =
            [
                // Деление на регионы окончаниями и пересечениями
                .. GetWallRegionsPointsFromEnds(),
                .. GetWallRegionsPointsFromIntersections(),
            ];
            newWallRegionsPoints = newWallRegionsPoints.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            for (int i = 1; i < newWallRegionsPoints.Count - 1; i += 2)
            {
                WallRegionCache regionCache = new WallRegionCache(this, newWallRegionsPoints[i], newWallRegionsPoints[i + 1]);
                regionCaches.Add(regionCache);
            }

            // Деление регионов на регионы отвестиями и проёмами, прилегающими к нижней части
            List<XYZ> wallRegionsPointsFromVoids = GetWallRegionsPointsFromVoids().OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
            for (int i = 0; i < regionCaches.Count; i++)
            {
                WallRegionCache regionCache = regionCaches[i];
                List<XYZ> splitPoints = GeometryTools.GetFilteredPointsInSightOfHostCache(wallRegionsPointsFromVoids, regionCache.Geom);
                if (splitPoints.Count > 0)
                {
                    List<WallRegionCache> splittedRegionCaches = regionCache.SplitByPoints(splitPoints);
                    if (splittedRegionCaches != null)
                    {
                        regionCaches.RemoveAt(i);
                        regionCaches.InsertRange(i, splittedRegionCaches);
                    }
                }
            }

            return regionCaches;
        }
        public List<XYZ> GetWallRegionsPointsFromEnds()
        {
            List<XYZ> regionsPoints = new List<XYZ>();
            if (Ends == null) Ends = ExtractingTools.GetWallEndCaches(Geom as GeometryWallCache);
            foreach (WallEndCache end in Ends)
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
        public List<XYZ> GetWallRegionsPointsFromIntersections()
        {
            List<XYZ> regionsPoints = new List<XYZ>();
            if (Intersections == null) Intersections = GetWallIntersectionCaches();
            foreach (WallIntersectionCache intersection in Intersections)
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
        public List<XYZ> GetWallRegionsPointsFromVoids()
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

        private List<WallIntersectionCache> GetWallIntersectionCaches()
        {
            // Инициализация
            Document doc = this.Elem.Document;
            List<WallIntersectionCache> intersectionCaches = [];
            List<ElementFilter> filters =
            [
                StructureElementFilters.ColumnsOrWalls,
                new ElementLevelFilter(this.Geom.LvlIds.Bot),
            ];
            ElementFilter filter = new LogicalAndFilter(filters);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);

            // Поиск примыкающих элементов и точек пересечений
            if (this.Geom.Solid == null) this.Geom.GetSolidData();
            List<RebarHostCache> allAttachedCaches = [];
            Dictionary<XYZ, XYZ> intersectionOriginsData = new(new XYZEqualityComparer());

            foreach (PlanarFace face in this.Geom.Faces.SideLong)
            {
                List<RebarHostCache> attachedCaches = [.. face.GetAttachedElements(collector, 10 / 304.8).Select(elem => elem.GetRebarHostCache())];
                allAttachedCaches.AddRange(attachedCaches);
                Plane plane = this.Geom.UnboundFaces.Center;

                foreach (RebarHostCache attachedCache in attachedCaches)
                {
                    plane.Project(attachedCache.Geom.Origins.CenterMiddleBottom, out UV uv, out _);
                    XYZ origin = new(plane.Origin.X + uv.U, plane.Origin.Y + uv.V, this.Geom.Dims.ZBot);
                    if (!intersectionOriginsData.ContainsKey(origin)) intersectionOriginsData.Add(origin, face.FaceNormal);
                }
            }

            foreach (PlanarFace face in this.Geom.Faces.SideShort)
            {
                List<RebarHostCache> attachedCaches = [.. face.GetAttachedElements(collector, 10 / 304.8).Select(elem => elem.GetRebarHostCache())];
                allAttachedCaches.AddRange(attachedCaches);
                foreach (RebarHostCache attachedCache in attachedCaches)
                {
                    XYZ origin = face.Project(attachedCache.Geom.Origins.CenterMiddleBottom).XYZPoint;
                    if (!intersectionOriginsData.ContainsKey(origin)) intersectionOriginsData.Add(origin, face.FaceNormal);
                }
            }

            // Сбор данных по пересечниям
            foreach (XYZ origin in intersectionOriginsData.Keys)
            {
                List<XYZ> points =
                [
                    origin - intersectionOriginsData[origin].VecABS() * 10 / 304.8,
                    origin + intersectionOriginsData[origin].VecABS() * 10 / 304.8 + this.Geom.Dirs.Z * this.Geom.Dims.H
                ];
                Outline outline = new(points.First(), points.Last());
                filter = new BoundingBoxIntersectsFilter(outline);
                List<RebarHostCache> attachedRebarHostCaches = [.. allAttachedCaches.Where(c => filter.PassesFilter(c.Elem))];

                intersectionCaches.Add(new WallIntersectionCache(this, attachedRebarHostCaches, origin));
            }
            return intersectionCaches;
        }

        // Свойства
        public Wall Wall { get; set; }
        public List<CompoundStructureLayer> Layers { get; set; }
        public List<WallRegionCache> Regions { get; set; }
        public List<WallIntersectionCache> Intersections { get; set; }
        public List<WallEndCache> Ends { get; set; }
        public List<VoidCache> Voids { get; set; }
        public double CatchDeep { get; private set; }
    }
}
