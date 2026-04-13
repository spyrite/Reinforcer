using System;
using System.Collections.Generic;
using RevitOSA.WallReinforcer.Resources1P;

namespace WallReinforcer.Config;

/// <summary>
/// Центральная конфигурация армирования стен.
/// Содержит все Shared Parameter GUID, имена partition, формулы и константы из Dynamo-скрипта.
/// </summary>
public static class ReinforcementConfig
{
    // ==================== Shared Parameter GUID (из RevitParameters.resx) ====================

    /// <summary>
    /// Размер. В погонных метрах (RebarBarType флаг)
    /// </summary>
    public static readonly Guid GuidInRunningMeters =
        new Guid(RevitParameters.p_Dims_InRunningMeters);

    /// <summary>
    /// Размер. Диаметр (для FamilyInstance ребар)
    /// </summary>
    public static readonly Guid GuidRebarDiameter =
        new Guid(RevitParameters.p_Dims_Diameter);

    /// <summary>
    /// Арм. Размер А (длина П-образного хомута)
    /// </summary>
    public static readonly Guid GuidReinfA =
        new Guid(RevitParameters.p_Reinf_A);

    /// <summary>
    /// Арм. Размер Б (ширина П-образного хомута)
    /// </summary>
    public static readonly Guid GuidReinfB =
        new("f4e10dbb-3a1c-4567-972d-b36679e122ad"); // НЕ в .resx, добавлен вручную

    /// <summary>
    /// Арм. Размер В (высота арматурного стержня)
    /// </summary>
    public static readonly Guid GuidReinfV =
        new Guid(RevitParameters.p_Reinf_V);

    // ==================== Имена параметров стен (shared parameters) ====================

    /// <summary>
    /// 1Пп_Класс бетона
    /// </summary>
    public static string ParamBClass => RevitParameters.pp_BClass;

    /// <summary>
    /// 1Пп_Арм_Горизонтальная_Диаметр
    /// </summary>
    public const string ParamHorizDiameter = "1Пп_Арм_Горизонтальная_Диаметр";

    /// <summary>
    /// 1Пп_Арм_Вертикальная_Диаметр 1
    /// </summary>
    public const string ParamVertDiameter1 = "1Пп_Арм_Вертикальная_Диаметр 1";

    /// <summary>
    /// 1Пп_Арм_Вертикальная_Диаметр 2
    /// </summary>
    public const string ParamVertDiameter2 = "1Пп_Арм_Вертикальная_Диаметр 2";

    /// <summary>
    /// 1Пп_Арм_Вертикальная_Диаметр 3
    /// </summary>
    public const string ParamVertDiameter3 = "1Пп_Арм_Вертикальная_Диаметр 3";

    /// <summary>
    /// 1Пп_Арм_Горизонтальная_Шаг
    /// </summary>
    public const string ParamHorizStep = "1Пп_Арм_Горизонтальная_Шаг";

    /// <summary>
    /// 1Пп_Арм_Вертикальная_Шаг
    /// </summary>
    public const string ParamVertStep = "1Пп_Арм_Вертикальная_Шаг";

    /// <summary>
    /// 1Пп_Арм_Шпильки_Диаметр
    /// </summary>
    public const string ParamStudDiameter = "1Пп_Арм_Шпильки_Диаметр";

    // ==================== Partition names (из ReinforcementPartitionNames.resx) ====================

    public static string PartitionHorizOutside => ReinforcementPartitionNames.reinfPartName_HorizOutside;
    public static string PartitionHorizInside => ReinforcementPartitionNames.reinfPartName_HorizInside;
    public static string PartitionVertOutside => ReinforcementPartitionNames.reinfPartName_VertOutside;
    public static string PartitionVertInside => ReinforcementPartitionNames.reinfPartName_VertInside;
    public static string PartitionPStirrups => ReinforcementPartitionNames.reinfPartName_PStirrups;
    public static string PartitionStudsOdd => ReinforcementPartitionNames.reinfPartName_Studs_OddRow;
    public static string PartitionStudsEven => ReinforcementPartitionNames.reinfPartName_Studs_EvenRow;
    public static string PartitionAnchors => ReinforcementPartitionNames.reinfPartName_Anchors;

    // ==================== Имена форм арматуры (RebarShape) ====================

    /// <summary>
    /// П-образный горизонтальный хомут
    /// </summary>
    public const string ShapeUHoop = "1П_Ск-1";

    /// <summary>
    /// Шпилька (зигзаг)
    /// </summary>
    public const string ShapeStud = "1П_Ш-1";

    /// <summary>
    /// Прямой стержень (вертикальная арматура)
    /// </summary>
    public const string ShapeStraight = "01";

    /// <summary>
    /// П-образный вертикальный хомут
    /// </summary>
    public const string ShapeUVHoop = "1П_Ск-1"; // может отличаться

    // ==================== RbtValues — расчётные сопротивления бетона осевому растяжению ====================

    /// <summary>
    /// Словарь: класс бетона → Rbt (МПа). СП 63.13330.
    /// </summary>
    public static readonly Dictionary<string, double> RbtValues = new()
    {
        ["B15"] = 0.75,
        ["B20"] = 0.90,
        ["B25"] = 1.05,
        ["B30"] = 1.15,
        ["B35"] = 1.30,
        ["B40"] = 1.40,
        ["B45"] = 1.50
    };

    // ==================== Формула анкеровки ====================

    /// <summary>
    /// Расчётное сопротивление арматуры (МПа) для A500.
    /// </summary>
    public const double Rs = 435.0;

    /// <summary>
    /// Коэффициент сцепления арматуры с бетоном.
    /// </summary>
    public const double BondCoefficient = 2.5;

    /// <summary>
    /// Коэффициент условий работы арматуры при анкеровке.
    /// </summary>
    public const double AnchorFactor = 0.75;

    /// <summary>
    /// Коэффициент условий работы арматуры при нахлёстке.
    /// </summary>
    public const double OverlapFactor = 0.90;

    /// <summary>
    /// Минимальная длина анкеровки/нахлёстки в мм.
    /// </summary>
    public const double MinAnchorLengthMm = 200.0;

    /// <summary>
    /// Минимальное количество диаметров для анкеровки.
    /// </summary>
    public const double MinAnchorDiameters = 15.0;

    /// <summary>
    /// Максимальный множитель нахлёстки (2.3 * lan).
    /// </summary>
    public const double MaxOverlapMultiplier = 2.3;

    /// <summary>
    /// Минимальный процент от базовой длины анкеровки (0.3 * l0an).
    /// </summary>
    public const double MinAnchorBaseFraction = 0.3;

    // ==================== Параметры арматуры по умолчанию ====================

    /// <summary>
    /// Минимальный отступ горизонтальной арматуры от края (мм).
    /// </summary>
    public const double MinOffsetMm = 50.0;

    /// <summary>
    /// Шаг шпилек по умолчанию (мм).
    /// </summary>
    public const double StudSpacingMm = 400.0;

    /// <summary>
    /// Защитный слой по краю (мм).
    /// </summary>
    public const double EdgeCoverMm = 35.0;

    /// <summary>
    /// Концевой защитный слой (мм).
    /// </summary>
    public const double EndCoverMm = 15.0;

    // ==================== Смещения для поиска ====================

    /// <summary>
    /// Смещение для поиска нижележащей стены (мм).
    /// </summary>
    public const double LowerWallOffsetMm = 500.0;

    /// <summary>
    /// Смещение для поиска вышележащей стены (мм).
    /// </summary>
    public const double UpperWallOffsetMm = 400.0;

    /// <summary>
    /// Смещение Z для проекции грани (мм).
    /// </summary>
    public const double FaceProjectionOffsetMm = 300.0;

    /// <summary>
    /// Малое смещение для проверки примыканий (мм).
    /// </summary>
    public const double JoinCheckOffsetMm = 10.0;

    /// <summary>
    /// Смещение для определения толщины примыкающей стены (мм).
    /// </summary>
    public const double JoinedWallThicknessOffsetMm = 20.0;

    /// <summary>
    /// GraphicsStyleId для фильтрации невидимых линий арматуры
    /// (проект-зависимое значение, может потребовать корректировки).
    /// </summary>
    public const int RebarHiddenGraphicsStyleId = 4062406;

    // ==================== Constraint handle names (русские, локализованные) ====================

    public const string HandleSegmentBar1 = "Сегмент стержня 1";
    public const string HandleBarStart = "Начало стержня";
    public const string HandleBarEnd = "Конец стержня";

    // ==================== Имя 3D вида по умолчанию ====================

    public const string DefaultWorkViewName = "3D - Смирнов - армирование стен_WorkView";
}
