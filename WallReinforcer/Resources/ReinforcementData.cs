using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct ReinforcementData
    {
        public List<Curve> CenterLines { get; set; }
        public double OffsetDst { get; set; }
        public double D { get; set; }
        public double Step { get; set; }
        public int N { get; set; }
        public string RClass { get; set; }
        public double Bend { get; set; }
        public XYZ Dir { get; set; }
        public XYZ StartPoint { get; set; }
        public RebarBarType BarType { get; set; }
        public bool InRunningMeters { get; set; }
    }
}
