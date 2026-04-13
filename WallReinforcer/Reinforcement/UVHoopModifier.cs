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

namespace WallReinforcer.Reinforcement;

/// <summary>
/// Модификация существующих П-образных вертикальных хомутов.
/// Аналог Python-ноды 9c3b85ce из Dynamo-скрипта.
/// </summary>
public class UVHoopModifier
{
    private readonly Document _doc;

    public UVHoopModifier(Document doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Модифицировать существующие П-образные вертикальные хомуты под текущие параметры стены.
    /// </summary>
    public IList<Rebar> ModifyUVRebars(
        IList<Rebar> verticalRebars,
        IList<Rebar> existingUVHoops,
        WallParameters wp,
        WallReinforcement wr,
        string shapeName)
    {
        var shapeUV = GetRebarShape(shapeName);
        if (shapeUV == null) return new List<Rebar>();

        var host = _doc.GetElement(wp.Id) as Wall;
        if (host == null) return new List<Rebar>();

        // Находим П-образные вертикальные хомуты среди существующих
        var uvHoops = existingUVHoops
            .Where(r =>
            {
                var shapeId = r.GetShapeId();
                return shapeId != null && shapeId.Value == shapeUV.Id.Value;
            })
            .ToList();

        if (uvHoops.Count == 0) return new List<Rebar>();

        var origins = GetOriginsForUVRebars(verticalRebars, wp);
        var xVec = -XYZ.BasisZ; // Направление вверх (отрицательный Z для масштабирования)
        var yVec = wp.Orient;
        var results = new List<Rebar>();

        foreach (var p in origins)
        {
            var rebarType = GetRebarTypeByDiameter(p.Diameter);
            if (rebarType == null) continue;

            var placePoint = p.Point
                + wp.Dir * p.Diameter
                - yVec * (p.Diameter / 2.0);

            var nearestHoop = GetNearestUVRebar(placePoint, uvHoops);
            if (nearestHoop == null) continue;

            var upperSlabT = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
                .Where(f => f.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                .OrderByDescending(f => f.Origin.Z)
                .FirstOrDefault()?.FaceNormal.Z ?? 0;

            // ScaleToBox
            var accessor = nearestHoop.GetShapeDrivenAccessor();
            if (accessor != null)
            {
                accessor.SetRebarShapeId(shapeUV.Id);

                nearestHoop.ChangeTypeId(rebarType.Id);
                nearestHoop.get_Parameter(BuiltInParameter.REBAR_ELEM_LAYOUT_RULE)?.Set(3);
                nearestHoop.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.Set(p.N);

                if (p.N > 1 && p.Step.HasValue)
                    nearestHoop.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.Set(p.Step.Value);

                var xVecBox = xVec * (upperSlabT * 2);
                var yVecBox = yVec * (wp.T - wr.CovExt - wr.CovInt - wr.DX * 2);

                accessor.ScaleToBox(placePoint, xVecBox, yVecBox);
                results.Add(nearestHoop);
            }
        }

        _doc.Regenerate();
        return results;
    }

    #region Private helpers

    private RebarShape? GetRebarShape(string shapeName)
    {
        return new FilteredElementCollector(_doc)
            .OfClass(typeof(RebarShape))
            .Cast<RebarShape>()
            .FirstOrDefault(s => s.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString() == shapeName);
    }

    private RebarBarType? GetRebarTypeByDiameter(double diameterFt)
    {
        var diameterMm = (int)System.Math.Round(UnitConverter.FtToMm(diameterFt));

        return new FilteredElementCollector(_doc)
            .OfClass(typeof(RebarBarType))
            .Cast<RebarBarType>()
            .FirstOrDefault(rt =>
            {
                var name = rt.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString();
                if (name == null || !name.Contains("A500")) return false;

                var diam = (int)System.Math.Round(
                    UnitConverter.FtToMm(rt.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0));
                var inMeters = rt.get_Parameter(ReinforcementConfig.GuidInRunningMeters)?.AsInteger() ?? 0;

                return diam == diameterMm && inMeters == 0;
            });
    }

    /// <summary>
    /// Получить точки размещения вертикальных П-хомутов из вертикальной арматуры.
    /// </summary>
    private IList<UVHoopOrigin> GetOriginsForUVRebars(IList<Rebar> allRebarsV, WallParameters wp)
    {
        // Берём только тонкие стержни (< 20мм) на внутренней стороне
        var rebars = GetRebarsForInteriorSide(allRebarsV, wp)
            .Where(r => UnitConverter.FtToMm(r.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0) < 20)
            .ToList();

        var origins = new List<UVHoopOrigin>();

        foreach (var rebar in rebars)
        {
            var curves = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
            var line = curves.Cast<Curve>().FirstOrDefault() as Line;
            if (line == null) continue;

            var point = line.GetEndPoint(1);
            var n = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.AsInteger() ?? 1;
            var d = rebar.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0;
            var step = n > 1
                ? rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.AsDouble()
                : (double?)null;

            origins.Add(new UVHoopOrigin(point, n, step, d));
        }

        return origins;
    }

    private IList<Rebar> GetRebarsForInteriorSide(IList<Rebar> allRebarsV, WallParameters wp)
    {
        var extFace = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(wp.Orient));
        var intFace = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(-wp.Orient));

        if (extFace == null || intFace == null) return new List<Rebar>();

        var results = new List<Rebar>();

        foreach (var rebar in allRebarsV)
        {
            var curves = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
            var line = curves.Cast<Curve>().FirstOrDefault() as Line;
            if (line == null) continue;

            var p0 = line.GetEndPoint(0) + XYZ.BasisZ * UnitConverter.MmToFt(ReinforcementConfig.FaceProjectionOffsetMm);

            var extProj = extFace.Project(p0);
            var intProj = intFace.Project(p0);

            if (extProj == null || intProj == null) continue;

            if (p0.DistanceTo(extProj.XYZPoint) > p0.DistanceTo(intProj.XYZPoint))
                results.Add(rebar);
        }

        return results;
    }

    private Rebar? GetNearestUVRebar(XYZ targetPoint, IList<Rebar> candidates)
    {
        if (candidates.Count == 0) return null;

        var minDst = double.MaxValue;
        Rebar? nearest = null;

        foreach (var rebar in candidates)
        {
            var loc = GetRebarLocation(rebar);
            var dst = loc.DistanceTo(targetPoint);

            if (dst < minDst)
            {
                minDst = dst;
                nearest = rebar;
            }
        }

        if (nearest != null)
            candidates.Remove(nearest);

        return nearest;
    }

    private XYZ GetRebarLocation(Rebar rebar)
    {
        var curves = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
        var line = curves.Cast<Curve>().FirstOrDefault();
        return line?.GetEndPoint(0) ?? XYZ.Zero;
    }

    #endregion

    private class UVHoopOrigin
    {
        public XYZ Point { get; }
        public int N { get; }
        public double? Step { get; }
        public double Diameter { get; }

        public UVHoopOrigin(XYZ point, int n, double? step, double diameter)
        {
            Point = point;
            N = n;
            Step = step;
            Diameter = diameter;
        }
    }
}
