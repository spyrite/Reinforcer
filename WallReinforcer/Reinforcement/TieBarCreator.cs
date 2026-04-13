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
/// Создание шпилек (зигзагообразных связей) между наружным и внутренним рядами арматуры.
/// Аналог Python-ноды af85eab3 из Dynamo-скрипта.
/// </summary>
public class TieBarCreator
{
    private readonly Document _doc;

    public TieBarCreator(Document doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Создать шпильки для стены.
    /// </summary>
    /// <param name="wp">Параметры стены</param>
    /// <param name="wr">Параметры армирования</param>
    /// <param name="ar">AreaReinforcement для получения горизонтальных уровней</param>
    /// <param name="verticalRebars">Вертикальная арматура стены</param>
    /// <param name="studSpacingMm">Шаг шпилек (мм)</param>
    /// <returns>Созданные шпильки</returns>
    public IList<Rebar> CreateSRebars(
        WallParameters wp,
        WallReinforcement wr,
        AreaReinforcement ar,
        IList<Rebar> verticalRebars,
        double studSpacingMm)
    {
        var shape = GetRebarShape(ReinforcementConfig.ShapeStud);
        var rebarType = wr.GetStudRebarType();
        if (shape == null || rebarType == null)
            return new List<Rebar>();

        var host = _doc.GetElement(wp.Id) as Wall;
        if (host == null) return new List<Rebar>();

        var origins = GetOriginsForSRebars(ar, verticalRebars, wp, studSpacingMm);
        var results = new List<Rebar>();

        foreach (var pointSet in origins)
        {
            foreach (var point in pointSet.Points)
            {
                var rsp = new RebarSParameters(wp, wr, point);
                var placePoint = rsp.Origin + wp.Dir * ((wr.DY1) / 2.0 + wr.DStud);

                var rebar = Rebar.CreateFromRebarShape(_doc, shape, rebarType, host, placePoint, rsp.XVec, rsp.YVec);
                if (rebar == null) continue;

                _doc.Regenerate();

                rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LAYOUT_RULE)?.Set(3);
                rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.Set(pointSet.N);

                if (pointSet.N > 1 && pointSet.Step.HasValue)
                    rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.Set(pointSet.Step.Value);

                rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(pointSet.Partition);

                EditConstraints(rebar);
                results.Add(rebar);
            }

            _doc.Regenerate();
        }

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

    /// <summary>
    /// Получить точки размещения шпилек из пересечения вертикальной и горизонтальной арматуры.
    /// </summary>
    private IList<StudOriginSet> GetOriginsForSRebars(
        AreaReinforcement ar,
        IList<Rebar> allRebarsV,
        WallParameters wp,
        double studSpacingMm)
    {
        // Получаем Z-координаты горизонтальных стержней из AreaReinforcement
        var rebarsH = ar.GetRebarInSystemIds()
            .Select(id => _doc.GetElement(id) as RebarInSystem)
            .Where(r => r != null)
            .ToList();

        var zSet = new SortedSet<double>();

        foreach (var rebarH in rebarsH)
        {
            var locZ = GetRebarLocation(rebarH).Z;
            for (int i = 0; i < rebarH.NumberOfBarPositions; i++)
            {
                var z = rebarH.GetBarPositionTransform(i).Origin.Z + locZ;
                zSet.Add(System.Math.Round(z, 6));
            }
        }

        var zSorted = zSet.ToList();
        var zOdd = new List<double>();   // Нечётный ряд
        var zEven = new List<double>();  // Чётный ряд

        for (int i = 0; i < zSorted.Count; i++)
        {
            if (i % 2 == 0)
                zOdd.Add(zSorted[i]);
            else
                zEven.Add(zSorted[i]);
        }

        // Фильтруем вертикальные стержни на внутренней стороне
        var rebarsVInterior = GetRebarsForInteriorSide(allRebarsV, wp);

        var origins = new List<StudOriginSet>();

        foreach (var rebarV in rebarsVInterior)
        {
            var line = rebarV.GetCenterlineCurves(false, true, true,
                MultiplanarOption.IncludeOnlyPlanarCurves, 0).Cast<Curve>().FirstOrDefault() as Line;
            if (line == null) continue;

            var x = line.Origin.X;
            var y = line.Origin.Y;

            bool isOnFloor = RebarIsOnFloor(rebarV, wp);
            var points = isOnFloor
                ? zOdd.Select(z => new XYZ(x, y, z)).ToList()
                : zEven.Select(z => new XYZ(x, y, z)).ToList();

            var partition = isOnFloor
                ? ReinforcementPartitionNames.reinfPartName_Studs_OddRow
                : ReinforcementPartitionNames.reinfPartName_Studs_EvenRow;

            var stepVMm = System.Math.Round((rebarV.get_Parameter(BuiltInParameter.REBAR_ELEM_BAR_SPACING)?.AsDouble() ?? 0) * UnitConverter.FtToMm(1.0));
            var k = studSpacingMm / stepVMm;
            var n = stepVMm > 0 ? (int)System.Math.Ceiling((rebarV.get_Parameter(BuiltInParameter.REBAR_ELEM_QUANTITY_OF_BARS)?.AsInteger() ?? 1) / k) : 1;

            double? step = n > 1 ? UnitConverter.MmToFt(studSpacingMm) : null;

            origins.Add(new StudOriginSet(points, n, step, partition));
        }

        return origins;
    }

    /// <summary>
    /// Фильтровать вертикальную арматуру, оставляя только стержни на внутренней стороне стены.
    /// </summary>
    private IList<Rebar> GetRebarsForInteriorSide(IList<Rebar> allRebarsV, WallParameters wp)
    {
        var extFace = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(wp.Orient));
        var intFace = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(-wp.Orient));

        if (extFace == null || intFace == null) return new List<Rebar>();

        var results = new List<Rebar>();

        foreach (var rebar in allRebarsV)
        {
            var curves = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
            var line = curves.Cast<Curve>().FirstOrDefault() as Line;
            if (line == null) continue;

            var p0 = line.GetEndPoint(0) + XYZ.BasisZ * UnitConverter.MmToFt(ReinforcementConfig.FaceProjectionOffsetMm);

            var extProj = extFace.Project(p0);
            var intProj = intFace.Project(p0);

            if (extProj == null || intProj == null) continue;

            var extDst = p0.DistanceTo(extProj.XYZPoint);
            var intDst = p0.DistanceTo(intProj.XYZPoint);

            if (extDst > intDst)
                results.Add(rebar);
        }

        return results;
    }

    /// <summary>
    /// Проверить, стоит ли арматура на плите.
    /// </summary>
    private bool RebarIsOnFloor(Rebar rebar, WallParameters wp)
    {
        var curves = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
        var line = curves.Cast<Curve>().FirstOrDefault() as Line;
        if (line == null) return true;

        var p0 = line.GetEndPoint(0);

        // Проверяем расстояние до нижней грани стены
        var bottomFaces = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .Where(f => f.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
            .ToList();

        foreach (var bf in bottomFaces)
        {
            var proj = bf.Project(p0);
            if (proj != null)
            {
                var dst = System.Math.Round(p0.DistanceTo(proj.XYZPoint) * UnitConverter.FtToMm(1.0));
                return dst == 0;
            }
        }

        return false;
    }

    private XYZ GetRebarLocation(RebarInSystem rebar)
    {
        var curves = rebar.GetCenterlineCurves(false, true, true);
        var line = curves.Cast<Curve>().FirstOrDefault();
        return line?.GetEndPoint(0) ?? XYZ.Zero;
    }

    /// <summary>
    /// Установить нулевой защитный слой на концах стержня.
    /// </summary>
    private void EditConstraints(Rebar rebar)
    {
        var conMan = rebar.GetRebarConstraintsManager();
        if (conMan == null) return;

        // Начало стержня
        var handleStart = conMan.GetAllHandles()
            .FirstOrDefault(h => h.GetHandleName() == ReinforcementConfig.HandleBarStart);

        if (handleStart != null)
        {
            var consts = conMan.GetConstraintCandidatesForHandle(handleStart);
            var constraint = consts.FirstOrDefault(c =>
                c.GetRebarConstraintTargetHostFaceType() == RebarConstraintTargetHostFaceType.Side0
                && c.IsToCover());

            if (constraint != null)
            {
                constraint.SetDistanceToTargetCover(0);
                conMan.SetPreferredConstraintForHandle(handleStart, constraint);
            }
        }

        // Конец стержня
        var handleEnd = conMan.GetAllHandles()
            .FirstOrDefault(h => h.GetHandleName() == ReinforcementConfig.HandleBarEnd);

        if (handleEnd != null)
        {
            var consts = conMan.GetConstraintCandidatesForHandle(handleEnd);
            var constraint = consts.FirstOrDefault(c =>
                c.GetRebarConstraintTargetHostFaceType() == RebarConstraintTargetHostFaceType.Side1
                && c.IsToCover());

            if (constraint != null)
            {
                constraint.SetDistanceToTargetCover(0);
                conMan.SetPreferredConstraintForHandle(handleEnd, constraint);
            }
        }

        _doc.Regenerate();
    }

    #endregion

    /// <summary>
    /// Набор точек для размещения шпилек.
    /// </summary>
    private class StudOriginSet
    {
        public IList<XYZ> Points { get; }
        public int N { get; }
        public double? Step { get; }
        public string Partition { get; }

        public StudOriginSet(IList<XYZ> points, int n, double? step, string partition)
        {
            Points = points;
            N = n;
            Step = step;
            Partition = partition;
        }
    }
}
