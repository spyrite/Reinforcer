using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Customs;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Revit.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Grid = Autodesk.Revit.DB.Grid;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class ExtractingTools
    {
        #region Helper Methods
        
        /// <summary>
        /// Извлекает ElementId из ElementId с учётом версии Revit
        /// </summary>
#if REVIT2024 || REVIT2025
        private static int GetElementIdValue(ElementId id) => (int)id.Value;
#else
        private static int GetElementIdValue(ElementId id) => id.IntegerValue;
#endif

        /// <summary>
        /// Получает материалы элемента (HostObject или FamilyInstance)
        /// </summary>
        /// <typeparam name="T">Тип результата: Material или string</typeparam>
        /// <param name="elem">Элемент</param>
        /// <param name="selector">Функция выбора из Material</param>
        /// <returns>Список материалов</returns>
        private static List<T> GetMaterialsCore<T>(Element elem, Func<Material, T> selector)
        {
            Document doc = elem.Document;
            List<T> result = new List<T>();

            if (elem is HostObject hostObject)
            {
                HostObjAttributes elemType = doc.GetElement(elem.GetTypeId()) as HostObjAttributes;
                if ((elemType is WallType wallType && wallType.Kind != WallKind.Curtain) || elemType is FloorType)
                {
                    CompoundStructure cs = elemType.GetCompoundStructure();
                    foreach (CompoundStructureLayer layer in cs.GetLayers())
                    {
                        Material material = doc.GetElement(layer.MaterialId) as Material;
                        if (material != null) result.Add(selector(material));
                    }
                }
            }
            else if (elem is FamilyInstance familyInstance)
            {
                Parameter[] materialParameters = new Parameter[4];
                
                // Параметр "1П_Материал" из символа
                var symbolParams = familyInstance.Symbol.GetParameters("1П_Материал");
                if (symbolParams.Count > 0) materialParameters[2] = symbolParams[0];
                
                // Параметр "1П_Материал" из экземпляра
                var instanceParams = elem.GetParameters("1П_Материал");
                if (instanceParams.Count > 0) materialParameters[3] = instanceParams[0];
                
                // Стандартные параметры материала
                materialParameters[0] = familyInstance.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                materialParameters[1] = familyInstance.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);

                foreach (Parameter param in materialParameters)
                {
                    if (param != null && param.AsElementId() != ElementId.InvalidElementId)
                    {
                        Material material = doc.GetElement(param.AsElementId()) as Material;
                        if (material != null)
                        {
                            result.Add(selector(material));
                            break;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Извлекает под-элементы из AssemblyInstance или зависимые элементы
        /// </summary>
        /// <param name="doc">Документ</param>
        /// <param name="elem">Элемент</param>
        /// <param name="filter">Фильтр для зависимых элементов</param>
        /// <returns>Список ElementId под-элементов</returns>
        private static List<ElementId> ExtractSubElements(Document doc, Element elem, ElementFilter filter)
        {
            List<ElementId> result = new List<ElementId>();

            if (elem is AssemblyInstance ai)
            {
                foreach (ElementId subId in ai.GetMemberIds())
                {
                    if (filter.PassesFilter(doc, subId))
                        result.Add(subId);
                }
            }
            else
            {
                foreach (ElementId subId in elem.GetDependentElements(filter))
                {
                    result.Add(subId);
                }
            }

            return result;
        }

        /// <summary>
        /// Извлекает Rebar из AssemblyInstance или зависимых элементов с распаковкой контейнеров
        /// </summary>
        private static List<int> ExtractRebarIds(Document doc, Element elem)
        {
            List<int> rebarIds = new List<int>();

            if (elem is AssemblyInstance ai)
            {
                // Прямые арматуры
                foreach (ElementId subId in ai.GetMemberIds())
                {
                    if (RebarFilters.Rebars.PassesFilter(doc, subId))
                        rebarIds.Add(GetElementIdValue(subId));
                }

                // Контейнеры с арматурой
                foreach (ElementId containerId in ai.GetMemberIds())
                {
                    if (RebarFilters.Containers.PassesFilter(doc, containerId))
                    {
                        foreach (ElementId subId in RebarContainerAssistant.Disassemble(doc, new List<ElementId> { containerId }, false))
                            rebarIds.Add(GetElementIdValue(subId));
                    }
                }
            }
            else
            {
                // Прямые арматуры
                foreach (ElementId subId in elem.GetDependentElements(RebarFilters.Rebars))
                {
                    rebarIds.Add(GetElementIdValue(subId));
                }

                // Контейнеры с арматурой
                foreach (ElementId containerId in elem.GetDependentElements(RebarFilters.Containers))
                {
                    foreach (ElementId subId in RebarContainerAssistant.Disassemble(doc, new List<ElementId> { containerId }, false))
                        rebarIds.Add(GetElementIdValue(subId));
                }
            }

            return rebarIds;
        }

        #endregion

        #region Reinforcement caches
        public static List<RebarCache> ExtractRCs(Document doc, List<ElementId> elemIds)
        {
            List<int> allRebarIdIntegerValues = new List<int>();
            
            foreach (ElementId id in elemIds)
            {
                Element elem = doc.GetElement(id);
                
                if (elem is Rebar)
                {
                    allRebarIdIntegerValues.Add(GetElementIdValue(id));
                }
                else
                {
                    allRebarIdIntegerValues.AddRange(ExtractRebarIds(doc, elem));
                }
            }
            
            allRebarIdIntegerValues = allRebarIdIntegerValues.Distinct().ToList();
            
#if REVIT2024 || REVIT2025
            List<RebarCache> RPs = (from intId in allRebarIdIntegerValues
                                    select new RebarCache(doc.GetElement(new ElementId((long)intId)) as Rebar)).ToList();
#else
            List<RebarCache> RPs = (from intId in allRebarIdIntegerValues
                                    select new RebarCache(doc.GetElement(new ElementId(intId)) as Rebar)).ToList();
#endif
            return RPs;
        }

        public static List<RebarContainerCache> ExtractRCCs(Document doc, List<ElementId> elemIds)
        {
            ElementFilter containerFilter = new ElementClassFilter(typeof(RebarContainer));
            List<RebarContainer> allContainers = new List<RebarContainer>();

            foreach (ElementId id in elemIds)
            {
                Element elem = doc.GetElement(id);
                
                if (elem is RebarContainer container)
                {
                    allContainers.Add(container);
                }
                else
                {
                    foreach (ElementId subId in ExtractSubElements(doc, elem, containerFilter))
                    {
                        RebarContainer subContainer = doc.GetElement(subId) as RebarContainer;
                        if (subContainer != null)
                            allContainers.Add(subContainer);
                    }
                }
            }

            return allContainers.Select(c => new RebarContainerCache(c)).ToList();
        }
        #endregion

        /*public static Wall GetMainWallFromView(ViewCache vp)
        {
            foreach (Element elem in vp.Collectors.Walls)
            {
                WallCache wCache = new WallCache(elem);
                wCache.Geom.GetSolidData();
                Solid solid = wCache.Geom.Solid;
                List<FamilyInstance> openings = wCache.Geom.GetOpenings();
                foreach ( double i in new List<double> { -0.25, 0, 0.25 } )
                {
                    Transform transform = Transform.CreateTranslation(vp.Dirs.X * vp.CropBox.Dims.L * i);
                    Curve line = vp.CropBox.Lines.CentroidDepth.CreateTransformed(transform);
                    List<ElementFilter> filters = new List<ElementFilter>();
                    for (int j = 0; j < 10; j++) filters.Add(new BoundingBoxContainsPointFilter(line.Evaluate(j * 0.1, true)));
                    ElementFilter filter = new LogicalOrFilter(filters);
                    if (solid.IntersectWithCurve(line, null).ToList().Count > 0
                        || (from opening in openings where filter.PassesFilter(opening) select openings).ToList().Count > 0)
                        return wCache.Elem as Wall;
                }
            }
            return null;
        }*/

        #region Elements
        /// <summary>
        /// Рекурсивно извлекает все под-компоненты FamilyInstance
        /// </summary>
        public static List<ElementId> ExtractFamilyInstanceSubComponents(Document doc, FamilyInstance sourceInst)
        {
            List<ElementId> subComponentIds = sourceInst.GetSubComponentIds().ToList();
            
            // Итеративный обход для рекурсивного извлечения вложенных компонентов
            int i = 0;
            while (i < subComponentIds.Count)
            {
                FamilyInstance subComponent = doc.GetElement(subComponentIds[i]) as FamilyInstance;
                if (subComponent != null)
                {
                    List<ElementId> additionalSubComponentIds = subComponent.GetSubComponentIds().ToList();
                    subComponentIds.AddRange(additionalSubComponentIds);
                }
                i++;
            }
            
            return subComponentIds;
        }
        public static Dictionary<Group, CICache> ExtractCIGroupsDict(View view)
        {
            Document doc = view.Document;
            Dictionary<Group, CICache> ciGroupsDict = new Dictionary<Group, CICache>();
            List<Group> ciGroups = (from elem in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Group))
                                    where (elem as Group).GroupType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK).AsString().Contains("ГЗД")
                                    select elem as Group).ToList();

#if REVIT2024 || REVIT2025
            ElementFilter filter = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new VisibleInViewFilter(doc, view.Id));
            foreach (Group ciGroup in ciGroups)
                {
                    FamilyInstance visibleCI = (from id in ciGroup.GetMemberIds()
                                                where filter.PassesFilter(doc.GetElement(id))
                                                select doc.GetElement(id) as FamilyInstance).FirstOrDefault();
                    if (visibleCI != null) ciGroupsDict.Add(ciGroup, new CICache(visibleCI));
                }
#else
            List<ElementId> visibleInViewIds = new FilteredElementCollector(doc, view.Id).OfClass(typeof(FamilyInstance)).ToElementIds().ToList();
            foreach (Group ciGroup in ciGroups)
            {
                FamilyInstance visibleCI = (from id in ciGroup.GetMemberIds()
                                            where visibleInViewIds.Contains(id)
                                            select doc.GetElement(id) as FamilyInstance).FirstOrDefault();
                if (visibleCI != null) ciGroupsDict.Add(ciGroup, new CICache(visibleCI));
            }
#endif
            return ciGroupsDict;
        }
        #endregion

        #region Materials
        public static List<string> GetMaterialNames(Element elem)
        {
            return GetMaterialsCore(elem, m => m.Name);
        }

        public static List<Material> GetMaterials(Element elem)
        {
            return GetMaterialsCore(elem, m => m);
        }
        #endregion

        #region Caches
        
        /// <summary>
        /// Извлекает VoidCache из HostObject (Wall, Floor)
        /// </summary>
        public static List<VoidCache> ExtractVoidCaches(Element elem)
        {
            Document doc = elem.Document;
            
            if (elem is not HostObject hostObject)
                return new List<VoidCache>();
                
            return (from id in hostObject.FindInserts(false, false, false, false)
                    where doc.GetElement(id) is FamilyInstance fi && fi.Host.Id == hostObject.Id
                    select new VoidCache(fi)).ToList();
        }
        
        /// <summary>
        /// Создаёт соответствующий кэш для элемента на основе его типа
        /// </summary>
        public static RebarHostCache GetRebarHostCache(this Element elem)
        {
            if (StructureElementFilters.Walls.PassesFilter(elem))
                return new WallCache(elem as Wall);
            else if (StructureElementFilters.Columns.PassesFilter(elem))
                return new ColumnCache(elem as FamilyInstance);
            else if (StructureElementFilters.Floors.PassesFilter(elem))
                return new SlabCache(elem as Floor);
            else if (StructureElementFilters.Beams.PassesFilter(elem))
                return new BeamCache(elem as FamilyInstance);
            else
                return null;
        }
        
        /// <summary>
        /// Извлекает прилегающие элементы к грани PlanarFace
        /// </summary>
        public static List<Element> GetAttachedElements(this PlanarFace face, FilteredElementCollector collector, double dst)
        {
            Solid catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(
                face.GetEdgesAsCurveLoops(), 
                face.FaceNormal, 
                dst);
            ElementFilter filter = new ElementIntersectsSolidFilter(catchSolid);
            
            return (from elem in collector
                    where filter.PassesFilter(elem)
                    select elem).ToList();
        }
        
        /// <summary>
        /// Извлекает ближайшие кэш-объекты элементов (стены, колонны, балки, плиты)
        /// </summary>
        public static List<RebarHostCache> GetNearestHostCaches(GeometryCache geomCache, List<PlanarFace> faces, Side side, double dst)
        {
            Document doc = geomCache.Elem.Document;
            
            ElementFilter lvlFilter = side switch
            {
                Side.Bottom => new ElementLevelFilter(geomCache.LvlIds.Bot),
                Side.Top => new ElementLevelFilter(geomCache.LvlIds.Top),
                _ => null
            };
            
            if (lvlFilter == null) return new List<RebarHostCache>();
            
            ElementFilter filter = new LogicalAndFilter(
                new LogicalOrFilter(new List<ElementFilter> 
                { 
                    StructureElementFilters.ColumnsOrWalls, 
                    StructureElementFilters.Floors, 
                    StructureElementFilters.Beams 
                }),
                lvlFilter
            );
            
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            
            return (from face in faces
                    from elem in GetAttachedElements(face, collector, dst)
                    select GetRebarHostCache(elem))
                    .ToList();
        }
        
        /// <summary>
        /// Извлекает ближайшие плиты (SlabCache) указанной стороны
        /// </summary>
        public static List<SlabCache> GetNearestSlabCaches(GeometryCache geomCache, Side side)
        {
            Document doc = geomCache.Elem.Document;
            
            ElementFilter filter = side switch
            {
                Side.Bottom => new ElementLevelFilter(geomCache.LvlIds.Bot),
                Side.Top => new ElementLevelFilter(geomCache.LvlIds.Top),
                _ => null
            };
            
            if (filter == null) return new List<SlabCache>();
            
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Floor))
                .WherePasses(filter);
            
            return (from Floor elem in collector
                    let slabCache = new SlabCache(elem)
                    where CheckSlabSupport(slabCache, geomCache, side)
                    select slabCache).ToList();
        }
        
        /// <summary>
        /// Проверяет, является ли плита опорой для геометрии
        /// </summary>
        private static bool CheckSlabSupport(SlabCache slabCache, GeometryCache geomCache, Side side)
        {
            switch (side)
            {
                case Side.Bottom:
                    slabCache.GetSupportLines(geomCache, Side.Top);
                    return slabCache.SupportLines.AllTop.Count > 0;
                case Side.Top:
                    slabCache.GetSupportLines(geomCache, Side.Bottom);
                    return slabCache.SupportLines.AllBottom.Count > 0;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Извлекает ближайшие стены или колонны указанной стороны
        /// </summary>
        private static List<T> GetNearestVerticalElements<T>(GeometryCache geomCache, Side side, Func<Element, T> cacheFactory)
            where T : RebarHostCache
        {
            Document doc = geomCache.Elem.Document;
            
            if (!TryCreateTransforms(geomCache, side, out Transform transform0, out List<XYZ> points))
                return new List<T>();
            
            Transform transform1 = Transform.CreateTranslation(-geomCache.Dirs.Z * 10 / 304.8);
            Transform transform2 = Transform.CreateTranslation(geomCache.Dirs.Z * 10 / 304.8);
            
            ElementFilter filter1 = typeof(T) == typeof(WallCache) 
                ? StructureElementFilters.Walls 
                : StructureElementFilters.Columns;
                
            points = points.OrderBy(p => p, new Sorting.XYZCoordsComparer()).ToList();
            Outline outline = new Outline(transform1.OfPoint(points.First()), transform2.OfPoint(points.Last()));
            ElementFilter filter2 = new BoundingBoxIntersectsFilter(outline);
            ElementFilter filter = new LogicalAndFilter(filter1, filter2);
            
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            Line cutLine = geomCache.Lines.CenterBot.CreateTransformed(transform0) as Line;
            
            var result = new List<T>();
            foreach (Element elem in collector)
            {
                T cache = cacheFactory(elem);
                if (cache.Geom.Solid == null) cache.Geom.GetSolidData();
                
                List<Curve> spotLines = cache.Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                if (spotLines.Count == 0) continue;
                
                // Проверка направления для стен и колонн
                if (geomCache is GeometryWallCache || geomCache is GeometryColumnCache)
                {
                    if (!cache.Geom.Dirs.X.IsAlmostEqualTo(geomCache.Dirs.X) && 
                        !cache.Geom.Dirs.X.IsAlmostEqualTo(-geomCache.Dirs.X))
                        continue;
                }
                
                result.Add(cache);
            }
            
            return result;
        }
        
        /// <summary>
        /// Пытается создать трансформации для поиска вертикальных элементов
        /// </summary>
        private static bool TryCreateTransforms(GeometryCache geomCache, Side side, out Transform transform0, out List<XYZ> points)
        {
            transform0 = null;
            points = null;
            
            switch (side)
            {
                case Side.Bottom:
                    transform0 = Transform.CreateTranslation(-geomCache.Dirs.Z * 500 / 304.8);
                    points = new List<XYZ>
                    {
                        transform0.OfPoint(geomCache.Origins.CenterStartBottom),
                        transform0.OfPoint(geomCache.Origins.CenterEndBottom)
                    };
                    return true;
                    
                case Side.Top:
                    transform0 = Transform.CreateTranslation(geomCache.Dirs.Z * 500 / 304.8);
                    points = new List<XYZ>
                    {
                        transform0.OfPoint(geomCache.Origins.CenterStartTop),
                        transform0.OfPoint(geomCache.Origins.CenterEndTop)
                    };
                    return true;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Извлекает ближайшие стены (WallCache) указанной стороны
        /// </summary>
        public static List<WallCache> GetNearestWallCaches(GeometryCache geomCache, Side side)
        {
            return GetNearestVerticalElements(geomCache, side, elem => new WallCache(elem as Wall));
        }
        
        /// <summary>
        /// Извлекает ближайшие колонны (ColumnCache) указанной стороны
        /// </summary>
        public static List<ColumnCache> GetNearestColumnCaches(GeometryCache geomCache, Side side)
        {
            return GetNearestVerticalElements(geomCache, side, elem => new ColumnCache(elem));
        }
        
        /// <summary>
        /// Извлекает кэши окончаний стены (WallEndCache)
        /// </summary>
        public static List<WallEndCache> GetWallEndCaches(WallCache wCache)
        {
            Document doc = wCache.Elem.Document;
            List<WallEndCache> endCaches = new List<WallEndCache> { null, null };
            
            ElementFilter filter = new LogicalAndFilter(
                StructureElementFilters.ColumnsOrWalls,
                new ElementLevelFilter(wCache.Geom.LvlIds.Bot)
            );
            
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            List<XYZ> endPoints = new List<XYZ>
            {
                wCache.Geom.Origins.CenterStartBottom,
                wCache.Geom.Origins.CenterEndBottom
            };
            List<int> tokens = new List<int> { 1, -1 };
            
            for (int i = 0; i < 2; i++)
            {
                if (HasAdjacentElements(collector, wCache, endPoints[i], tokens[i]))
                {
                    XYZ origin = endPoints[i];
                    XYZ xDir = wCache.Geom.Dirs.X * tokens[i];
                    endCaches[i] = new WallEndCache(wCache, origin, xDir);
                }
            }
            
            return endCaches;
        }
        
        /// <summary>
        /// Проверяет наличие прилегающих элементов к окончанию стены
        /// </summary>
        private static bool HasAdjacentElements(FilteredElementCollector collector, WallCache wCache, XYZ endPoint, int token)
        {
            double offset = 10 / 304.8;
            double halfThickness = wCache.Geom.Dims.T / 2 + offset;
            
            var checkPoints = new List<XYZ>
            {
                endPoint + wCache.Geom.Dirs.X * offset * token - wCache.Geom.Dirs.Y * halfThickness + wCache.Geom.Dirs.Z * offset,
                endPoint + wCache.Geom.Dirs.X * offset * token + wCache.Geom.Dirs.Y * halfThickness + wCache.Geom.Dirs.Z * offset,
                endPoint - wCache.Geom.Dirs.X * offset * token - wCache.Geom.Dirs.Y * halfThickness + wCache.Geom.Dirs.Z * offset,
                endPoint - wCache.Geom.Dirs.X * offset * token + wCache.Geom.Dirs.Y * halfThickness + wCache.Geom.Dirs.Z * offset
            };
            
            ElementFilter filter1 = new LogicalOrFilter(
                new BoundingBoxContainsPointFilter(checkPoints[0]),
                new BoundingBoxContainsPointFilter(checkPoints[1])
            );
            ElementFilter filter2 = new LogicalOrFilter(
                new BoundingBoxContainsPointFilter(checkPoints[2]),
                new BoundingBoxContainsPointFilter(checkPoints[3])
            );
            
            return (from elem in collector
                    where filter1.PassesFilter(elem) || filter2.PassesFilter(elem)
                    select elem).Any();
        }
        
        /// <summary>
        /// Находит одинаковые верхние проёмы (VoidCache) для указанного проёма
        /// </summary>
        public static List<VoidCache> GetEqualUpperVoids(VoidCache voidCache)
        {
            List<VoidCache> equalUpperVoidAtEnds = new List<VoidCache>() { null, null };
            List<XYZ> checkPoints1 = new List<XYZ>
            {
                voidCache.Geom.Origins.CenterStartBottom,
                voidCache.Geom.Origins.CenterEndBottom
            };
            
            List<WallCache> upperWallCaches = GetNearestHostCaches(
                voidCache.HostCache.Geom, 
                voidCache.HostCache.Geom.Faces.Top, 
                Side.Top, 
                500 / 304.8
            ).OfType<WallCache>().ToList();
            
            foreach (WallCache upperWallCache in upperWallCaches)
            {
                List<VoidCache> upperVoidCaches = ExtractVoidCaches(upperWallCache.Elem);
                
                foreach (VoidCache upperVoidCache in upperVoidCaches)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        List<XYZ> checkPoints2 = new List<XYZ>
                        {
                            upperVoidCache.Geom.Origins.CenterStartBottom,
                            upperVoidCache.Geom.Origins.CenterEndBottom
                        };
                        
                        Line checkLine = Line.CreateBound(checkPoints1[i], checkPoints2[i]);
                        if (GeometryTools.VecABS(checkLine.Direction).IsAlmostEqualTo(GeometryTools.VecABS(voidCache.Geom.Dirs.Z)))
                            equalUpperVoidAtEnds[i] = upperVoidCache;
                    }
                    
                    if (!equalUpperVoidAtEnds.Contains(null)) break;
                }
            }
            
            return equalUpperVoidAtEnds;
        }
        #endregion

        #region Extractors
        public class ConcreteExtractor
        {
            private Document doc;
            private static List<Element> elems;
            private static List<ElementId> elemIds;
            private static FilteredElementCollector collector;
            private static ElementFilter defaultFilter;

            public ConcreteExtractor(Document doc)
            {
                Initialize(doc, true);
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }
            public ConcreteExtractor(Document doc, ElementId viewId)
            {
                Initialize(doc, false);
                collector = new FilteredElementCollector(doc, viewId).WherePasses(defaultFilter).WhereElementIsNotElementType();
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }
            public ConcreteExtractor(Document doc, List<ElementId> elemSetIds)
            {
                Initialize(doc, false);
                collector = new FilteredElementCollector(doc, elemSetIds).WherePasses(defaultFilter).WhereElementIsNotElementType();
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }
            public ConcreteExtractor(Document doc, AssemblyInstance ai)
            {
                Initialize(doc, false);
                collector = new FilteredElementCollector(doc, ai.GetMemberIds()).WherePasses(defaultFilter).WhereElementIsNotElementType();
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }
            public ConcreteExtractor(Document doc, Solid solid)
            {
                Initialize(doc, false);
                defaultFilter = new ElementIntersectsSolidFilter(solid);
                collector = new FilteredElementCollector(doc).WherePasses(defaultFilter);
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }
            public ConcreteExtractor(Document doc, Solid solid, ElementFilter filter)
            {
                Initialize(doc, false);
                defaultFilter = new LogicalAndFilter(filter, new ElementIntersectsSolidFilter(solid));
                collector = new FilteredElementCollector(doc).WherePasses(defaultFilter);
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }
            public ConcreteExtractor(Document doc, ElementFilter filter)
            {
                Initialize(doc, false);
                collector = new FilteredElementCollector(doc).WherePasses(filter).WhereElementIsNotElementType();
                foreach (Element elem in collector) CheckAndAddConcrete(elem);
            }

            private void Initialize(Document doc, bool setDefaultCollector)
            {
                this.doc = doc;
                defaultFilter = StructureElementFilters.Union;
                elems = new List<Element>();
                elemIds = new List<ElementId>();
                if (setDefaultCollector) collector = new FilteredElementCollector(doc).WherePasses(defaultFilter).WhereElementIsNotElementType();
            }

            private void CheckAndAddConcrete(Element elem)
            {
                List<string> materialNames = GetMaterialNames(elem);
                foreach (string materialName in materialNames)
                {
                    if (materialName.StartsWith("Бетон"))
                    {
                        elems.Add(elem);
                        elemIds.Add(elem.Id);
                        break;
                    }
                }
            }

            public static bool IsConcrete(Element elem)
            {
                List<string> materialNames = GetMaterialNames(elem);
                foreach (string materialName in materialNames)
                    if (materialName.StartsWith("Бетон")) return true;
                return false;
            }

            public List<Element> ToElements() { return elems; }
            public List<ElementId> ToElementIds() { return elemIds; }
            public List<ElementId> ToFloorIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.Floors).ToElementIds().ToList(); }
            public List<ElementId> ToColumnIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.Columns).ToElementIds().ToList(); }
            public List<ElementId> ToWallIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.Walls).ToElementIds().ToList(); }
            public List<ElementId> ToBeamIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.Beams).ToElementIds().ToList(); }
            public List<ElementId> ToStairIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.Stairs).ToElementIds().ToList(); }
            public List<ElementId> ToFoundationIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.Foundations).ToElementIds().ToList(); }
            public List<ElementId> ToGMIds() { return new FilteredElementCollector(doc, elemIds).WherePasses(StructureElementFilters.GMs).ToElementIds().ToList(); }
        }
        public class ConcreteInsertExtractor
        {
            private static List<FamilyInstance> cis;
            private static List<ElementId> ciIds;
            private static FilteredElementCollector collector;
            private static readonly ElementFilter instanceFilter = new LogicalAndFilter(
                new ElementClassFilter(typeof(FamilyInstance)),
                new LogicalOrFilter(new ElementCategoryFilter(BuiltInCategory.OST_GenericModel),
                                    new ElementCategoryFilter(BuiltInCategory.OST_StructConnections)));

            public ConcreteInsertExtractor(Document doc)
            {
                Initialize(doc, true);
                foreach (Element elem in collector) CheckAndAddConcreteInsert(elem);
            }
            public ConcreteInsertExtractor(Document doc, Element host)
            {
                Initialize(doc, true);
                foreach (Element elem in collector)
                {
                    if ((elem as FamilyInstance).Host != null
                        && (elem as FamilyInstance).Host.Id == host.Id)
                        CheckAndAddConcreteInsert(elem);
                }
            }
            public ConcreteInsertExtractor(Document doc, ElementId viewId)
            {
                Initialize(doc, false);
                collector = new FilteredElementCollector(doc, viewId).WherePasses(instanceFilter);
                foreach (Element elem in collector) CheckAndAddConcreteInsert(elem);
            }
            public ConcreteInsertExtractor(Document doc, AssemblyInstance ai)
            {
                Initialize(doc, false);
                collector = new FilteredElementCollector(doc, ai.GetMemberIds()).WherePasses(instanceFilter);
                foreach (Element elem in collector) CheckAndAddConcreteInsert(elem);
            }

            private static void Initialize(Document doc, bool setDefaultCollector)
            {
                cis = new List<FamilyInstance>();
                ciIds = new List<ElementId>();
                if (setDefaultCollector) collector = new FilteredElementCollector(doc).WherePasses(instanceFilter);
            }
            public static bool CheckAndAddConcreteInsert(Element elem)
            {
                FamilyInstance inst = elem as FamilyInstance;
                if (IsConcreteInsert(inst))
                {
                    cis.Add(inst);
                    ciIds.Add(inst.Id);
                    return true;
                }
                else return false;
            }

            public static bool IsConcreteInsert(Element elem)
            {
                if (elem is FamilyInstance)
                {
                    FamilyInstance inst = elem as FamilyInstance;
                    bool condition1 = inst.Symbol.FamilyName.StartsWith("220_");
                    bool condition2 = inst.Symbol.FamilyName.StartsWith("261_Монтажная петля");
                    if (condition1 || condition2) return true;
                }
                return false;
            }

            public List<FamilyInstance> ToElements() { return cis; }
            public List<ElementId> ToElementIds() { return ciIds; }
        }
        public class VoidExtractor : Extractor
        {
            public VoidExtractor(Document doc) : base(doc)
            {
                SetDefaultFilter(VoidFilters.Union);
                elems = collector.ToElements().ToList();
                elemIds = collector.ToElementIds().ToList();
            }
            public VoidExtractor(Document doc, ElementId viewId) : base(doc, viewId)
            {
                SetDefaultFilter(VoidFilters.Union);
                elems = collector.ToElements().ToList();
                elemIds = collector.ToElementIds().ToList();
            }
            public VoidExtractor(Document doc, List<ElementId> elemSetIds) : base(doc, elemSetIds)
            {
                SetDefaultFilter(VoidFilters.Union);
                elems = collector.ToElements().ToList();
                elemIds = collector.ToElementIds().ToList();
            }
            public VoidExtractor(Document doc, AssemblyInstance ai) : base(doc, ai)
            {
                SetDefaultFilter(VoidFilters.Union);
                elems = collector.ToElements().ToList();
                elemIds = collector.ToElementIds().ToList();
            }
            public VoidExtractor(Wall wall)
            {
                doc = wall.Document;
                SetDefaultFilter(VoidFilters.Union);
                elemIds = wall.FindInserts(false, false, false, false).ToList();
                elems = (from id in elemIds
                         select doc.GetElement(id)).ToList();
            }
        }
        public class ReinforcementExtractor
        {

        }
        public class PartitionExtractor : Extractor
        {
            public PartitionExtractor(Document doc, Solid solid) : base(doc, solid)
            {
                SetDefaultFilter(StructureElementFilters.Walls);
                foreach (Element elem in collector) CheckAndAddPartition(elem);
            }
            public PartitionExtractor(Document doc, List<ElementId> elemSetIds, Solid solid) : base(doc, elemSetIds, solid)
            {
                SetDefaultFilter(StructureElementFilters.Walls);
                foreach (Element elem in collector) CheckAndAddPartition(elem);
            }

            private void CheckAndAddPartition(Element elem)
            {
                List<string> materialNames = GetMaterialNames(elem);
                foreach (string materialName in materialNames)
                {
                    if (materialName.StartsWith("Кирпич") || materialName.StartsWith("Газобетон") || materialName.StartsWith("Керамзит") || materialName.StartsWith("Пазогребнеевые плиты"))
                    {
                        elems.Add(elem);
                        elemIds.Add(elem.Id);
                        break;
                    }
                }
            }
        }
        public class SteelExtractor
        {
            private static Document doc;
            private static List<Element> elems;
            private static FilteredElementCollector collector;

            public static bool IsSteel(Element elem)
            {
                List<string> materialNames = GetMaterialNames(elem);
                foreach (string materialName in materialNames)
                    if (materialName.StartsWith("С245")) return true;
                return false;
            }

            public static List<Element> GetSteelConnections(Element elem)
            {
                doc = elem.Document;
                ElementFilter filter = new ElementIntersectsElementFilter(elem);
                collector = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_GenericModel)
                    .WherePasses(filter);
                elems = (from inst in collector.Cast<FamilyInstance>()
                         where inst.Symbol.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM).AsString().StartsWith("30")
                         select inst as Element).ToList();
                return elems;
            }
        }
        #endregion
    }
}
