namespace WallReinforcer.Utilities;

/// <summary>
/// Утилиты конвертации между футами (внутренние единицы Revit) и миллиметрами.
/// </summary>
public static class UnitConverter
{
    /// <summary>
    /// Коэффициент конвертации: 1 фут = 304.8 мм.
    /// </summary>
    public const double FeetToMm = 304.8;

    /// <summary>
    /// Конвертировать футы в миллиметры.
    /// </summary>
    public static double FtToMm(double feet) => feet * FeetToMm;

    /// <summary>
    /// Конвертировать миллиметры в футы.
    /// </summary>
    public static double MmToFt(double mm) => mm / FeetToMm;

    /// <summary>
    /// Округлить значение в футах до ближайших 10 мм и вернуть обратно в футы.
    /// Используется для строительного округления длин анкеровки/нахлёстки.
    /// </summary>
    public static double RoundUpTo10mm(double feetValue)
    {
        var mm = FtToMm(feetValue);
        var rounded = System.Math.Ceiling(mm / 10.0) * 10.0;
        return MmToFt(rounded);
    }

    /// <summary>
    /// Округлить миллиметры до ближайших 10 мм вверх.
    /// </summary>
    public static double RoundUpMmTo10(double mmValue) =>
        System.Math.Ceiling(mmValue / 10.0) * 10.0;
}
