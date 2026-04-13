using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using WallReinforcer.Models;
using WallReinforcer.Services;
using WallReinforcer.Utilities;

namespace WallReinforcer.Services;

/// <summary>
/// Сервис для сбора параметров стен и их армирования.
/// Аналог Python-ноды d3314e31 из Dynamo-скрипта.
/// </summary>
public class WallDataCollector
{
    private readonly Document _doc;
    private readonly LevelService _levelService;
    private readonly WallGeometryService _geometryService;

    public WallDataCollector(Document doc, LevelService levelService, WallGeometryService geometryService)
    {
        _doc = doc;
        _levelService = levelService;
        _geometryService = geometryService;
    }

    /// <summary>
    /// Собрать WallParameters и WallReinforcement для всех переданных стен.
    /// </summary>
    /// <param name="walls">Список стен для обработки</param>
    /// <param name="workView">Рабочий 3D вид (для bounding box проёмов)</param>
    /// <returns>Кортеж (WallParameters, WallReinforcement) по каждому стеклу</returns>
    public (IList<WallParameters> WPars, IList<WallReinforcement> WRebs) CollectData(
        IList<Wall> walls,
        View3D workView)
    {
        var optForWalls = new Options { ComputeReferences = true };

        // Сортировка стен по elevation уровня
        var sortedWalls = walls
            .OrderBy(w => GetWallElevationMm(w))
            .ToList();

        var wPars = new List<WallParameters>();
        var wRebs = new List<WallReinforcement>();

        foreach (var wall in sortedWalls)
        {
            var wp = new WallParameters(_doc, wall, optForWalls,
                _levelService.GroupedLevels, _levelService.SortedLevels);

            var wr = new WallReinforcement(_doc, wall);

            wPars.Add(wp);
            wRebs.Add(wr);
        }

        return (wPars, wRebs);
    }

    private int GetWallElevationMm(Wall wall)
    {
        var lvlId = wall.LevelId;
        var lvl = _doc.GetElement(lvlId) as Level;
        if (lvl == null) return 0;
        return (int)System.Math.Round(UnitConverter.FtToMm(lvl.Elevation));
    }
}
