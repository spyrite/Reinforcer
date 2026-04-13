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

namespace WallReinforcer.Services;

/// <summary>
/// Сервис для поиска и классификации анкеров с нижележащего уровня.
/// Аналог Python-ноды f82187f9 из Dynamo-скрипта.
/// </summary>
public class WallAnchorService
{
    private readonly Document _doc;
    private readonly View3D _workView;

    public WallAnchorService(Document doc, View3D workView)
    {
        _doc = doc;
        _workView = workView;
    }

    /// <summary>
    /// Найти анкеры с нижележащего уровня для стены.
    /// </summary>
    public IList<WallAnchor> GetWallAnchors(WallParameters wp, WallReinforcement wr)
    {
        var prevLevel = wp.GetPreviousLevel();
        if (prevLevel == null)
            return new List<WallAnchor> { WallAnchor.Empty() };

        // Собираем арматуру на рабочем виде, привязанную к предыдущему уровню
        var rebars = new FilteredElementCollector(_doc, _workView.Id)
            .OfCategory(BuiltInCategory.OST_Rebar)
            .Cast<Rebar>()
            .Where(r =>
            {
                var lvl = GetRebarLevel(r);
                return lvl != null && lvl.Id.Value == prevLevel.Id.Value;
            })
            .ToList();

        var anchors = new List<WallAnchor>();

        foreach (var rebar in rebars)
        {
            var anchor = new WallAnchor();
            RefreshAnchorData(anchor, wp, wr, rebar);

            if (anchor.L.HasValue && anchor.ForOverlap == true)
                anchors.Add(anchor);
        }

        if (anchors.Count > 0)
        {
            UpdateAnchorAssignments(anchors, wp);
        }
        else
        {
            anchors.Add(WallAnchor.Empty());
        }

        return anchors;
    }

    #region Private helpers

    private void RefreshAnchorData(WallAnchor anchor, WallParameters wp, WallReinforcement wr, Rebar rebar)
    {
        var solid = wp.Solid;

        var curves = rebar.GetCenterlineCurves(false, true, true,
            MultiplanarOption.IncludeOnlyPlanarCurves, 0);

        foreach (var curve in curves)
        {
            if (curve is Line line)
            {
                var dir = line.Direction.Normalize();
                var absDir = VectorHelpers.VecAbs(dir);

                if (!absDir.IsAlmostEqualTo(XYZ.BasisZ)) continue;

                var intersections = solid.IntersectWithCurve(line, new SolidCurveIntersectionOptions());
                if (intersections == null || intersections.SegmentCount == 0) continue;

                var curveSeg = intersections.GetCurveSegment(0);
                var sp = curveSeg.GetEndPoint(0);

                anchor.Location = new XYZ(sp.X, sp.Y, wp.Z);
                anchor.L = System.Math.Round(curveSeg.Length * UnitConverter.FtToMm(1.0)) / UnitConverter.FtToMm(1.0);
                anchor.D = rebar.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble();
                anchor.N = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.AsInteger();
                anchor.Step = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.AsDouble();
                anchor.DistPathDir = rebar.GetShapeDrivenAccessor()?.GetDistributionPath()?.Direction.Normalize();

                break;
            }
        }

        if (anchor.L != null)
        {
            var anchorLenMm = System.Math.Round(anchor.L.Value * UnitConverter.FtToMm(1.0), -1);
            var expectedAnchorMm = System.Math.Round(
                AnchorCalculator.ComputeAnchorLength(anchor.D ?? 0, wp.BClass) * UnitConverter.FtToMm(1.0));
            var expectedOverlapMm = System.Math.Round(
                AnchorCalculator.ComputeOverlapLength(anchor.D ?? 0, wp.BClass) * UnitConverter.FtToMm(1.0));
            var maxOverlapMm = System.Math.Round(
                AnchorCalculator.ComputeMaxOverlapLength(anchor.D ?? 0, wp.BClass) * UnitConverter.FtToMm(1.0));

            if (anchorLenMm == expectedAnchorMm)
            {
                anchor.ForAnchor = true;
                anchor.ForOverlap = false;
            }
            else if (anchorLenMm == expectedOverlapMm || anchorLenMm == maxOverlapMm)
            {
                anchor.ForAnchor = false;
                anchor.ForOverlap = true;
            }
            else
            {
                anchor.ForAnchor = null;
                anchor.ForOverlap = null;
            }
        }
    }

    /// <summary>
    /// Классифицировать анкеры по типу привязки: Edge, EdgeLinear, NearOpening, Linear.
    /// </summary>
    private void UpdateAnchorAssignments(IList<WallAnchor> anchors, WallParameters wp)
    {
        var dstsToSingleSP = new List<double>();
        var dstsToSingleEP = new List<double>();
        var dstsToArray = new List<double>();

        foreach (var wanc in anchors)
        {
            if (wanc.Location == null) continue;

            var dstSP = System.Math.Round(
                wp.SP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0));
            var dstEP = System.Math.Round(
                wp.EP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0));

            if (wanc.N == 1)
            {
                dstsToSingleSP.Add(dstSP);
                dstsToSingleEP.Add(dstEP);
            }
            else
            {
                dstsToArray.Add(dstSP);
                dstsToArray.Add(dstEP);
            }
        }

        var uniqueSingleSP = dstsToSingleSP.Distinct().OrderBy(d => d).ToList();
        var uniqueSingleEP = dstsToSingleEP.Distinct().OrderBy(d => d).ToList();
        var uniqueArray = dstsToArray.Distinct().OrderBy(d => d).ToList();

        foreach (var wanc in anchors)
        {
            if (wanc.Location == null) continue;

            var dstSP = System.Math.Round(
                wp.SP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0));
            var dstEP = System.Math.Round(
                wp.EP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0));
            var minDst = System.Math.Min(dstSP, dstEP);

            if (wanc.N == 1)
            {
                var isEdge = uniqueSingleSP.Count > 0 && (minDst == uniqueSingleSP[0] ||
                    (uniqueSingleSP.Count > 1 && minDst == uniqueSingleSP[1])) ||
                    uniqueSingleEP.Count > 0 && (minDst == uniqueSingleEP[0] ||
                    (uniqueSingleEP.Count > 1 && minDst == uniqueSingleEP[1]));

                if (isEdge)
                    wanc.Assignment = "Edge";
            }
            else
            {
                if (uniqueArray.Count > 0 && minDst == uniqueArray[0])
                    wanc.Assignment = "EdgeLinear";
            }
        }

        // Проверяем близость к проёмам
        var openings = wp.GetOpenings(_workView);
        if (openings.Count > 0)
        {
            foreach (var opening in openings)
            {
                var dstsOpenSP = new List<double>();
                var dstsOpenEP = new List<double>();

                foreach (var wanc in anchors)
                {
                    if (wanc.Location == null) continue;

                    dstsOpenSP.Add(System.Math.Round(
                        opening.SP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0)));
                    dstsOpenEP.Add(System.Math.Round(
                        opening.EP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0)));
                }

                var uniqueOpenSP = dstsOpenSP.Distinct().OrderBy(d => d).ToList();
                var uniqueOpenEP = dstsOpenEP.Distinct().OrderBy(d => d).ToList();

                foreach (var wanc in anchors)
                {
                    if (wanc.Location == null || wanc.N != 1) continue;

                    var dstOpenSP = System.Math.Round(
                        opening.SP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0));
                    var dstOpenEP = System.Math.Round(
                        opening.EP.DistanceTo(wanc.Location) * UnitConverter.FtToMm(1.0));
                    var minDst = System.Math.Min(dstOpenSP, dstOpenEP);

                    var isNear = uniqueOpenSP.Count > 0 &&
                        (minDst == uniqueOpenSP[0] || (uniqueOpenSP.Count > 1 && minDst == uniqueOpenSP[1])) ||
                        uniqueOpenEP.Count > 0 &&
                        (minDst == uniqueOpenEP[0] || (uniqueOpenEP.Count > 1 && minDst == uniqueOpenEP[1]));

                    if (isNear)
                        wanc.Assignment = "NearOpening";
                }
            }
        }
    }

    private Level? GetRebarLevel(Rebar rebar)
    {
        var hostId = rebar.GetHostId();
        if (hostId != ElementId.InvalidElementId)
        {
            var host = _doc.GetElement(hostId);
            if (host is Wall wall)
            {
                return _doc.GetElement(wall.LevelId) as Level;
            }
            else if (host is Floor floor)
            {
                return _doc.GetElement(floor.LevelId) as Level;
            }
        }
        return null;
    }

    #endregion
}
