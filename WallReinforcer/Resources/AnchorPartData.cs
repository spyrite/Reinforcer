using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct AnchorPartData
    {
        public List<Curve> Lines;
        public XYZ BottomPoint;
        public XYZ TopPoint;
        public double Length;
    }
}
