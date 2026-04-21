using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Caching;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct Attachment
    {
        public PlanarFace Face { get; set; }
        public double FaceH { get; set; }
        public Element Concrete { get; set; }
        public PartitionCache PCache { get; set; }
        public XYZ Origin { get; set; }
        public List<XYZ> LintelOrigins { get; set; }
    }
}
