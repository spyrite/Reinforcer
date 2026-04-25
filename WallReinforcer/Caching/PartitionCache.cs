using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;

namespace RevitOSA.WallReinforcer.Caching
{
    public class PartitionCache : RebarHostCache
    {
        public PartitionCache(Element elem) : base(elem)
        {
            Partition = elem as Wall;
            Geom = new GeometryWallCache(Partition);
            Attachments = null;
            AttachedConcretes = null;

#if REVIT2024 || REVIT2025
            Voids = (from id in (elem as Wall).FindInserts(false, false, false, false)
                     where _doc.GetElement(id).Category.BuiltInCategory == BuiltInCategory.OST_Windows || _doc.GetElement(id).Category.BuiltInCategory == BuiltInCategory.OST_Doors
                     select _doc.GetElement(id)).ToList();
#else
            Voids = (from id in (elem as Wall).FindInserts(false, false, false, false)
                     where (BuiltInCategory)doc.GetElement(id).Category.Id.IntegerValue == BuiltInCategory.OST_Windows ||
                     (BuiltInCategory)doc.GetElement(id).Category.Id.IntegerValue == BuiltInCategory.OST_Doors
                     select doc.GetElement(id)).ToList();
#endif
        }

        public Wall Partition { get; set; }
        public List<Attachment> Attachments { get; set; }
        public List<Element> AttachedConcretes { get; set; }
        public List<Element> Voids { get; set; }

        public void GetAttachedConcreteIds()
        {
            if (Geom.Solid != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    double h = Geom.Dims.H / 2 * (i + 1);
                    List<XYZ> catchPoints = new List<XYZ>()
                    {
                        Geom.Origins.CenterStartBottom - Geom.Dirs.X*ReinfSettings.Default.ci_ConcreteCatchAccuracy/304.8 + Geom.Dirs.Z*h,
                        Geom.Origins.CenterStartBottom + Geom.Dirs.X*ReinfSettings.Default.ci_ConcreteCatchAccuracy/304.8 - Geom.Dirs.Y*(Geom.Dims.T/2+ReinfSettings.Default.ci_ConcreteCatchAccuracy/304.8) + Geom.Dirs.Z*h,
                        Geom.Origins.CenterStartBottom + Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Y*(Geom.Dims.T/2+ReinfSettings.Default.ci_ConcreteCatchAccuracy/304.8) + Geom.Dirs.Z*h,
                        Geom.Origins.CenterEndBottom + Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z*h,
                        Geom.Origins.CenterStartBottom - Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 - Geom.Dirs.Y*(Geom.Dims.T/2+ReinfSettings.Default.ci_ConcreteCatchAccuracy/304.8) + Geom.Dirs.Z*h,
                        Geom.Origins.CenterStartBottom - Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Y*(Geom.Dims.T/2+ReinfSettings.Default.ci_ConcreteCatchAccuracy/304.8) + Geom.Dirs.Z*h,
                    };
                    List<ElementFilter> filters = (from point in catchPoints
                                                   select new BoundingBoxContainsPointFilter(point) as ElementFilter).ToList();
                    filters.Add(new ElementIntersectsSolidFilter(Geom.Solid));
                    ElementFilter filter = new LogicalOrFilter(filters);
                    AttachedConcretes = new ExtractingTools.ConcreteExtractor(_doc, filter).ToElements().ToList();
                }
            }
        }
        public void GetAttachmentsToConcretes()
        {
            GetAttachedConcreteIds();
            if (AttachedConcretes != null && AttachedConcretes.Count > 0)
            {
                Attachments = new List<Attachment>();
                for (int i = 0; i < 2; i++)
                {
                    double h = Geom.Dims.H / 2 * (i + 1);
                    List<XYZ> pointsAtStart = new List<XYZ>
                    {
                        Geom.Origins.CenterStartBottom - Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h,
                        Geom.Origins.CenterStartBottom + Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h,
                        Geom.Origins.CenterStartBottom - Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h - Geom.Dirs.Y * (Geom.Dims.T / 2 + ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8),
                        Geom.Origins.CenterStartBottom + Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h + Geom.Dirs.Y * (Geom.Dims.T / 2 + ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8)
                    };
                    List<XYZ> pointsAtEnd = new List<XYZ>
                    {
                        Geom.Origins.CenterEndBottom + Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h,
                        Geom.Origins.CenterEndBottom - Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h,
                        Geom.Origins.CenterEndBottom - Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h - Geom.Dirs.Y * (Geom.Dims.T / 2 + ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8),
                        Geom.Origins.CenterEndBottom + Geom.Dirs.X * ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8 + Geom.Dirs.Z * h + Geom.Dirs.Y * (Geom.Dims.T / 2 + ReinfSettings.Default.ci_ConcreteCatchAccuracy / 304.8)
                    };
                    List<Line> cutlines = new List<Line>
                    {
                        Line.CreateBound(pointsAtStart[0], pointsAtEnd[0]),
                        Line.CreateBound(pointsAtStart[2], pointsAtStart[1]),
                        Line.CreateBound(pointsAtStart[3], pointsAtStart[1]),
                        Line.CreateBound(pointsAtEnd[2], pointsAtEnd[1]),
                        Line.CreateBound(pointsAtEnd[3], pointsAtEnd[1])
                    };
                    foreach (Element concrete in AttachedConcretes)
                    {
                        Solid solid = GeometryTools.GetSolid(concrete, false);
                        if (solid != null)
                        {
                            foreach (Line cutline in cutlines)
                            {
                                List<Curve> spotlines = solid.IntersectWithCurve(cutline, null).ToList();
                                if (spotlines != null && spotlines.Count > 0)
                                {
                                    foreach (Line spotline in spotlines)
                                    {
                                        XYZ dir = spotline.Direction.Normalize();
                                        List<PlanarFace> faces = new List<PlanarFace>();
                                        foreach (PlanarFace face in solid.Faces)
                                            if (face.FaceNormal.Normalize().IsAlmostEqualTo(dir) || face.FaceNormal.Normalize().IsAlmostEqualTo(-dir))
                                                faces.Add(face);
                                        for (int j = 0; j < 2; j++)
                                        {
                                            XYZ point = spotline.GetEndPoint(j);
                                            foreach (PlanarFace face in faces)
                                            {
                                                IntersectionResult proj = face.Project(point);
                                                if (proj != null && Math.Round(proj.Distance * 304.8) == 0)
                                                {
                                                    Attachment att = new Attachment
                                                    {
                                                        Origin = new XYZ(point.X, point.Y, Geom.Dims.ZBot),
                                                        Face = face,
                                                        Concrete = concrete
                                                    };
                                                    Attachments.Add(att);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public List<XYZ> GetCIOrigins(PlanarFace face, XYZ startPoint)
        {
            List<XYZ> origins = new List<XYZ>();
            if (Geom.Solid != null)
            {
                double startZ = (Math.Round(Geom.Dims.ZBot * 304.8) + ReinfSettings.Default.ci_BottomAlign) / 304.8;
                double endZ = Geom.Dims.ZTop;
                for (double z = startZ; z <= endZ; z += ReinfSettings.Default.ci_Step)
                {
                    XYZ p0 = new XYZ(startPoint.X, startPoint.Y, z);
                    XYZ p1 = p0 + face.FaceNormal * ReinfSettings.Default.ci_SmallWallAccuracy / 304.8;
                    Line cutline = Line.CreateBound(p0, p1);
                    List<Curve> spotlines = Geom.Solid.IntersectWithCurve(cutline, null).ToList();
                    if (spotlines != null && spotlines.Count > 0)
                    {
                        Line spotline = spotlines.First() as Line;
                        if (Math.Round(spotline.Length * 304.8) >= ReinfSettings.Default.ci_SmallWallAccuracy) origins.Add(p0);
                    }
                }
            }
            return origins;
        }
    }
}
