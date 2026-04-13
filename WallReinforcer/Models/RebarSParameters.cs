using Autodesk.Revit.DB;
using WallReinforcer.Models;

namespace WallReinforcer.Models;

/// <summary>
/// Параметры размещения шпильки (зигзагообразной связи).
/// Аналог RebarSParameters из Dynamo-скрипта.
/// </summary>
public class RebarSParameters
{
    /// <summary>
    /// Угол наклона шпильки (радианы).
    /// </summary>
    public double Angle { get; }

    /// <summary>
    /// Вектор направления X (вдоль толщины стены).
    /// </summary>
    public XYZ XVec { get; }

    /// <summary>
    /// Вектор направления Y (вертикальный + горизонтальная составляющая).
    /// </summary>
    public XYZ YVec { get; }

    /// <summary>
    /// Точка размещения шпильки.
    /// </summary>
    public XYZ Origin { get; }

    public RebarSParameters(WallParameters wp, WallReinforcement wr, XYZ startPoint)
    {
        var thickness = wp.T - wr.CovExt - wr.CovInt - wr.DX;
        var sinValue = (wr.DStudS + wr.DStud) / thickness;

        // Защита от выхода за пределы допустимого диапазона arcsin
        sinValue = System.Math.Max(-1.0, System.Math.Min(1.0, sinValue));

        Angle = System.Math.Asin(sinValue);
        XVec = wp.Orient - XYZ.BasisZ * System.Math.Tan(Angle);
        YVec = XYZ.BasisZ + wp.Orient * System.Math.Tan(Angle);
        Origin = startPoint
            - XVec * (wr.DStudS / 2.0 + wr.DStud * 1.5)
            + YVec * wr.DStudS / 2.0
            - wp.Orient * (wr.DY1 / 2.0 + wr.DX / 2.0);
    }
}
