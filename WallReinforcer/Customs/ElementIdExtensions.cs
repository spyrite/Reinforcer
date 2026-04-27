using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Customs
{
    public static class ElementIdExtensions
    {
        public static Element GetElement(this ElementId id, Document doc) => doc.GetElement(id);
    }
}
