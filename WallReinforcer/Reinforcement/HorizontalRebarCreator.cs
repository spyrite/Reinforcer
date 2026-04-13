#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using WallReinforcer.Config;
using WallReinforcer.Models;
using WallReinforcer.Services;
using WallReinforcer.Utilities;
using RevitOSA.WallReinforcer.Resources1P;

namespace WallReinforcer.Reinforcement;

/// <summary>
/// Создание горизонтальной арматуры через AreaReinforcement.
/// Аналог Python-ноды 89a117f6 из Dynamo-скрипта.
/// </summary>
public class HorizontalRebarCreator
{
    private readonly Document _doc;
    private readonly WallGeometryService _geometryService;

    public HorizontalRebarCreator(Document doc, WallGeometryService geometryService)
    {
        _doc = doc;
        _geometryService = geometryService;
    }

    /// <summary>
    /// Создать горизонтальную арматуру для стены между уровнями.
    /// </summary>
    /// <param name="wp">Параметры стены</param>
    /// <param name="wr">Параметры армирования</param>
    /// <param name="lvlBot">Нижний уровень</param>
    /// <param name="lvlTop">Верхний уровень</param>
    /// <param name="minOffsetMm">Минимальный отступ от края (мм)</param>
    /// <returns>Список созданных AreaReinforcement</returns>
    public IList<AreaReinforcement> CreateHRebars(
        WallParameters wp,
        WallReinforcement wr,
        Level lvlBot,
        Level lvlTop,
        double minOffsetMm)
    {
        var arType = GetAreaReinforcementType();
        var rebarType = wr.GetDXRebarTypeForArea();
        if (rebarType == null || arType == null)
            return new List<AreaReinforcement>();

        var botSlabT = _geometryService.GetSlabThickness(lvlBot.Id, wp.GetMidPointOfCenterLine());
        var offsetMin = UnitConverter.MmToFt(minOffsetMm);

        var heightRange = lvlTop.Elevation - (lvlBot.Elevation + botSlabT) - 2 * offsetMin;
        var n = (int)System.Math.Floor(heightRange / wr.StepX) + 1;
        var offsetStart = (lvlTop.Elevation - (lvlBot.Elevation + botSlabT) - wr.StepX * (n - 1)) / 2.0;

        var additOffset = IsWallJoinedBothSides(wp) ? wr.DX : 0;

        // Создаём базовую линию сечения (cutline) на уровне низа + плита
        var cutline = (Line)CreateCutline(wp, lvlBot, botSlabT);
        cutline = (Line)cutline.CreateOffset(offsetStart, XYZ.BasisZ.CrossProduct(wp.Dir));

        var arCurveLoops = new List<CurveLoop>();

        for (int i = 0; i < n; i++)
        {
            var spotlines = wp.Solid.IntersectWithCurve(cutline, new SolidCurveIntersectionOptions());
            if (spotlines != null && spotlines.SegmentCount > 0)
            {
                // Нашли пересечение — строим CurveLoop для AreaReinforcement
                var curveSeg = spotlines.GetCurveSegment(0);
                var baseOffset = -(curveSeg.GetEndPoint(0).Z - wp.GetBaseZ() - wr.DX / 2.0 - additOffset);
                var topOffset = -(wp.H - System.Math.Abs(baseOffset)
                    - System.Math.Floor((wp.H - (System.Math.Abs(baseOffset) + wr.DX / 2.0)) / wr.StepX) * wr.StepX
                    - wr.DX);

                var cl = BuildAreaReinforcementCurveLoop(wp, baseOffset, topOffset);
                if (cl != null)
                    arCurveLoops.Add(cl);

                break; // Берём только первый найденный уровень
            }

            cutline = (Line)cutline.CreateOffset(wr.StepX, XYZ.BasisZ.CrossProduct(wp.Dir));
        }

        // Создаём AreaReinforcement — наружный и внутренний для каждого CurveLoop
        var hostWall = _doc.GetElement(wp.Id) as Wall;
        var arResults = new List<AreaReinforcement>();

        foreach (var cl in arCurveLoops)
        {
            if (hostWall == null) continue;

            var curves = cl.Cast<Curve>().ToList();

            // Наружный (exterior) — активен Top Dir 1
            var arExt = AreaReinforcement.Create(
                _doc, hostWall, curves, wp.Dir, arType.Id, rebarType.Id, ElementId.InvalidElementId);

            arExt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_TOP_DIR_1_GENERIC)?.Set(1);
            arExt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_TOP_DIR_2_GENERIC)?.Set(0);
            arExt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_BOTTOM_DIR_1_GENERIC)?.Set(0);
            arExt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_BOTTOM_DIR_2_GENERIC)?.Set(0);
            arExt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_SPACING_TOP_DIR_1_GENERIC)?.Set(wr.StepX);
            arExt.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(ReinforcementPartitionNames.reinfPartName_HorizOutside);

            _doc.Regenerate();

            // Внутренний (interior) — активен Bottom Dir 1
            var arInt = AreaReinforcement.Create(
                _doc, hostWall, curves, wp.Dir, arType.Id, rebarType.Id, ElementId.InvalidElementId);

            arInt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_TOP_DIR_1_GENERIC)?.Set(0);
            arInt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_TOP_DIR_2_GENERIC)?.Set(0);
            arInt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_BOTTOM_DIR_1_GENERIC)?.Set(1);
            arInt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_ACTIVE_BOTTOM_DIR_2_GENERIC)?.Set(0);
            arInt.get_Parameter(BuiltInParameter.REBAR_SYSTEM_SPACING_BOTTOM_DIR_1_GENERIC)?.Set(wr.StepX);
            arInt.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(ReinforcementPartitionNames.reinfPartName_HorizInside);

            _doc.Regenerate();
            arResults.Add(arInt);
        }

        _doc.Regenerate();
        return arResults;
    }

    #region Private helpers

    private AreaReinforcementType? GetAreaReinforcementType()
    {
        var types = new FilteredElementCollector(_doc)
            .OfClass(typeof(AreaReinforcementType))
            .ToElementIds();

        return types.Count > 0
            ? _doc.GetElement(types.First()) as AreaReinforcementType
            : null;
    }

    private bool IsWallJoinedBothSides(WallParameters wp)
    {
        var vecAbs = VectorHelpers.VecAbs(wp.Dir);
        return vecAbs.IsAlmostEqualTo(XYZ.BasisY)
            && wp.JoinedWallsAtStart.Count > 0
            && wp.JoinedWallsAtEnd.Count > 0;
    }

    private Line CreateCutline(WallParameters wp, Level lvlBot, double botSlabT)
    {
        var sp = wp.CenterLine.GetEndPoint(0);
        var ep = wp.CenterLine.GetEndPoint(1);
        var p0 = new XYZ(sp.X, sp.Y, lvlBot.Elevation + botSlabT);
        var p1 = new XYZ(ep.X, ep.Y, lvlBot.Elevation + botSlabT);
        return Line.CreateBound(p0, p1);
    }

    private CurveLoop? BuildAreaReinforcementCurveLoop(
        WallParameters wp,
        double baseOffset,
        double topOffset)
    {
        // Ищем основную грань (наружную)
        var mainFace = wp.Solid.Faces
            .Cast<Face>()
            .OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(wp.Orient));

        if (mainFace == null) return null;

        var wallCurveLoops = mainFace.GetEdgesAsCurveLoops();
        var arCurveLoop = new CurveLoop();

        // Для каждого CurveLoop применяем смещения
        foreach (var curveLoop in wallCurveLoops)
        {
            var offsets = new List<double>();

            foreach (var curve in curveLoop)
            {
                var line = curve as Line;
                var dir = line != null ? line.Direction.Normalize() : XYZ.BasisX;

                if (dir.IsAlmostEqualTo(-wp.Dir))
                    offsets.Add(baseOffset);
                else if (dir.IsAlmostEqualTo(wp.Dir))
                    offsets.Add(topOffset);
                else if (dir.IsAlmostEqualTo(XYZ.BasisZ) && wp.JoinAllowedAtStart
                    && System.Math.Abs(wp.Dir.DotProduct(curve.GetEndPoint(0)) - wp.Dir.DotProduct(wp.SP)) <= wp.T / 2.0)
                    offsets.Add(_geometryService.GetJoinedWallThickness(wp, wp.SP, ReinforcementConfig.JoinCheckOffsetMm)
                        - UnitConverter.MmToFt(ReinforcementConfig.JoinedWallThicknessOffsetMm));
                else if (dir.IsAlmostEqualTo(-XYZ.BasisZ) && wp.JoinAllowedAtEnd
                    && System.Math.Abs(wp.Dir.DotProduct(wp.EP) - wp.Dir.DotProduct(curve.GetEndPoint(0))) <= wp.T / 2.0)
                    offsets.Add(_geometryService.GetJoinedWallThickness(wp, wp.EP, ReinforcementConfig.JoinCheckOffsetMm)
                        - UnitConverter.MmToFt(ReinforcementConfig.JoinedWallThicknessOffsetMm));
                else
                    offsets.Add(0);
            }

            var offsetLoop = CurveLoop.CreateViaOffset(curveLoop, offsets, wp.Orient);
            foreach (var c in offsetLoop)
                arCurveLoop.Append(c);
        }

        return arCurveLoop;
    }

    #endregion
}
