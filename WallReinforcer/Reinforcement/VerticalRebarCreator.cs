#nullable enable
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Resources1P;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Shapes;
using WallReinforcer.Config;
using WallReinforcer.Models;
using WallReinforcer.Services;
using WallReinforcer.Utilities;
using Line = Autodesk.Revit.DB.Line;

namespace WallReinforcer.Reinforcement;

/// <summary>
/// Создание вертикальной арматуры стены.
/// Аналог Python-ноды 593c8c56 из Dynamo-скрипта (которая была EXCLUDED).
/// Упрощённая версия: создание прямой вертикальной арматуры с привязкой к анкеровке.
/// </summary>
public class VerticalRebarCreator
{
    private readonly Document _doc;
    private readonly WallGeometryService _geometryService;

    public VerticalRebarCreator(Document doc, WallGeometryService geometryService)
    {
        _doc = doc;
        _geometryService = geometryService;
    }

    /// <summary>
    /// Создать вертикальную арматуру для стены.
    /// </summary>
    /// <param name="wp">Параметры стены</param>
    /// <param name="wr">Параметры армирования</param>
    /// <param name="wancs">Анкеры с нижележащего уровня</param>
    /// <param name="workView">Рабочий вид</param>
    /// <returns>Созданная вертикальная арматура</returns>
    /*public IList<Rebar> CreateVRebars(
        WallParameters wp,
        WallReinforcement wr,
        IList<WallAnchor> wancs,
        View3D workView)
    {
        var results = new List<Rebar>();

        // Если есть анкеры с нахлёсткой — создаём арматуру с перекрытием
        var overlapAnchors = wancs.Where(a => a.ForOverlap == true && a.Location != null).ToList();

        if (overlapAnchors.Count > 0)
        {
            results.AddRange(CreateRebarsWithOverlap(wp, wr, overlapAnchors));
        }
        else
        {
            // Нет анкеров — создаём арматуру с базовой анкеровкой от уровня
            results.AddRange(CreateRebarsFromBase(wp, wr));
        }

        return results;
    }*/

    public IList<Rebar> CreateVRebars(
    WallParameters wp,
    WallReinforcement wr,
    IList<WallAnchor> wancs,
    View3D workView)
    {
        var results = new List<Rebar>();

        Solid solid = wp.Solid;
        List<Line> lines = [];
        List<Line> additLines = [];

        foreach (WallAnchor wanc in wancs)
        {
            switch (wanc.Assignment)
            {
                case WallAnchorAssigment.Edge:
                    double D = wr.DY2;
                    break;
                case WallAnchorAssigment.NearOpening:
                    D = wanc.D;
                    break;
                default:
                    D = wr.DY1; 
                    break;
            }

            XYZ sp = wanc.Location;
            Line cutline = Line.CreateBound(sp, sp + XYZ.BasisZ * wp.H);
            List<Line> spotlines = solid.IntersectWithCurve(cutline, null).Cast<Line>().ToList();

            if (spotlines.Count > 0)
            {
                double upslabT = 
            }


        }





        return results;
    }

    #region Private helpers

    /// <summary>
    /// Создать вертикальные стержни с нахлёсткой от анкеров нижележащего уровня.
    /// </summary>
    private IList<Rebar> CreateRebarsWithOverlap(
        WallParameters wp,
        WallReinforcement wr,
        IList<WallAnchor> anchors)
    {
        var results = new List<Rebar>();
        var host = _doc.GetElement(wp.Id) as Wall;
        if (host == null) return results;

        var shape = GetRebarShape(ReinforcementConfig.ShapeStraight);
        var rebarType = wr.GetDY1RebarType();
        if (shape == null || rebarType == null) return results;

        var overlapLength = AnchorCalculator.ComputeOverlapLength(wr.DY1, wp.BClass);
        var wallTopZ = wp.GetTopZ();

        foreach (var anchor in anchors)
        {
            if (anchor.Location == null) continue;

            var anchorTopZ = anchor.Location.Z + (anchor.L ?? 0);
            var rebarLength = wallTopZ - anchorTopZ;

            if (rebarLength <= 0) continue;

            var startPoint = new XYZ(
                anchor.Location.X,
                anchor.Location.Y,
                anchorTopZ);

            var endPoint = startPoint + XYZ.BasisZ * rebarLength;
            var line = Line.CreateBound(startPoint, endPoint);

            var rebar = Rebar.CreateFromCurves(
                _doc,
                RebarStyle.Standard,
                rebarType,
                null, // startHook
                null, // endHook
                host,
                wp.Orient, // normal
                new List<Curve> { line },
                RebarHookOrientation.Left,
                RebarHookOrientation.Left,
                false,
                true);

            if (rebar == null) continue;

            _doc.Regenerate();

            // Устанавливаем параметры
            if (anchor.N.HasValue && anchor.N.Value > 1)
            {
                rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LAYOUT_RULE)?.Set(3);
                rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.Set(anchor.N.Value);
                if (anchor.Step.HasValue)
                    rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.Set(anchor.Step.Value);
            }

            rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(
                wp.LvlParity == 0
                    ? ReinforcementPartitionNames.reinfPartName_VertInside
                    : ReinforcementPartitionNames.reinfPartName_VertOutside);

            results.Add(rebar);
        }

        _doc.Regenerate();
        return results;
    }

    /// <summary>
    /// Создать вертикальные стержни от базового уровня стены (без анкеров).
    /// </summary>
    private IList<Rebar> CreateRebarsFromBase(WallParameters wp, WallReinforcement wr)
    {
        var results = new List<Rebar>();
        var host = _doc.GetElement(wp.Id) as Wall;
        if (host == null) return results;

        var shape = GetRebarShape(ReinforcementConfig.ShapeStraight);
        var rebarType = wr.GetDY1RebarType();
        if (shape == null || rebarType == null) return results;

        var anchorLength = AnchorCalculator.ComputeAnchorLength(wr.DY1, wp.BClass);
        var baseZ = wp.GetBaseZ();
        var topZ = wp.GetTopZ();
        var wallHeight = topZ - baseZ;

        // Создаём линию арматуры
        var sp = wp.SP;
        var ep = wp.EP;
        var wallLength = sp.DistanceTo(ep);

        // Количество стержней по шагу
        var n = wallLength > 0 ? (int)System.Math.Floor(wallLength / wr.StepY) + 1 : 2;
        var actualStep = n > 1 ? (wallLength) / (n - 1) : 0;

        for (int i = 0; i < n; i++)
        {
            var t = n > 1 ? (double)i / (n - 1) : 0.5;
            var pointOnLine = sp + (ep - sp) * t;

            var startPoint = new XYZ(pointOnLine.X, pointOnLine.Y, baseZ - anchorLength);
            var endPoint = new XYZ(pointOnLine.X, pointOnLine.Y, topZ);
            var line = Line.CreateBound(startPoint, endPoint);

            var rebar = Rebar.CreateFromCurves(
                _doc,
                RebarStyle.Standard,
                rebarType,
                null, // startHook
                null, // endHook
                host,
                wp.Orient, // normal
                new List<Curve> { line },
                RebarHookOrientation.Left,
                RebarHookOrientation.Left,
                false,
                true);

            if (rebar == null) continue;

            _doc.Regenerate();

            rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(
                wp.LvlParity == 0
                    ? ReinforcementPartitionNames.reinfPartName_VertInside
                    : ReinforcementPartitionNames.reinfPartName_VertOutside);

            results.Add(rebar);
        }

        _doc.Regenerate();
        return results;
    }

    private RebarShape? GetRebarShape(string shapeName)
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(RebarShape))
            .Cast<RebarShape>()
            .FirstOrDefault(s => s.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString() == shapeName);
    }

    #endregion
}
