#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using WallReinforcer.Config;
using WallReinforcer.Models;
using WallReinforcer.Reinforcement;
using WallReinforcer.Services;

namespace WallReinforcer.Commands;

/// <summary>
/// Главная команда армирования стен.
/// Запускает полный цикл: выбор стен → сбор данных → создание арматуры.
/// </summary>
[Transaction(TransactionMode.Manual)]
public class WallReinforcementCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiApp = commandData.Application;
        var uiDoc = uiApp.ActiveUIDocument;
        var doc = uiDoc.Document;

        if (doc == null)
        {
            TaskDialog.Show("Ошибка", "Нет открытого документа.");
            return Result.Failed;
        }

        // 1. Выбор стен
        var wallIds = SelectWalls(uiDoc);
        if (wallIds == null || wallIds.Count == 0)
        {
            TaskDialog.Show("Отмена", "Стены не выбраны.");
            return Result.Cancelled;
        }

        // 2. Выбор рабочего вида
        var workView = GetOrCreateWorkView(doc);
        if (workView == null)
        {
            TaskDialog.Show("Ошибка", "Не удалось найти или создать рабочий 3D вид.");
            return Result.Failed;
        }

        // 3. Инициализация сервисов
        var levelService = new LevelService(doc);
        var geometryService = new WallGeometryService(doc);
        var dataCollector = new WallDataCollector(doc, levelService, geometryService);
        var anchorService = new WallAnchorService(doc, workView);
        var hRebarCreator = new HorizontalRebarCreator(doc, geometryService);
        var vRebarCreator = new VerticalRebarCreator(doc, geometryService);
        var tieBarCreator = new TieBarCreator(doc);
        var uHoopCreator = new UHoopCreator(doc);
        var uvHoopModifier = new UVHoopModifier(doc);
        var coverAdjuster = new CoverAdjuster(doc);
        var studDeleter = new StudDeleter(doc);

        // 4. Сбор данных по стенам
        var walls = wallIds.Select(id => doc.GetElement(id) as Wall)
            .Where(w => w != null)
            .Cast<Wall>()
            .ToList();

        var (wPars, wRebs) = dataCollector.CollectData(walls, workView);

        // 5. Основной цикл в транзакции
        using (var t = new Transaction(doc, "Армирование стен"))
        {
            t.Start();

            try
            {
                for (int i = 0; i < wPars.Count; i++)
                {
                    var wp = wPars[i];
                    var wr = wRebs[i];

                    // 5a. Удаление старых шпилек
                    studDeleter.DeleteOldStuds(new List<WallParameters> { wp });

                    // 5b. Обновление защитного слоя
                    coverAdjuster.UpdateCoverTypes(wp, wr,
                        ReinforcementConfig.EdgeCoverMm,
                        ReinforcementConfig.EndCoverMm);

                    // 5c. Поиск анкеров
                    var wancs = anchorService.GetWallAnchors(wp, wr);

                    // 5d. Создание вертикальной арматуры
                    var verticalRebars = vRebarCreator.CreateVRebars(wp, wr, wancs, workView);

                    // 5e. Обновление constraints вертикальной арматуры
                    if (verticalRebars.Count > 0)
                    {
                        coverAdjuster.UpdateRebarConstraints(verticalRebars, wp, wr);
                    }

                    // 5f. Создание горизонтальной арматуры (AreaReinforcement)
                    var arList = CreateHorizontalReinforcementForWall(wp, wr, levelService, hRebarCreator);

                    // 5g. Создание П-образных горизонтальных хомутов
                    foreach (var ar in arList)
                    {
                        uHoopCreator.CreateUHRebars(ar, wp, wr, ReinforcementConfig.ShapeUHoop);
                    }

                    // 5h. Создание шпилек
                    if (arList.Count > 0 && verticalRebars.Count > 0)
                    {
                        tieBarCreator.CreateSRebars(wp, wr, arList[0], verticalRebars,
                            ReinforcementConfig.StudSpacingMm);
                    }
                }

                t.Commit();
                TaskDialog.Show("Успех", $"Армирование выполнено для {wPars.Count} стен(ы).");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                t.RollBack();
                message = $"Ошибка при армировании: {ex.Message}";
                TaskDialog.Show("Ошибка", message);
                return Result.Failed;
            }
        }
    }

    #region Helpers

    /// <summary>
    /// Выбрать стены пользователем.
    /// </summary>
    private IList<ElementId>? SelectWalls(UIDocument uiDoc)
    {
        try
        {
            var selected = uiDoc.Selection.PickObjects(
                ObjectType.Element,
                new WallSelectionFilter(),
                "Выберите стены для армирования (Esc — отмена)");

            return selected.Select(r => r.ElementId).ToList();
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Найти рабочий 3D вид или создать его.
    /// </summary>
    private View3D? GetOrCreateWorkView(Document doc)
    {
        // Сначала ищем вид по умолчанию
        var existing = new FilteredElementCollector(doc)
            .OfClass(typeof(View3D))
            .Cast<View3D>()
            .FirstOrDefault(v => !v.IsTemplate && v.Name.Contains("армирован"));

        if (existing != null) return existing;

        // Если не нашли — возвращаем активный 3D вид
        return doc.ActiveView as View3D;
    }

    /// <summary>
    /// Создать горизонтальную арматуру для всех уровней стены.
    /// </summary>
    private IList<AreaReinforcement> CreateHorizontalReinforcementForWall(
        WallParameters wp,
        WallReinforcement wr,
        LevelService levelService,
        HorizontalRebarCreator hRebarCreator)
    {
        var results = new List<AreaReinforcement>();

        for (int j = 0; j < levelService.GroupedLevels.Count - 1; j++)
        {
            var baseLvl = levelService.GroupedLevels[j][0];
            var topLvl = levelService.GroupedLevels[j + 1][0];
            var z = wp.GetBaseZ() - wp.BaseOffset;

            if (z >= baseLvl.Elevation && z <= topLvl.Elevation)
            {
                var arList = hRebarCreator.CreateHRebars(wp, wr, baseLvl, topLvl,
                    ReinforcementConfig.MinOffsetMm);
                results.AddRange(arList);
                break;
            }
        }

        return results;
    }

    #endregion

    /// <summary>
    /// Фильтр выбора только стен.
    /// </summary>
    private class WallSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Wall;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
