#nullable enable
using Autodesk.Revit.DB;

namespace WallReinforcer.Models;

public enum WallAnchorAssigment
{
    Linear,
    Edge,
    NearOpening
}

/// <summary>
/// Анкерная арматура с нижележащего этажа.
/// Аналог WallAnchor из Dynamo-скрипта.
/// </summary>
public class WallAnchor
{
    /// <summary>
    /// Расположение анкера (ft).
    /// </summary>
    public XYZ Location { get; set; }

    /// <summary>
    /// Длина анкеровки (ft).
    /// </summary>
    public double L { get; set; }

    /// <summary>
    /// Диаметр анкера (ft).
    /// </summary>
    public double D { get; set; }

    /// <summary>
    /// Количество стержней.
    /// </summary>
    public int N { get; set; }

    /// <summary>
    /// Шаг распределения (ft).
    /// </summary>
    public double Step { get; set; }

    /// <summary>
    /// Направление пути распределения.
    /// </summary>
    public XYZ DistPathDir { get; set; }

    /// <summary>
    /// Тип привязки: Linear, Edge, EdgeLinear, NearOpening.
    /// </summary>
    public WallAnchorAssigment Assignment { get; set; } = WallAnchorAssigment.Linear;

    /// <summary>
    /// Предназначен для анкеровки.
    /// </summary>
    public bool ForAnchor { get; set; }

    /// <summary>
    /// Предназначен для нахлёстки.
    /// </summary>
    public bool ForOverlap { get; set; }

    /// <summary>
    /// Создать пустой анкер (заглушка).
    /// </summary>
    public static WallAnchor Empty() => new WallAnchor();
}
