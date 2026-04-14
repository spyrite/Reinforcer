using Autodesk.Revit.DB;
using RevitOSA.CoreMain.FB;
using RevitOSA.CoreMain.Assistants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Line = Autodesk.Revit.DB.Line;
using Transform = Autodesk.Revit.DB.Transform;
using View = Autodesk.Revit.DB.View;
using Parameter = Autodesk.Revit.DB.Parameter;
using AnnSettings = RevitOSA.CoreSettings.Properties.Annotations;
using ReinfSettings = RevitOSA.CoreSettings.Properties.Reinforcement;
using ModelSettings = RevitOSA.CoreSettings.Properties.Modelling;
using Autodesk.Revit.DB.Architecture;

#if COMPANY_FP
using RevitOSA.CoreSettings.ResourcesFP;
using static RevitOSA.CoreSettings.ResourcesFP.RevitParameters;

#elif COMPANY_FP
using static RevitOSA.CoreSettings.ResourcesOLP.RevitParameters;
using RevitOSA.CoreSettings.ResourcesOLP;

#else
#endif

namespace RevitOSA.CoreMain.Caching
{
    public class GeometryCache
    {
        //Поля
        private protected static Document doc;
        private protected static ElementFilter openingFilter = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)),
            new LogicalOrFilter(new ElementCategoryFilter(BuiltInCategory.OST_Windows), new ElementCategoryFilter(BuiltInCategory.OST_Doors)));
        private protected List<object> preCalculations;

        //Свойства
        public Element Elem { get; set; }
        public LevelIds LvlIds { get; set; }
        public Directions Dirs { get; set; }
        public Solid Solid { get; set; }
        public ElemFaces Faces { get; set; }
        public UnboundElemFaces UnboundFaces { get; set; }
        public ControlDimensions Dims { get; set; }
        public ControlPoints Origins { get; set; }
        public ControlLines Lines { get; set; }
        public BasePoint BasePoint { get; set; }
        public Outline Outline { get; set; }

        // Конструкторы
        public GeometryCache() { }
        public GeometryCache(Element elem)
        {
            doc = elem.Document;
#if R2024 || R2025
            BasePoint = BasePoint.GetProjectBasePoint(doc);
#else
            BasePoint = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).FirstElement() as BasePoint;
#endif
            Elem = elem;
        }

        // Методы
        public virtual void GetSolidData()
        {
            Solid = GeometryTools.GetSolid(Elem, false);
            GetPrimaryFacesData();
        }
        public virtual void GetSolidData(Document targetDoc)
        {
            Solid = GeometryTools.GetSolidInTargetDoc(Elem, targetDoc);
            GetPrimaryFacesData();
        }
        public List<FamilyInstance> GetOpenings()
        {
            List<FamilyInstance> openings = new List<FamilyInstance>();
            foreach (ElementId id in Elem.GetDependentElements(openingFilter)) openings.Add(doc.GetElement(id) as FamilyInstance);
            return openings;
        }

        private protected void GetPrimaryFacesData()
        {
            List<PlanarFace> planarFaces = new List<PlanarFace>();
            foreach (Face face in Solid.Faces) { if (face is PlanarFace) planarFaces.Add(face as PlanarFace); }
            ;
            Faces = new ElemFaces() { All = new List<PlanarFace>() };
            foreach (Face face in Solid.Faces) { if (face is PlanarFace) Faces.All.Add(face as PlanarFace); }
            ;

            UnboundFaces = new UnboundElemFaces()
            { All = (from face in Faces.All select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList() };
        }

        private protected void GetExtendedFacesData()
        {
            Faces.Top = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(Dirs.Z));
            Faces.Bottom = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(-Dirs.Z));
            Faces.Left = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(-Dirs.X));
            Faces.Right = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(Dirs.X));
            Faces.SideShort = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(Dirs.X) | face.FaceNormal.IsAlmostEqualTo(-Dirs.X));
            Faces.SideLong = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(Dirs.Y) | face.FaceNormal.IsAlmostEqualTo(-Dirs.Y));
            Faces.Sides = Faces.All.FindAll(face => !face.FaceNormal.IsAlmostEqualTo(Dirs.Z) & !face.FaceNormal.IsAlmostEqualTo(-Dirs.Z));
            Faces.FrontContour = Faces.All.FindAll(face => !face.FaceNormal.IsAlmostEqualTo(Dirs.Y) & !face.FaceNormal.IsAlmostEqualTo(-Dirs.Y));
            Faces.Front = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(-Dirs.Y));
            Faces.Rear = Faces.All.FindAll(face => face.FaceNormal.IsAlmostEqualTo(Dirs.Y));

            UnboundFaces.Top = (from face in Faces.Top select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.Bottom = (from face in Faces.Bottom select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.Left = (from face in Faces.Left select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.Right = (from face in Faces.Right select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.SideShort = (from face in Faces.SideShort select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.SideLong = (from face in Faces.SideLong select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.Sides = (from face in Faces.Sides select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.FrontContour = (from face in Faces.FrontContour select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.Front = (from face in Faces.Front select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
            UnboundFaces.Rear = (from face in Faces.Rear select Plane.CreateByNormalAndOrigin(face.FaceNormal, face.Origin)).ToList();
        }


        // Структуры и подклассы
        public struct Directions
        {
            public XYZ X { get; set; }
            public XYZ Y { get; set; }
            public XYZ Z { get; set; }
        }
        public class ElemFaces
        {
            public List<PlanarFace> Top { get; set; }
            public List<PlanarFace> Bottom { get; set; }
            public List<PlanarFace> Left { get; set; }
            public List<PlanarFace> Right { get; set; }
            public List<PlanarFace> SideShort { get; set; }
            public List<PlanarFace> SideLong { get; set; }
            public List<PlanarFace> Sides { get; set; }
            public List<PlanarFace> All { get; set; }
            public List<PlanarFace> FrontContour { get; set; }
            public List<PlanarFace> Front { get; set; }
            public List<PlanarFace> Rear { get; set; }
        }
        public class UnboundElemFaces
        {
            public List<Plane> Top { get; set; }
            public List<Plane> Bottom { get; set; }
            public List<Plane> Left { get; set; }
            public List<Plane> Right { get; set; }
            public List<Plane> SideShort { get; set; }
            public List<Plane> SideLong { get; set; }
            public List<Plane> Sides { get; set; }
            public Plane Center { get; set; }
            public List<Plane> All { get; set; }
            public List<Plane> FrontContour { get; set; }
            public List<Plane> Front { get; set; }
            public List<Plane> Rear { get; set; }
        }
        public struct ControlDimensions
        {
            public double L { get; set; }
            public double L1 { get; set; }
            public double L2 { get; set; }
            public double T { get; set; }
            public double H { get; set; }
            public double B { get; set; }
            public double OffsetBot { get; set; }
            public double OffsetTop { get; set; }
            public double OffsetY { get; set; }
            public double OffsetZ { get; set; }
            public double ZBot { get; set; }
            public double ZTop { get; set; }
        }
        public class ControlPoints
        {
            public XYZ CenterStartBottom { get; set; }
            public XYZ CenterMiddleBottom { get; set; }
            public XYZ CenterEndBottom { get; set; }
            public XYZ CenterStartMiddle { get; set; }
            public XYZ CenterMiddleMiddle { get; set; }
            public XYZ CenterEndMiddle { get; set; }
            public XYZ CenterStartTop { get; set; }
            public XYZ CenterMiddleTop { get; set; }
            public XYZ CenterEndTop { get; set; }
        }
        public class ControlLines
        {
            public Curve CenterBot { get; set; }
            public Curve CenterTop { get; set; }
        }
        public class LevelIds
        {
            public ElementId Bot { get; set; }
            public ElementId Top { get; set; }
            public ElementId Base { get; set; }
        }
    }
}
