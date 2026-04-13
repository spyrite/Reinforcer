using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using WallReinforcer.Utilities;

namespace WallReinforcer.Services;

/// <summary>
/// Сервис для работы с уровнями: сортировка, группировка, поиск предыдущего уровня.
/// </summary>
public class LevelService
{
    /// <summary>
    /// Уровни, сгруппированные по этажности (по точке в имени).
    /// levels[0] = все подуровни первого этажа, levels[1] = второго, и т.д.
    /// </summary>
    public IList<IList<Level>> GroupedLevels { get; }

    /// <summary>
    /// Уровни, отсортированные по отметке (плоский список).
    /// </summary>
    public IList<Level> SortedLevels { get; }

    public LevelService(Document doc)
    {
        var allLevels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .ToList();

        SortedLevels = allLevels
            .OrderBy(l => System.Math.Round(UnitConverter.FtToMm(l.Elevation)))
            .ToList();

        GroupedLevels = GroupLevelsByName(SortedLevels);
    }

    /// <summary>
    /// Сгруппировать уровни по имени: если имя содержит '.', это подуровень
    /// основного этажа. Каждый основной этаж — отдельная группа.
    /// </summary>
    private static IList<IList<Level>> GroupLevelsByName(IList<Level> levels)
    {
        var groups = new List<IList<Level>>();

        foreach (var lvl in levels)
        {
            var nameParts = lvl.Name.Split('.');
            if (nameParts.Length == 1)
            {
                // Новый основной этаж
                groups.Add(new List<Level> { lvl });
            }
            else
            {
                // Подуровень — добавляем в последнюю группу
                if (groups.Count > 0)
                    groups[groups.Count - 1].Add(lvl);
                else
                    groups.Add(new List<Level> { lvl });
            }
        }

        return groups;
    }
}
