using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometrySlabCache : GeometryCache
    {
        public GeometrySlabCache(Floor slab) : base(slab)
        {
            Transform transform = Transform.CreateRotation(XYZ.BasisZ, slab.SpanDirectionAngle);
            Dirs = new Directions
            {
                X = transform.OfVector(XYZ.BasisX),
                Y = transform.OfVector(XYZ.BasisY),
                Z = slab.GetNormalAtVerticalProjectionPoint(new XYZ(), FloorFace.Top)
            };
            Dims = new ControlDimensions
            {
                T = slab.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble(),
                ZBot = slab.GetVerticalProjectionPoint(new XYZ(), FloorFace.Bottom).Z,
                ZTop = slab.GetVerticalProjectionPoint(new XYZ(), FloorFace.Top).Z,
            };
            LvlIds = new LevelIds
            {
                Base = slab.LevelId
            };

        }

        public override void GetSolidData()
        {
            base.GetSolidData();
            GetExtendedFacesData();
        }
    }
}
