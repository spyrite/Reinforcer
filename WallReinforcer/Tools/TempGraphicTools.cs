using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class TempGraphicTools
    {
        public static void CreateDirectShapeFromGeomCache(Document doc, GeometryCache geomCache)
        {
            DirectShape tempHost = DirectShape.CreateElement(doc, geomCache.Elem.Category.Id);
            tempHost.SetShape(new List<GeometryObject> { geomCache.Solid });
            doc.Regenerate();
        }
        public static void CreateDirectShapeFromSolid(Document doc, Solid solid)
        {
            Category cat = Category.GetCategory(doc, BuiltInCategory.OST_GenericModel);
            DirectShape tempHost = DirectShape.CreateElement(doc, cat.Id);
            tempHost.SetShape(new List<GeometryObject> { solid });
            doc.Regenerate();
        }
        public static DirectShape CreateDirectShapeFromSolid(Document doc, Solid solid, BuiltInCategory category)
        {
            Category cat = Category.GetCategory(doc, category);
            DirectShape tempHost = DirectShape.CreateElement(doc, cat.Id);
            tempHost.SetShape(new List<GeometryObject> { solid });
            doc.Regenerate();
            return tempHost;
        }
        public static void CreateLine(Document doc, XYZ p0, XYZ p1, XYZ yVec)
        {
            Line line = Line.CreateBound(p0, p1);
            Plane plane = Plane.CreateByNormalAndOrigin(yVec, p0);
            SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
            doc.Create.NewModelCurve(line, sketchPlane);
        }
    }
}
