using System;
using WallReinforcer.Config;
using WallReinforcer.Utilities;

namespace WallReinforcer.Services;

/// <summary>
/// Расчёт длин анкеровки и нахлёстки арматуры по СП 63.13330.
/// </summary>
public static class AnchorCalculator
{
    /// <summary>
    /// Базовая длина анкеровки l0an = (Rs * As) / (Rbond * us)
    /// </summary>
    /// <param name="diameterFt">Диаметр арматуры в футах</param>
    /// <param name="bclass">Класс бетона (B15, B20...)</param>
    /// <returns>Базовая длина анкеровки в футах</returns>
    public static double ComputeBaseAnchorLength(double diameterFt, string bclass)
    {
        if (!Config.ReinforcementConfig.RbtValues.TryGetValue(bclass, out double rbt))
            rbt = 1.05; // По умолчанию B25

        double rs = ReinforcementConfig.Rs;                          // 435 МПа
        double rbond = rbt * ReinforcementConfig.BondCoefficient * 1; // Rbt * 2.5 * 1
        double as_ = (System.Math.PI * diameterFt * diameterFt) / 4;  // Площадь сечения
        double us = System.Math.PI * diameterFt;                      // Периметр

        return (rs * as_) / (rbond * us);
    }

    /// <summary>
    /// Расчётная длина анкеровки lan = 0.75 * l0an
    /// с ограничениями: min(0.3*l0an, 15*d, 200мм).
    /// Округляется вверх до 10 мм.
    /// </summary>
    public static double ComputeAnchorLength(double diameterFt, string bclass)
    {
        var l0an = ComputeBaseAnchorLength(diameterFt, bclass);
        var lan = ReinforcementConfig.AnchorFactor * l0an;

        var min0 = ReinforcementConfig.MinAnchorBaseFraction * l0an;
        var min1 = ReinforcementConfig.MinAnchorDiameters * diameterFt;
        var min2 = UnitConverter.MmToFt(ReinforcementConfig.MinAnchorLengthMm);

        var result = System.Math.Max(min0, System.Math.Max(min1, System.Math.Max(min2, lan)));
        return UnitConverter.RoundUpTo10mm(result);
    }

    /// <summary>
    /// Длина нахлёстки lan = 0.9 * l0an
    /// с ограничениями: min(0.3*l0an, 15*d, 200мм).
    /// Округляется вверх до 10 мм.
    /// </summary>
    public static double ComputeOverlapLength(double diameterFt, string bclass)
    {
        var l0an = ComputeBaseAnchorLength(diameterFt, bclass);
        var lan = ReinforcementConfig.OverlapFactor * l0an;

        var min0 = ReinforcementConfig.MinAnchorBaseFraction * l0an;
        var min1 = ReinforcementConfig.MinAnchorDiameters * diameterFt;
        var min2 = UnitConverter.MmToFt(ReinforcementConfig.MinAnchorLengthMm);

        var result = System.Math.Max(min0, System.Math.Max(min1, System.Math.Max(min2, lan)));
        return UnitConverter.RoundUpTo10mm(result);
    }

    /// <summary>
    /// Максимальная длина нахлёстки (2.3 * lan) — округлена до 10 мм.
    /// </summary>
    public static double ComputeMaxOverlapLength(double diameterFt, string bclass)
    {
        var lan = ComputeOverlapLength(diameterFt, bclass);
        var maxOverlap = ReinforcementConfig.MaxOverlapMultiplier * lan;
        return UnitConverter.RoundUpTo10mm(maxOverlap);
    }
}
