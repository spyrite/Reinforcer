#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using WallReinforcer.Models;
using WallReinforcer.Utilities;

namespace WallReinforcer.Services;

/// <summary>
/// Сервис для получения геометрических данных стены:
/// Solid, проёмы, толщины плит, поиск вышележащих/нижележащих стен.
/// </summary>
public class WallGeometryService
{
    private readonly Document _doc;

    public WallGeometryService(Document doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Получить Solid стены с вычисленными ссылками (для пересечений).
    /// </summary>
    public Solid? GetWallSolid(Wall wall)
    {
        var opt = new Options { ComputeReferences = true };
        var geomElem = wall.get_Geometry(opt);
        if (geomElem == null) return null;

        foreach (var geomObj in geomElem)
        {
            if (geomObj is Solid solid && solid.Volume > 0)
                return solid;

            if (geomObj is GeometryInstance geomInst)
            {
                foreach (var instObj in geomInst.GetInstanceGeometry())
                {
                    if (instObj is Solid s && s.Volume > 0)
                        return s;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Получить толщину плиты на заданном уровне в точке catchPoint.
    /// </summary>
    public double GetSlabThickness(ElementId levelId, XYZ catchPoint)
    {
        var filter = new ElementLevelFilter(levelId);
        var slabs = new FilteredElementCollector(_doc)
            .OfClass(typeof(Floor))
            .WherePasses(filter)
            .Cast<Floor>()
            .ToList();

        var opt = new Options();

        foreach (var slab in slabs)
        {
            var geomElem = slab.get_Geometry(opt);
            if (geomElem == null) continue;

            foreach (var geomObj in geomElem)
            {
                if (geomObj is Solid slabSolid)
                {
                    var topFace = slabSolid.Faces
                        .Cast<Face>()
                        .OfType<PlanarFace>()
                        .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ));

                    if (topFace != null && topFace.Project(catchPoint) != null)
                        return slab.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM)?.AsDouble() ?? 0;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Найти плиту (Floor) на уровне в точке catchPoint.
    /// </summary>
    public Floor? GetFloor(ElementId levelId, XYZ catchPoint)
    {
        var filter = new ElementLevelFilter(levelId);
        var slabs = new FilteredElementCollector(_doc)
            .OfClass(typeof(Floor))
            .WherePasses(filter)
            .Cast<Floor>()
            .ToList();

        var opt = new Options();

        foreach (var slab in slabs)
        {
            var geomElem = slab.get_Geometry(opt);
            if (geomElem == null) continue;

            foreach (var geomObj in geomElem)
            {
                if (geomObj is Solid slabSolid)
                {
                    var topFace = slabSolid.Faces
                        .Cast<Face>()
                        .OfType<PlanarFace>()
                        .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ));

                    if (topFace != null && topFace.Project(catchPoint) != null)
                        return slab;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Найти стену выше текущей на верхнем уровне.
    /// </summary>
    public Wall? GetUpperWall(WallParameters wp, XYZ point, double upperWallOffsetMm)
    {
        var catchPoint = point + XYZ.BasisZ * (wp.BaseOffset + wp.H + UnitConverter.MmToFt(upperWallOffsetMm));
        var filter = new ElementLevelFilter(wp.TopLvlId);

        var walls = new FilteredElementCollector(_doc)
            .OfClass(typeof(Wall))
            .WherePasses(filter)
            .Cast<Wall>()
            .ToList();

        foreach (var wall in walls)
        {
            var bbFilter = new BoundingBoxContainsPointFilter(catchPoint);
            if (bbFilter.PassesFilter(wall))
                return wall;
        }

        return null;
    }

    /// <summary>
    /// Получить толщину примыкающей стены в точке (если есть примыкание).
    /// </summary>
    public double GetJoinedWallThickness(WallParameters wp, XYZ point, double offsetMm)
    {
        var catchPoint = point + XYZ.BasisZ * wp.H / 2.0;
        var filter = new BoundingBoxContainsPointFilter(catchPoint);

        var walls = new FilteredElementCollector(_doc)
            .OfClass(typeof(Wall))
            .WherePasses(filter)
            .Excluding(new System.Collections.Generic.List<ElementId> { wp.Id })
            .Cast<Wall>()
            .ToList();

        if (walls.Count == 0) return 0;

        var cutline = Line.CreateBound(catchPoint, catchPoint + XYZ.BasisZ * UnitConverter.MmToFt(offsetMm));
        var wall = walls[0];
        var solid = GetWallSolid(wall);

        if (solid != null)
        {
            var options = new SolidCurveIntersectionOptions();
            var result = solid.IntersectWithCurve(cutline, options);
            // SolidCurveIntersection имеет SegmentCount
            if (result != null && result.SegmentCount > 0)
                return wall.Width;
        }

        return 0;
    }
}
