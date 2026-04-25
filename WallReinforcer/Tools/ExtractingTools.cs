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
        #region Reinforcement caches
        public static List<RebarCache> ExtractRCs(Document doc, List<ElementId> elemIds)
        {
            List<int> allRebarIdIntegerValues = new List<int>();
            foreach (ElementId id in elemIds)
            {
                Element elem = doc.GetElement(id);
#if REVIT2024 || REVIT2025
                if (elem is Rebar) allRebarIdIntegerValues.Add((int)id.Value);
#else
                if (elem is Rebar) allRebarIdIntegerValues.Add(id.IntegerValue);
#endif
                else if (elem is AssemblyInstance)
                {
#if REVIT2024 || REVIT2025
                    List<int> subRebarIdIntegerValues = (from subId in (elem as AssemblyInstance).GetMemberIds()
                                                         where RebarFilters.Rebars.PassesFilter(doc, subId)
                                                         select (int)subId.Value).ToList();
#else
                    List<int> subRebarIdIntegerValues = (from subId in (elem as AssemblyInstance).GetMemberIds()
                                                         where RebarFilters.Rebars.PassesFilter(doc, subId)
                                                         select subId.IntegerValue).ToList();
#endif
                    allRebarIdIntegerValues.AddRange(subRebarIdIntegerValues);

                    List<ElementId> subRebarContainerIds = (from subId in (elem as AssemblyInstance).GetMemberIds()
                                                            where RebarFilters.Containers.PassesFilter(doc, subId)
                                                            select subId).ToList();
                    foreach (ElementId containerId in subRebarContainerIds)
#if REVIT2024 || REVIT2025
                    allRebarIdIntegerValues.AddRange(from subId in RebarContainerAssistant.Disassemble(doc, new List<ElementId> { containerId }, false)
                                                         select (int)subId.Value);    
#else
                        allRebarIdIntegerValues.AddRange(from subId in RebarContainerAssistant.Disassemble(doc, new List<ElementId> { containerId }, false)
                                                         select subId.IntegerValue);
#endif
                }
                else
                {
#if REVIT2024 || REVIT2025
                    List<int> subRebarIdIntegerValues = (from subId in elem.GetDependentElements(RebarFilters.Rebars)
                                                         select (int)subId.Value).ToList();
#else
                    List<int> subRebarIdIntegerValues = (from subId in elem.GetDependentElements(RebarFilters.Rebars)
                                                         select subId.IntegerValue).ToList();
#endif
                    allRebarIdIntegerValues.AddRange(subRebarIdIntegerValues);

                    List<ElementId> subRebarContainerIds = (from subId in elem.GetDependentElements(RebarFilters.Containers)
                                                            select subId).ToList();
                    foreach (ElementId containerId in subRebarContainerIds)
#if REVIT2024 || REVIT2025
                    allRebarIdIntegerValues.AddRange(from subId in RebarContainerAssistant.Disassemble(doc, new List<ElementId> { containerId }, false)
                                                         select (int)subId.Value);
#else
                        allRebarIdIntegerValues.AddRange(from subId in RebarContainerAssistant.Disassemble(doc, new List<ElementId> { containerId }, false)
                                                         select subId.IntegerValue);
#endif
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
            List<RebarContainer> allContainers = new List<RebarContainer>();
            ElementFilter containerFilter = new ElementClassFilter(typeof(RebarContainer));
            foreach (ElementId id in elemIds)
            {
                Element elem = doc.GetElement(id);
                if (elem is RebarContainer) allContainers.Add(elem as RebarContainer);
                else if (elem is AssemblyInstance)
                {
                    AssemblyInstance ai = elem as AssemblyInstance;
                    List<Element> subElems = new List<Element>();
                    foreach (ElementId subId in ai.GetMemberIds()) { subElems.Add(doc.GetElement(subId)); }
                    ;
                    List<RebarContainer> subContainers = new List<RebarContainer>();
                    foreach (Element subElem in subElems) { subContainers.Add(subElem as RebarContainer); }
                    ;
                    allContainers.AddRange(subContainers);
                }
                else
                {
                    List<RebarContainer> subContainers = new List<RebarContainer>();
                    foreach (ElementId subId in elem.GetDependentElements(containerFilter)) { subContainers.Add(doc.GetElement(subId) as RebarContainer); }
                    ;
                    allContainers.AddRange(subContainers);
                }
            }

            List<RebarContainerCache> RCPs = new List<RebarContainerCache>();
            foreach (RebarContainer container in allContainers) { RCPs.Add(new RebarContainerCache(container)); }
            return RCPs;
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
        public static List<ElementId> ExtractFamilyInstanceSubComponents(Document doc, FamilyInstance sourceInst)
        {
            /*List<ElementId> primarySubComponentIds = sourceInst.GetSubComponentIds().ToList();
            List<ElementId> subComponentIds = primarySubComponentIds;
            int beforeCount = subComponentIds.Count;
            int afterCount = subComponentIds.Count;
            foreach (ElementId subId in primarySubComponentIds)
            {
                while (beforeCount < afterCount)
                {

                }
            }*/

            List<ElementId> subComponentIds = sourceInst.GetSubComponentIds().ToList();
            int i = 0;
            while (i < subComponentIds.Count)
            {
                FamilyInstance subComponent = doc.GetElement(subComponentIds[i]) as FamilyInstance;
                List<ElementId> additionalSubComponentIds = subComponent.GetSubComponentIds().ToList();
                subComponentIds.AddRange(additionalSubComponentIds);
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
            Document doc = elem.Document;
            List<string> materialNames = new List<string>();
            if (elem is HostObject)
            {
                HostObjAttributes elemType = doc.GetElement(elem.GetTypeId()) as HostObjAttributes;
                if (elemType is WallType && (elemType as WallType).Kind != WallKind.Curtain || elemType is FloorType)
                {
                    CompoundStructure cs = elemType.GetCompoundStructure();
                    foreach (CompoundStructureLayer layer in cs.GetLayers())
                    {
                        Material material = doc.GetElement(layer.MaterialId) as Material;
                        materialNames.Add(material.Name);
                    }
                }
            }
            else if (elem is FamilyInstance)
            {
                Parameter materialParameter3 = null;
                if ((elem as FamilyInstance).Symbol.GetParameters("1П_Материал").Count > 0) materialParameter3 = (elem as FamilyInstance).Symbol.GetParameters("1П_Материал")[0];
                Parameter materialParameter4 = null;
                if (elem.GetParameters("1П_Материал").Count > 0) materialParameter4 = elem.GetParameters("1П_Материал")[0];
                List<Parameter> materialParameters = new List<Parameter>()
                    {
                        (elem as FamilyInstance).Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM),
                        (elem as FamilyInstance).get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM),
                        materialParameter3,
                        materialParameter4
                    };
                foreach (Parameter materialParameter in materialParameters)
                {
                    if (materialParameter != null && materialParameter.AsElementId() != ElementId.InvalidElementId)
                    {
                        ElementId materialId = materialParameter.AsElementId();
                        Material material = doc.GetElement(materialId) as Material;
                        materialNames.Add(material.Name);
                        break;
                    }
                }
            }
            return materialNames;
        }
        public static List<Material> GetMaterials(Element elem)
        {
            Document doc = elem.Document;
            List<Material> materials = new List<Material>();
            if (elem is HostObject)
            {
                HostObjAttributes elemType = doc.GetElement(elem.GetTypeId()) as HostObjAttributes;
                if (elemType is WallType && (elemType as WallType).Kind != WallKind.Curtain || elemType is FloorType)
                {
                    CompoundStructure cs = elemType.GetCompoundStructure();
                    foreach (CompoundStructureLayer layer in cs.GetLayers())
                    {
                        Material material = doc.GetElement(layer.MaterialId) as Material;
                        materials.Add(material);
                    }
                }
            }
            else if (elem is FamilyInstance)
            {
                Parameter materialParameter3 = null;
                if ((elem as FamilyInstance).Symbol.GetParameters("1П_Материал").Count > 0) materialParameter3 = (elem as FamilyInstance).Symbol.GetParameters("1П_Материал")[0];
                Parameter materialParameter4 = null;
                if (elem.GetParameters("1П_Материал").Count > 0) materialParameter4 = elem.GetParameters("1П_Материал")[0];
                List<Parameter> materialParameters = new List<Parameter>()
                    {
                        (elem as FamilyInstance).Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM),
                        (elem as FamilyInstance).get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM),
                        materialParameter3,
                        materialParameter4
                    };
                foreach (Parameter materialParameter in materialParameters)
                {
                    if (materialParameter != null && materialParameter.AsElementId() != ElementId.InvalidElementId)
                    {
                        ElementId materialId = materialParameter.AsElementId();
                        Material material = doc.GetElement(materialId) as Material;
                        materials.Add(material);
                        break;
                    }
                }
            }
            return materials;
        }
        #endregion

        #region Caches
        public static List<VoidCache> ExtractVoidCaches(Element elem)
        {
            Document doc = elem.Document;
            List<VoidCache> voidCaches = new List<VoidCache>();
            switch (elem.GetType().Name)
            {
                case "Wall":
                    Wall wall = elem as Wall;
                    voidCaches = (from id in wall.FindInserts(false, false, false, false)
                                  where doc.GetElement(id) is FamilyInstance
                                  && (doc.GetElement(id) as FamilyInstance).Host.Id == wall.Id
                                  select new VoidCache(doc.GetElement(id) as FamilyInstance)).ToList();
                    break;
                case "Floor":
                    Floor slab = elem as Floor;
                    voidCaches = (from id in slab.FindInserts(false, false, false, false)
                                  where doc.GetElement(id) is FamilyInstance
                                  && (doc.GetElement(id) as FamilyInstance).Host.Id == slab.Id
                                  select new VoidCache(doc.GetElement(id) as FamilyInstance)).ToList();
                    break;
            }
            return voidCaches;
        }
        public static List<GridCache> GetNearestGridCaches(GridCache sourceGC)
        {
            Document doc = sourceGC.Grid.Document;
            List<GridCache> nearGPs = new List<GridCache>() { null, null };
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Grid));

            List<GridCache> otherGCs = (from elem in collector.Excluding(new List<ElementId> { sourceGC.Grid.Id })
                                        where ((elem as Grid).Curve as Line).Direction.IsAlmostEqualTo(sourceGC.Dirs.X)
                                        || ((elem as Grid).Curve as Line).Direction.IsAlmostEqualTo(-sourceGC.Dirs.X)
                                        select new GridCache(elem as Grid)).ToList();

            List<Plane> planes = (from gp in otherGCs select gp.CenterPlane).ToList();
            List<XYZ> points = new List<XYZ>();
            foreach (Plane plane in planes)
            {
                plane.Project(sourceGC.Centers[0], out UV uv, out double dst);
                points.Add(plane.Origin + plane.XVec * uv.U + plane.YVec * uv.V);
            }
            List<XYZ> dirs = new List<XYZ> { -sourceGC.Dirs.Y, sourceGC.Dirs.Y };
            List<Line> rays = (from dir in dirs select Line.CreateBound(sourceGC.Centers[0], sourceGC.Centers[0] + 100000 * dir)).ToList();

            for (int i = 0; i < 2; i++)
            {
                List<double> projectPars = (from point in points select Math.Round(rays[i].Project(point).Parameter, 5)).ToList();
                List<double> dsts = (from dst in projectPars where dst > 0 select dst).ToList();
                if (dsts.Count > 0)
                {
                    nearGPs[i] = otherGCs[projectPars.IndexOf(dsts.Min())];
                    nearGPs[i].RayWhereGridHasBeenCatchedDir = rays[i].Direction.Normalize();
                }
            }
            return nearGPs;
        }
        public static List<RebarHostCache> GetNearestHostCaches(GeometryCache geomCache, List<PlanarFace> faces, Side side, double dst)
        {
            Document doc = geomCache.Elem.Document;
            List<RebarHostCache> hostCaches = new List<RebarHostCache>();
            ElementFilter lvlFilter = null;
            switch (side)
            {
                case Side.Bottom: lvlFilter = new ElementLevelFilter(geomCache.LvlIds.Bot); break;
                case Side.Top: lvlFilter = new ElementLevelFilter(geomCache.LvlIds.Top); break;
            }
            List<ElementFilter> filters = new List<ElementFilter>
            {
                new LogicalOrFilter(new List<ElementFilter> { StructureElementFilters.ColumnsOrWalls, StructureElementFilters.Floors, StructureElementFilters.Beams }),
                lvlFilter,
            };
            ElementFilter filter = new LogicalAndFilter(filters);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);

            foreach (PlanarFace face in faces)
                hostCaches.AddRange(from elem in GetAttachedElements(face, collector, dst)
                                    select GetRebarHostCache(elem));
            return hostCaches;
        }
        public static List<SlabCache> GetNearestSlabCaches(GeometryCache geomCache, Side side)
        {
            Document doc = geomCache.Elem.Document;
            List<SlabCache> slabCaches = new List<SlabCache>();
            ElementFilter filter;
            switch (side)
            {
                case Side.Bottom: filter = new ElementLevelFilter(geomCache.LvlIds.Bot); break;
                case Side.Top: filter = new ElementLevelFilter(geomCache.LvlIds.Top); break;
                default: return slabCaches;
            }
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Floor)).WherePasses(filter);
            foreach (Element elem in collector)
            {
                SlabCache slabCache = new SlabCache(elem as Floor);
                switch (side)
                {
                    case Side.Bottom:
                        slabCache.GetSupportLines(geomCache, Side.Top);
                        if (slabCache.SupportLines.AllTop.Count > 0) slabCaches.Add(slabCache);
                        break;
                    case Side.Top:
                        slabCache.GetSupportLines(geomCache, Side.Bottom);
                        if (slabCache.SupportLines.AllBottom.Count > 0) slabCaches.Add(slabCache);
                        break;
                }
            }
            return slabCaches;
        }
        public static List<WallCache> GetNearestWallCaches(GeometryCache geomCache, Side side)
        {
            Document doc = geomCache.Elem.Document;
            List<WallCache> wallCaches = new List<WallCache>();
            Transform transform0;
            List<XYZ> points;

            switch (side)
            {
                case Side.Bottom:
                    transform0 = Transform.CreateTranslation(-geomCache.Dirs.Z * 500 / 304.8);
                    points = new List<XYZ>
                    {
                        transform0.OfPoint(geomCache.Origins.CenterStartBottom),
                        transform0.OfPoint(geomCache.Origins.CenterEndBottom)
                    };
                    break;
                case Side.Top:
                    transform0 = Transform.CreateTranslation(geomCache.Dirs.Z * 500 / 304.8);
                    points = new List<XYZ>
                    {
                        transform0.OfPoint(geomCache.Origins.CenterStartTop),
                        transform0.OfPoint(geomCache.Origins.CenterEndTop)
                    };
                    break;
                default: return wallCaches;
            }

            Transform transform1 = Transform.CreateTranslation(-geomCache.Dirs.Z * 10 / 304.8);
            Transform transform2 = Transform.CreateTranslation(geomCache.Dirs.Z * 10 / 304.8);

            ElementFilter filter1 = StructureElementFilters.Walls;
            points = points.OrderBy(p => p, new Sorting.XYZCoordsComparer()).ToList();
            Outline outline = new Outline(transform1.OfPoint(points.First()), transform2.OfPoint(points.Last()));
            ElementFilter filter2 = new BoundingBoxIntersectsFilter(outline);
            ElementFilter filter = new LogicalAndFilter(filter1, filter2);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            Line cutLine = geomCache.Lines.CenterBot.CreateTransformed(transform0) as Line;

            foreach (Element elem in collector)
            {
                WallCache wallCache = new WallCache(elem as Wall);
                if (wallCache.Geom.Solid == null) wallCache.Geom.GetSolidData();
                List<Curve> spotLines = wallCache.Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                if (spotLines.Count > 0)
                {
                    if (geomCache is GeometryWallCache || geomCache is GeometryColumnCache)
                    {
                        if (wallCache.Geom.Dirs.X.IsAlmostEqualTo(geomCache.Dirs.X) || wallCache.Geom.Dirs.X.IsAlmostEqualTo(-geomCache.Dirs.X))
                            wallCaches.Add(wallCache);
                    }
                    else wallCaches.Add(wallCache);
                }
            }
            return wallCaches;
        }
        public static List<ColumnCache> GetNearestColumnCaches(GeometryCache geomCache, Side side)
        {
            Document doc = geomCache.Elem.Document;
            List<ColumnCache> colCaches = new List<ColumnCache>();
            Transform transform0;
            List<XYZ> points;

            switch (side)
            {
                case Side.Bottom:
                    transform0 = Transform.CreateTranslation(-geomCache.Dirs.Z * 500 / 304.8);
                    points = new List<XYZ>
                    {
                        transform0.OfPoint(geomCache.Origins.CenterStartBottom),
                        transform0.OfPoint(geomCache.Origins.CenterEndBottom)
                    };
                    break;
                case Side.Top:
                    transform0 = Transform.CreateTranslation(geomCache.Dirs.Z * 500 / 304.8);
                    points = new List<XYZ>
                    {
                        transform0.OfPoint(geomCache.Origins.CenterStartTop),
                        transform0.OfPoint(geomCache.Origins.CenterEndTop)
                    };
                    break;
                default: return colCaches;
            }

            Transform transform1 = Transform.CreateTranslation(-geomCache.Dirs.Z * 10 / 304.8);
            Transform transform2 = Transform.CreateTranslation(geomCache.Dirs.Z * 10 / 304.8);

            ElementFilter filter1 = StructureElementFilters.Columns;
            points = points.OrderBy(p => p, new Sorting.XYZCoordsComparer()).ToList();
            Outline outline = new Outline(transform1.OfPoint(points.First()), transform2.OfPoint(points.Last()));
            ElementFilter filter2 = new BoundingBoxIntersectsFilter(outline);
            ElementFilter filter = new LogicalAndFilter(filter1, filter2);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            Line cutLine = geomCache.Lines.CenterBot.CreateTransformed(transform0) as Line;

            foreach (Element elem in collector)
            {
                ColumnCache colCache = new ColumnCache(elem);
                if (colCache.Geom.Solid == null) colCache.Geom.GetSolidData();
                List<Curve> spotLines = colCache.Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                if (spotLines.Count > 0)
                {
                    if (geomCache is GeometryWallCache || geomCache is GeometryColumnCache)
                    {
                        if (colCache.Geom.Dirs.X.IsAlmostEqualTo(geomCache.Dirs.X) || colCache.Geom.Dirs.X.IsAlmostEqualTo(-geomCache.Dirs.X))
                            colCaches.Add(colCache);
                    }
                    else colCaches.Add(colCache);
                }
            }
            return colCaches;
        }
        
        public static List<WallEndCache> GetWallEndCaches(GeometryWallCache geomWallCache)
        {
            Document doc = geomWallCache.Elem.Document;
            List<WallEndCache> endCaches = new List<WallEndCache> { null, null };
            List<ElementFilter> filters = new List<ElementFilter>
            {
                StructureElementFilters.ColumnsOrWalls,
                new ElementLevelFilter(geomWallCache.LvlIds.Bot),
            };
            ElementFilter filter = new LogicalAndFilter(filters);
            FilteredElementCollector collector = new FilteredElementCollector(doc).WherePasses(filter);
            List<XYZ> endPoints = new List<XYZ>
            {
                geomWallCache.Origins.CenterStartBottom,
                geomWallCache.Origins.CenterEndBottom
            };
            List<int> tokens = new List<int> { 1, -1 };
            for (int i = 0; i < 2; i++)
            {
                filters = new List<ElementFilter>
                {
                    new BoundingBoxContainsPointFilter(endPoints[i] + geomWallCache.Dirs.X * 10 / 304.8 * tokens[i] - geomWallCache.Dirs.Y * (geomWallCache.Dims.T / 2 + 10 / 304.8) + geomWallCache.Dirs.Z * 10 / 304.8),
                    new BoundingBoxContainsPointFilter(endPoints[i] + geomWallCache.Dirs.X * 10 / 304.8 * tokens[i] + geomWallCache.Dirs.Y * (geomWallCache.Dims.T / 2 + 10 / 304.8) + geomWallCache.Dirs.Z * 10 / 304.8),
                    new BoundingBoxContainsPointFilter(endPoints[i] - geomWallCache.Dirs.X * 10 / 304.8 * tokens[i] - geomWallCache.Dirs.Y * (geomWallCache.Dims.T / 2 + 10 / 304.8) + geomWallCache.Dirs.Z * 10 / 304.8),
                    new BoundingBoxContainsPointFilter(endPoints[i] - geomWallCache.Dirs.X * 10 / 304.8 * tokens[i] + geomWallCache.Dirs.Y * (geomWallCache.Dims.T / 2 + 10 / 304.8) + geomWallCache.Dirs.Z * 10 / 304.8)
                };
                ElementFilter filter1 = new LogicalOrFilter(filters[0], filters[1]);
                ElementFilter filter2 = new LogicalOrFilter(filters[2], filters[3]);

                List<Element> attachedElements = (from elem in collector
                                                  where filter1.PassesFilter(elem)
                                                  || filter2.PassesFilter(elem)
                                                  select elem).ToList();
                if (attachedElements.Count > 0)
                {
                    XYZ origin = endPoints[i];
                    XYZ xDir = geomWallCache.Dirs.X * tokens[i];
                    WallEndCache endCache = new WallEndCache(geomWallCache, origin, xDir);
                }
            }
            ;
            return endCaches;
        }
        public static List<VoidCache> GetEqualUpperVoids(VoidCache voidCache)
        {
            List<VoidCache> equalUpperVoidAtEnds = new List<VoidCache>() { null, null };
            List<XYZ> checkPoints1 = new List<XYZ>
            {
                voidCache.Geom.Origins.CenterStartBottom,
                voidCache.Geom.Origins.CenterEndBottom
            };
            List<WallCache> upperWallCaches = GetNearestHostCaches(voidCache.HostCache.Geom, voidCache.HostCache.Geom.Faces.Top, Side.Top, 500 / 304.8).OfType<WallCache>().ToList();
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

        public static List<Element> GetAttachedElements(this PlanarFace face, FilteredElementCollector collector, double dst)
        {
            Solid catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(face.GetEdgesAsCurveLoops(), face.FaceNormal, dst);
            ElementFilter secondaryFilter = new ElementIntersectsSolidFilter(catchSolid);
            List<Element> attachedElems = (from elem in collector
                                           where secondaryFilter.PassesFilter(elem)
                                           select elem).ToList();
            return attachedElems;
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
