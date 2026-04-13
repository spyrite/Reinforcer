#nullable enable
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System.Linq;
using WallReinforcer.Config;
using WallReinforcer.Utilities;

namespace WallReinforcer.Models;

/// <summary>
/// Параметры армирования стены: диаметры, шаги, покрытия, типы арматуры.
/// Аналог WallReinforcement из Dynamo-скрипта.
/// </summary>
public class WallReinforcement
{
    private readonly Document _doc;
    private readonly Wall _wall;

    // Диаметры арматуры (ft)
    public double DX { get; }       // Горизонтальная
    public double DY1 { get; }      // Вертикальная 1
    public double DY2 { get; }      // Вертикальная 2
    public double DY3 { get; }      // Вертикальная 3
    public double StepX { get; }    // Шаг горизонтальной (ft)
    public double StepY { get; }    // Шаг вертикальной (ft)
    public double DStud { get; }    // Диаметр шпилек (ft)

    // Защитные слои (ft)
    public double CovExt { get; set; }  // Наружный
    public double CovInt { get; set; }  // Внутренний
    public double CovOth { get; set; }  // Прочий

    /// <summary>
    /// Расстояние между рядами шпилек (DStud * 2.5), ft.
    /// </summary>
    public double DStudS { get; }

    public WallReinforcement(Document doc, Wall wall)
    {
        _doc = doc;
        _wall = wall;

        DX = GetWallParamValue(ReinforcementConfig.ParamHorizDiameter);
        DY1 = GetWallParamValue(ReinforcementConfig.ParamVertDiameter1);
        DY2 = GetWallParamValue(ReinforcementConfig.ParamVertDiameter2);
        DY3 = GetWallParamValue(ReinforcementConfig.ParamVertDiameter3);
        StepX = GetWallParamValue(ReinforcementConfig.ParamHorizStep);
        StepY = GetWallParamValue(ReinforcementConfig.ParamVertStep);
        DStud = GetWallParamValue(ReinforcementConfig.ParamStudDiameter);

        CovExt = GetCoverDistance(BuiltInParameter.CLEAR_COVER_EXTERIOR);
        CovInt = GetCoverDistance(BuiltInParameter.CLEAR_COVER_INTERIOR);
        CovOth = GetCoverDistance(BuiltInParameter.CLEAR_COVER_OTHER);

        DStudS = DStud * 2.5;
    }

    /// <summary>
    /// Найти тип арматуры для DY1 (A500, нужный диаметр, не в погонных метрах).
    /// </summary>
    public RebarBarType? GetDY1RebarType() =>
        FindRebarType(DY1, "А500С", false);

    /// <summary>
    /// Найти тип арматуры для DY2 (A500, нужный диаметр, не в погонных метрах).
    /// </summary>
    public RebarBarType? GetDY2RebarType() =>
        FindRebarType(DY2, "А500С", false);

    /// <summary>
    /// Найти тип арматуры для DX (A500, нужный диаметр, не в погонных метрах).
    /// </summary>
    public RebarBarType? GetDXRebarType() =>
        FindRebarType(DX, "А500С", false);

    /// <summary>
    /// Найти тип арматуры для горизонтальной AreaReinforcement (A500, в погонных метрах).
    /// </summary>
    public RebarBarType? GetDXRebarTypeForArea() =>
        FindRebarType(DX, "А500С", true);

    /// <summary>
    /// Найти тип арматуры для шпилек (A240, не в погонных метрах).
    /// </summary>
    public RebarBarType? GetStudRebarType() =>
        FindRebarType(DStud, "A240", false);

    #region Private helpers

    private double GetWallParamValue(string paramName)
    {
        var pars = _wall.GetParameters(paramName);
        if (pars != null && pars.Count > 0)
            return pars[0].AsDouble();
        return 0;
    }

    private double GetCoverDistance(BuiltInParameter bip)
    {
        var elemId = _wall.get_Parameter(bip)?.AsElementId();
        if (elemId == null || elemId == ElementId.InvalidElementId)
            return 0;
        var elem = _doc.GetElement(elemId);
        if (elem is RebarCoverType coverType)
            return coverType.CoverDistance;
        return 0;
    }

    private RebarBarType? FindRebarType(double diameterFt, string gradeName, bool inMeters)
    {
        var diameterMm = (int)System.Math.Round(UnitConverter.FtToMm(diameterFt));
        var inMetersInt = inMeters ? 1 : 0;

        var collector = new FilteredElementCollector(_doc)
            .OfClass(typeof(RebarBarType));

        foreach (RebarBarType rt in collector)
        {
            var name = rt.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString();
            if (name == null || !name.Contains(gradeName))
                continue;

            var diam = (int)System.Math.Round(
                UnitConverter.FtToMm(rt.get_Parameter(BuiltInParameter.REBAR_BAR_DIAMETER)?.AsDouble() ?? 0));

            var param = rt.get_Parameter(ReinforcementConfig.GuidInRunningMeters);
            var inMeteres = param?.AsInteger() ?? 0;

            if (diam == diameterMm && inMeteres == inMetersInt)
                return rt;
        }

        return null;
    }

    #endregion
}
