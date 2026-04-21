using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct AttachedElements
    {
        public List<Element> OnTop { get; set; }
        public List<Element> OnBottom { get; set; }
        public List<Element> OnLeft { get; set; }
        public List<Element> OnRight { get; set; }
    }
}
