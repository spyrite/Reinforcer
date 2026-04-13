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
/// Корректировка защитного слоя арматуры (RebarCoverType) и constraints.
/// Аналог Python-ноды 245e9ae0 из Dynamo-скрипта.
/// </summary>
public class CoverAdjuster
{
    private readonly Document _doc;

    public CoverAdjuster(Document doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Обновить типы защитного слоя для стены на основе вычисленных расстояний.
    /// </summary>
    /// <param name="wp">Параметры стены</param>
    /// <param name="wr">Параметры армирования</param>
    /// <param name="edgeCoverMm">Защитный слой по краю (мм)</param>
    /// <param name="endCoverMm">Концевой защитный слой (мм)</param>
    public void UpdateCoverTypes(WallParameters wp, WallReinforcement wr, double edgeCoverMm, double endCoverMm)
    {
        var wall = _doc.GetElement(wp.Id) as Wall;
        if (wall == null) return;

        var dst1 = UnitConverter.MmToFt(edgeCoverMm) - wr.DX / 2.0; // Сторона с учётом диаметра
        var dst2 = UnitConverter.MmToFt(endCoverMm);                  // Прочий

        var covType1 = GetOrCreateCoverType(dst1);
        var covType2 = GetOrCreateCoverType(dst2);

        // Обновляем параметры защитного слоя на стене
        if (System.Math.Round(wr.CovExt * UnitConverter.FtToMm(1.0)) != System.Math.Round(dst1 * UnitConverter.FtToMm(1.0)))
        {
            wr.CovExt = dst1;
            wall.get_Parameter(BuiltInParameter.CLEAR_COVER_EXTERIOR)?.Set(covType1.Id);
        }

        if (System.Math.Round(wr.CovInt * UnitConverter.FtToMm(1.0)) != System.Math.Round(dst1 * UnitConverter.FtToMm(1.0)))
        {
            wr.CovInt = dst1;
            wall.get_Parameter(BuiltInParameter.CLEAR_COVER_INTERIOR)?.Set(covType1.Id);
        }

        if (System.Math.Round(wr.CovOth * UnitConverter.FtToMm(1.0)) != System.Math.Round(dst2 * UnitConverter.FtToMm(1.0)))
        {
            wr.CovOth = dst2;
            wall.get_Parameter(BuiltInParameter.CLEAR_COVER_OTHER)?.Set(covType2.Id);
        }
    }

    /// <summary>
    /// Обновить constraints для вертикальной арматуры — привязка к наружной или внутренней грани.
    /// </summary>
    public void UpdateRebarConstraints(IList<Rebar> rebars, WallParameters wp, WallReinforcement wr)
    {
        foreach (var rebar in rebars)
        {
            var conMan = rebar.GetRebarConstraintsManager();
            if (conMan == null) continue;

            // Ищем handle "Сегмент стержня 1"
            var handles = conMan.GetAllHandles();
            var segmentHandles = handles.Where(h => h.GetHandleName() == ReinforcementConfig.HandleSegmentBar1).ToList();

            foreach (var hand in segmentHandles)
            {
                var consts = conMan.GetConstraintCandidatesForHandle(hand);
                var (side, _) = GetSideAndPartition(rebar, wp);

                var constraint = consts.FirstOrDefault(c =>
                    c.GetRebarConstraintTargetHostFaceType() == side
                    && c.IsToCover());

                if (constraint != null)
                {
                    constraint.SetDistanceToTargetCover(-wr.DX);
                    conMan.SetPreferredConstraintForHandle(hand, constraint);
                }
            }

            // Устанавливаем partition
            var (_, partitionName) = GetSideAndPartition(rebar, wp);
            rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.Set(partitionName);
        }

        _doc.Regenerate();
    }

    #region Private helpers

    private RebarCoverType GetOrCreateCoverType(double distanceFt)
    {
        var existing = new FilteredElementCollector(_doc)
            .OfClass(typeof(RebarCoverType))
            .Cast<RebarCoverType>()
            .FirstOrDefault(ct => System.Math.Round(ct.CoverDistance * UnitConverter.FtToMm(1.0)) ==
                                  System.Math.Round(distanceFt * UnitConverter.FtToMm(1.0)));

        if (existing != null) return existing;

        var name = System.Math.Round(distanceFt * UnitConverter.FtToMm(1.0)).ToString();
        return RebarCoverType.Create(_doc, name, distanceFt);
    }

    /// <summary>
    /// Определить, на какой стороне (ext/int) находится арматура.
    /// </summary>
    private (RebarConstraintTargetHostFaceType Side, string PartitionName) GetSideAndPartition(Rebar rebar, WallParameters wp)
    {
        var extFace = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(wp.Orient));
        var intFace = wp.Solid.Faces.Cast<Face>().OfType<PlanarFace>()
            .FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(-wp.Orient));

        var curves = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
        var line = curves.Cast<Curve>().FirstOrDefault() as Line;

        if (line == null || extFace == null || intFace == null)
            return (RebarConstraintTargetHostFaceType.Side0, ReinforcementPartitionNames.reinfPartName_VertInside);

        var p0 = line.GetEndPoint(0) + XYZ.BasisZ * UnitConverter.MmToFt(ReinforcementConfig.FaceProjectionOffsetMm);

        var extProj = extFace.Project(p0);
        var intProj = intFace.Project(p0);

        if (extProj == null || intProj == null)
            return (RebarConstraintTargetHostFaceType.Side0, ReinforcementPartitionNames.reinfPartName_VertInside);

        var extDst = p0.DistanceTo(extProj.XYZPoint);
        var intDst = p0.DistanceTo(intProj.XYZPoint);

        if (extDst < intDst)
            return (RebarConstraintTargetHostFaceType.Side0, ReinforcementPartitionNames.reinfPartName_VertOutside);
        else
            return (RebarConstraintTargetHostFaceType.Side1, ReinforcementPartitionNames.reinfPartName_VertInside);
    }

    #endregion
}
