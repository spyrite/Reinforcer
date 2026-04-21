using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Caching
{
    public class CICache
    {
        private protected Document doc;

        public CICache(Element elem)
        {
            doc = elem.Document;
            CI = elem as FamilyInstance;
            Geom = new GeometryFamilyInstanceCache(CI);
            if (!(elem.GroupId == ElementId.InvalidElementId))
                CIGroupOrigin = ((doc.GetElement(elem.GroupId) as Group).Location as LocationPoint).Point;
            else
                CIGroupOrigin = null;
        }

        public FamilyInstance CI { get; set; }
        public GeometryCache Geom { get; set; }
        public XYZ CIGroupOrigin { get; set; }
    }
}
