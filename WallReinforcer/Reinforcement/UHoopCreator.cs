#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using WallReinforcer.Config;
using WallReinforcer.Models;
using WallReinforcer.Utilities;
using RevitOSA.WallReinforcer.Resources1P;

namespace WallReinforcer.Reinforcement;

/// <summary>
/// Создание П-образных горизонтальных хомутов на основе AreaReinforcement.
/// Аналог Python-ноды 87c885b1 из Dynamo-скрипта.
/// </summary>
public class UHoopCreator
{
    private readonly Document _doc;

    public UHoopCreator(Document doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Создать П-образные горизонтальные хомуты для AreaReinforcement.
    /// </summary>
    public IList<Rebar> CreateUHRebars(
        AreaReinforcement ar,
        WallParameters wp,
        WallReinforcement wr,
        string shapeName)
    {
        var shape = GetRebarShape(shapeName);
        var rebarType = GetRebarTypeForUHoops(wr);
        if (shape == null || rebarType == null)
            return new List<Rebar>();

        var host = _doc.GetElement(ar.GetHostId()) as Wall;
        if (host == null) return new List<Rebar>();

        var origins = GetOriginsForUHRebars(ar, wp);
        var xVec = wp.Dir;
        var yVec = wp.Orient;
        var results = new List<Rebar>();

        foreach (var p in origins)
        {
            var placePoint = p.Point
                + XYZ.BasisZ * wr.DX / 2.0 * p.Token
                - xVec * wr.DX / 2.0 * p.Token;

            var rebar = Rebar.CreateFromRebarShape(_doc, shape, rebarType, host, placePoint, xVec * p.Token, yVec);
            if (rebar == null) continue;

            _doc.Regenerate();

            rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LAYOUT_RULE)?.Set(3);
            rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.Set(p.N);

            if (p.N > 1 && p.Step.HasValue)
                rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.Set(p.Step.Value);

            // Размеры П-хомутов
            var paramA = rebar.get_Parameter(ReinforcementConfig.GuidReinfA);
            paramA?.Set(wp.T * 2); // Длина

            var paramB = rebar.get_Parameter(ReinforcementConfig.GuidReinfB);
            paramB?.Set(wp.T - wr.CovExt - wr.CovInt); // Ширина

            rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(ReinforcementPartitionNames.reinfPartName_PStirrups);
            results.Add(rebar);
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

    private RebarBarType? GetRebarTypeForUHoops(WallReinforcement wr)
    {
        return wr.GetDXRebarType();
    }

    /// <summary>
    /// Получить точки размещения П-хомутов из AreaReinforcement.
    /// </summary>
    private IList<UHoopOrigin> GetOriginsForUHRebars(AreaReinforcement ar, WallParameters wp)
    {
        var rebars = ar.GetRebarInSystemIds()
            .Select(id => _doc.GetElement(id) as RebarInSystem)
            .Where(r => r != null)
            .ToList();

        var lines = rebars
            .Select(r => r.GetCenterlineCurves(false, true, true).Cast<Curve>().FirstOrDefault() as Line)
            .Where(l => l != null)
            .ToList();

        var origins = new List<UHoopOrigin>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var points = new[] { line.GetEndPoint(0), line.GetEndPoint(1) };

            foreach (var p in points)
            {
                var n = rebars[i].get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.AsInteger() ?? 1;
                var step = n > 1
                    ? rebars[i].get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.AsDouble()
                    : (double?)null;

                var token = p.IsAlmostEqualTo(line.GetEndPoint(0)) ? 1.0 : -1.0;

                origins.Add(new UHoopOrigin(p, n, step, token));
            }
        }

        return origins;
    }

    #endregion

    private class UHoopOrigin
    {
        public XYZ Point { get; }
        public int N { get; }
        public double? Step { get; }
        public double Token { get; }

        public UHoopOrigin(XYZ point, int n, double? step, double token)
        {
            Point = point;
            N = n;
            Step = step;
            Token = token;
        }
    }
}
