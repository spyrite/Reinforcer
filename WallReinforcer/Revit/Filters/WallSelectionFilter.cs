using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Revit.Filters
{
    internal class WallSelectionFilter : ISelectionFilter
    {

        private List<ElementId> _excludingIds;

        public WallSelectionFilter()
        {
            _excludingIds = [];
        }

        public WallSelectionFilter(List<ElementId> excludingIds)
        {
            _excludingIds = excludingIds;
        }


        public bool AllowElement(Element elem)
        {
            return elem is Wall
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
