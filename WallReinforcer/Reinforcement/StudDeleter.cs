#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using WallReinforcer.Models;

namespace WallReinforcer.Reinforcement;

/// <summary>
/// Удаление старых шпилек с partition name, начинающимся с "06_Шпильки".
/// Аналог Python-ноды 15d90f55 из Dynamo-скрипта.
/// </summary>
public class StudDeleter
{
    private readonly Document _doc;

    public StudDeleter(Document doc)
    {
        _doc = doc;
    }

    /// <summary>
    /// Удалить все шпильки для переданных стен.
    /// </summary>
    public int DeleteOldStuds(IList<WallParameters> wallParams)
    {
        var idsToDelete = new List<ElementId>();

        foreach (var wp in wallParams)
        {
            var wall = _doc.GetElement(wp.Id) as Wall;
            if (wall == null) continue;

            var rebarIds = wall.GetDependentElements(new ElementClassFilter(typeof(Rebar)));

            foreach (var rebarId in rebarIds)
            {
                if (_doc.GetElement(rebarId) is Rebar rebar)
                {
                    var partition = rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM)?.AsString();
                    if (!string.IsNullOrEmpty(partition) && partition.StartsWith("06_Шпильки"))
                    {
                        idsToDelete.Add(rebar.Id);
                    }
                }
            }
        }

        if (idsToDelete.Count > 0)
            _doc.Delete(idsToDelete);

        return idsToDelete.Count;
    }
}
