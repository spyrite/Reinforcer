#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using WallReinforcer.Config;
using WallReinforcer.Utilities;

namespace WallReinforcer.Models;

/// <summary>
/// Геометрические и структурные параметры стены.
/// Аналог WallParameters из Dynamo-скрипта.
/// </summary>
public class WallParameters
{
    public ElementId Id { get; }
    public double T { get; }              // Толщина стены (ft)
    public double H { get; }              // Высота стены (ft)
    public double TopOffset { get; }      // Смещение верха (ft)
    public double BaseOffset { get; }     // Смещение низа (ft)
    public double Z { get; }              // Z начальной точки + BaseOffset (ft)
    public XYZ Dir { get; }               // Направление осевой линии (единичный)
    public XYZ Orient { get; }            // Ориентация (нормаль стены)
    public Curve CenterLine { get; } = null!; // Осевая линия
    public ElementId TopLvlId { get; }    // Уровень верха
    public ElementId BottomLvlId { get; } // Уровень низа
    public XYZ SP { get; }                // Начальная точка
    public XYZ EP { get; }                // Конечная точка
    public string BClass { get; }         // Класс бетона
    public Solid Solid { get; }           // Тело стены
    public bool JoinAllowedAtStart { get; }
    public bool JoinAllowedAtEnd { get; }
    public IList<ElementId> JoinedWallsAtStart { get; }
    public IList<ElementId> JoinedWallsAtEnd { get; }

    /// <summary>
    /// Чётность уровня (0 = чётный, 1 = нечётный) — для чередования шпилек.
    /// </summary>
    public int LvlParity { get; }

    private readonly Document _doc;
    private readonly Options _optForWalls;
    private readonly IList<IList<Level>> _levels;
    private readonly IList<Level> _levelsSorted;

    public WallParameters(
        Document doc,
        Wall wall,
        Options optForWalls,
        IList<IList<Level>> levels,
        IList<Level> levelsSorted)
    {
        _doc = doc;
        _optForWalls = optForWalls;
        _levels = levels;
        _levelsSorted = levelsSorted;

        Id = wall.Id;
        T = wall.Width;
        H = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0;
        TopOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)?.AsDouble() ?? 0;
        BaseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0;

        var locCurve = wall.Location as LocationCurve;
        if (locCurve != null)
        {
            CenterLine = locCurve.Curve;
            SP = CenterLine.GetEndPoint(0);
            EP = CenterLine.GetEndPoint(1);

            // Curve.Direction → для Line используем метод GetDirection
            var line = locCurve.Curve as Line;
            Dir = line != null ? line.Direction.Normalize() : XYZ.BasisX;

            Z = SP.Z + BaseOffset;
        }
        else
        {
            Dir = XYZ.BasisX;
            SP = XYZ.Zero;
            EP = XYZ.Zero;
            Z = 0;
        }

        Orient = wall.Orientation.Normalize();
        TopLvlId = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.AsElementId() ?? ElementId.InvalidElementId;
        BottomLvlId = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId() ?? ElementId.InvalidElementId;

        var pars = wall.GetParameters(ReinforcementConfig.ParamBClass);
        BClass = (pars != null && pars.Count > 0) ? pars[0].AsString() ?? string.Empty : string.Empty;

        Solid = GetWallSolid(wall);
        JoinAllowedAtStart = WallUtils.IsWallJoinAllowedAtEnd(wall, 0);
        JoinAllowedAtEnd = WallUtils.IsWallJoinAllowedAtEnd(wall, 1);

        JoinedWallsAtStart = locCurve != null
            ? locCurve.get_ElementsAtJoin(0).Cast<Element>().Select(elem => elem.Id).ToList()
            : new List<ElementId>();
        JoinedWallsAtEnd = locCurve != null
            ? locCurve.get_ElementsAtJoin(1).Cast<Element>().Select(elem => elem.Id).ToList()
            : new List<ElementId>();

        // LvlParity
        var wallLvlId = wall.LevelId.Value;
        LvlParity = 0;
        for (int i = 0; i < _levels.Count; i++)
        {
            foreach (var lvl in _levels[i])
            {
                if (lvl.Id.Value == wallLvlId)
                {
                    LvlParity = i % 2;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Получить предыдущий уровень (для поиска анкеров с нижележащего этажа).
    /// </summary>
    public Level? GetPreviousLevel()
    {
        var bottomLvl = _doc.GetElement(BottomLvlId) as Level;
        if (bottomLvl == null) return null;

        var nameParts = bottomLvl.Name.Split('.');
        if (nameParts.Length == 2)
            return _levelsSorted.FirstOrDefault();

        // Поиск по bounding box нижележащей стены
        var catchPoint = CenterLine.ComputeDerivatives(0.5, true).Origin
            - XYZ.BasisZ * UnitConverter.MmToFt(ReinforcementConfig.LowerWallOffsetMm);

        var filter = new BoundingBoxContainsPointFilter(catchPoint);
        var lowerWall = new FilteredElementCollector(_doc)
            .OfClass(typeof(Wall))
            .WherePasses(filter)
            .FirstElement() as Wall;

        if (lowerWall != null)
        {
            var pLvlId = lowerWall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT)?.AsElementId();
            return pLvlId != null ? _doc.GetElement(pLvlId) as Level : null;
        }

        return null;
    }

    /// <summary>
    /// Z верхней грани стены.
    /// </summary>
    public double GetTopZ()
    {
        var lvl = _doc.GetElement(TopLvlId) as Level;
        return (lvl?.Elevation ?? 0) + TopOffset;
    }

    /// <summary>
    /// Z нижней грани стены.
    /// </summary>
    public double GetBaseZ()
    {
        var lvl = _doc.GetElement(BottomLvlId) as Level;
        return (lvl?.Elevation ?? 0) + BaseOffset;
    }

    /// <summary>
    /// Средняя точка осевой линии.
    /// </summary>
    public XYZ GetMidPointOfCenterLine() =>
        CenterLine.ComputeDerivatives(0.5, true).Origin;

    /// <summary>
    /// Получить проёмы (окна) в стене.
    /// </summary>
    public IList<OpeningParameters> GetOpenings(View workView)
    {
        var wall = _doc.GetElement(Id) as Wall;
        if (wall == null) return new List<OpeningParameters>();

        var filter = new LogicalAndFilter(
            new ElementClassFilter(typeof(FamilyInstance)),
            new ElementCategoryFilter(BuiltInCategory.OST_Windows));

        var openingIds = wall.GetDependentElements(filter);
        var result = new List<OpeningParameters>();

        foreach (var oid in openingIds)
        {
            if (_doc.GetElement(oid) is FamilyInstance opening)
            {
                result.Add(new OpeningParameters(this, opening, workView));
            }
        }

        return result;
    }

    private Solid GetWallSolid(Wall wall)
    {
        var geomElem = wall.get_Geometry(_optForWalls);
        if (geomElem == null)
            throw new System.InvalidOperationException($"Не удалось получить геометру стены {wall.Id}");

        foreach (var geomObj in geomElem)
        {
            if (geomObj is Solid s && s.Volume > 0)
                return s;

            if (geomObj is GeometryInstance geomInst)
            {
                foreach (var instObj in geomInst.GetInstanceGeometry())
                {
                    if (instObj is Solid s2 && s2.Volume > 0)
                        return s2;
                }
            }
        }

        throw new System.InvalidOperationException($"Не найдено Solid для стены {wall.Id}");
    }
}
