using Autodesk.Revit.DB;

namespace WallReinforcer.Utilities;

/// <summary>
/// Вспомогательные методы для работы с векторами (аналог VecABS, GetVecToken из Dynamo).
/// </summary>
public static class VectorHelpers
{
    /// <summary>
    /// Вернуть XYZ с абсолютными значениями компонент.
    /// Аналог VecABS из Python-скрипта.
    /// </summary>
    public static XYZ VecAbs(XYZ vec) =>
        new XYZ(System.Math.Abs(vec.X), System.Math.Abs(vec.Y), System.Math.Abs(vec.Z));

    /// <summary>
    /// Сумма компонент вектора — для определения ориентации.
    /// Аналог GetVecToken из Python-скрипта.
    /// </summary>
    public static double GetVecToken(XYZ vec) => vec.X + vec.Y + vec.Z;

    /// <summary>
    /// Вернуть ненулевую координату вектора (для единичных направлений).
    /// </summary>
    public static double GetNonZeroCoord(XYZ vec)
    {
        if (!IsAlmostZero(vec.X)) return vec.X;
        if (!IsAlmostZero(vec.Y)) return vec.Y;
        return vec.Z;
    }

    /// <summary>
    /// Покомпонентное умножение двух векторов.
    /// Аналог MultiplyVecs из Python-скрипта.
    /// </summary>
    public static XYZ MultiplyVecs(XYZ v1, XYZ v2) =>
        new XYZ(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);

    /// <summary>
    /// Проверка, что значение близко к нулю (в пределах 1e-9).
    /// </summary>
    public static bool IsAlmostZero(double value) =>
        System.Math.Abs(value) < 1e-9;

    /// <summary>
    /// Проверка, что вектор почти единичный по конкретной оси.
    /// </summary>
    public static bool IsAlmostParallelTo(XYZ vec, XYZ axis) =>
        VecAbs(vec.Normalize()).IsAlmostEqualTo(axis.Normalize());
}
