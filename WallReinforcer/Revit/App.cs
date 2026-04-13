using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace RevitOSA.WallReinforcer.Revit
{

    public class App : IExternalApplication
    {
        private RibbonPanel ribbonPanel;
        public Result OnStartup(UIControlledApplication application)
        {

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }

}