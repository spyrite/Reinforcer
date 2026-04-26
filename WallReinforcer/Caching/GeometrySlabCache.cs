using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometrySlabCache : GeometryCache
    {
        public GeometrySlabCache(Floor slab) : base(slab)
        {
            // 1. Получаем явные параметры и геометрию
            double thickness = slab.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM).AsDouble();
            double spanAngle = slab.SpanDirectionAngle;
            
            XYZ normalTop = slab.GetNormalAtVerticalProjectionPoint(new XYZ(), FloorFace.Top);
            XYZ normalBottom = slab.GetNormalAtVerticalProjectionPoint(new XYZ(), FloorFace.Bottom);
            
            // 2. Инициализация направлений
            Transform transform = Transform.CreateRotation(XYZ.BasisZ, spanAngle);
            Dirs = new Directions
            {
                X = transform.OfVector(XYZ.BasisX),
                Y = transform.OfVector(XYZ.BasisY),
                Z = normalTop
            };

            // 3. Расчет размеров и координат
            double zBot = slab.GetVerticalProjectionPoint(new XYZ(), FloorFace.Bottom).Z;
            double zTop = slab.GetVerticalProjectionPoint(new XYZ(), FloorFace.Top).Z;

            Dims = new ControlDimensions
            {
                T = thickness,
                ZBot = zBot,
                ZTop = zTop
            };

            // 4. Инициализация уровней
            LvlIds = new LevelIds
            {
                Base = slab.LevelId
            };

            // 5. BoundingBox (если доступен)
            Outline = new Outline(slab.get_BoundingBox(null).Min, slab.get_BoundingBox(null).Max);
        }

        public override void GetSolidData()
        {
            base.GetSolidData();
            GetExtendedFacesData();
        }
    }
}
