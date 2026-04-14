using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitOSA.CoreMain.Caching;
using RevitOSA.WallReinforcer.Revit.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Revit
{
    public class PerformWallReinforce : IExternalCommand
    {
        public static UIApplication UIApp;
        public static UIDocument UIDoc;
        public static Autodesk.Revit.ApplicationServices.Application App;
        public static Document Doc;

        private List<ElementId> _selectedElementIds;
        private List<WallCache> _wCaches;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Инициализация
            UIApp = commandData.Application;
            UIDoc = commandData.Application.ActiveUIDocument;
            App = commandData.Application.Application;
            Doc = commandData.Application.ActiveUIDocument.Document;
            _selectedElementIds = [.. UIDoc.Selection.GetElementIds()];
            _wCaches = [];

            //Выбор стен
            _wCaches = [.. _selectedElementIds.Select(id => Doc.GetElement(id)).Where(elem => elem is Wall).Select(elem => new WallCache(elem))];

            if (!_wCaches.Any())
                try
                {
                    _wCaches = [.. UIDoc.Selection.PickObjects(ObjectType.Element, new WallSelectionFilter(),
                        "Выберите армируемые стены").Select(r => new WallCache(Doc.GetElement(r)))];
                }
                catch { return Result.Cancelled; }


            

            return Result.Succeeded;
        }
    }
}
