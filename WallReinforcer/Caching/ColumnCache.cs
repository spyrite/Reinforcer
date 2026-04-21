using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Caching
{
    public class ColumnCache : RebarHostCache
    {
        public ColumnCache(Element elem) : base(elem)
        {
            Column = elem as FamilyInstance;
            Geom = new GeometryColumnCache(Column);
        }

        public FamilyInstance Column { get; set; }
    }
}
