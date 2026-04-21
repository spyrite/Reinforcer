using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Caching
{
    public class BeamCache : RebarHostCache
    {
        public BeamCache(Element elem) : base(elem)
        {
            Beam = elem as FamilyInstance;
            Geom = new GeometryBeamCache(Beam);
        }

        public FamilyInstance Beam { get; set; }
    }
}
