using Autodesk.Revit.DB;

namespace WallReinforcer.Models;

/// <summary>
/// Параметры проёма (окна) в стене.
/// Аналог OpeningParameters из Dynamo-скрипта.
/// </summary>
public class OpeningParameters
{
    /// <summary>
    /// ElementId проёма.
    /// </summary>
    public ElementId Id { get; }

    /// <summary>
    /// Ширина проёма вдоль направления стены (ft).
    /// </summary>
    public double B { get; }

    /// <summary>
    /// Высота проёма (ft).
    /// </summary>
    public double H { get; }

    /// <summary>
    /// Точка происхождения (середина по высоте, на оси стены).
    /// </summary>
    public XYZ Origin { get; }

    /// <summary>
    /// Начальная точка проёма вдоль оси стены.
    /// </summary>
    public XYZ SP { get; }

    /// <summary>
    /// Конечная точка проёма вдоль оси стены.
    /// </summary>
    public XYZ EP { get; }

    public OpeningParameters(WallParameters wp, FamilyInstance opening, View workView)
    {
        Id = opening.Id;

        var bbox = opening.get_BoundingBox(workView);
        if (bbox == null)
        {
            B = 0;
            H = 0;
            Origin = XYZ.Zero;
            SP = XYZ.Zero;
            EP = XYZ.Zero;
            return;
        }

        B = wp.Dir.DotProduct(bbox.Max) - wp.Dir.DotProduct(bbox.Min);
        H = bbox.Max.Z - bbox.Min.Z;
        Origin = opening.GetLocationPoint() + XYZ.BasisZ * (H - wp.H);
        SP = Origin - wp.Dir * (B / 2.0);
        EP = Origin + wp.Dir * (B / 2.0);
    }
}

/// <summary>
/// Extension для получения Location.Point у FamilyInstance.
/// </summary>
internal static class FamilyInstanceExtensions
{
    public static XYZ GetLocationPoint(this FamilyInstance fi)
    {
        if (fi.Location is LocationPoint lp)
            return lp.Point;
        return XYZ.Zero;
    }
}
