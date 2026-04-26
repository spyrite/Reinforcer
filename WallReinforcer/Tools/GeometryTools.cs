using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Revit.Filters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class GeometryTools
    {

        #region Points and vectors
        public static XYZ VecABS(this XYZ vec)
        {
            double x = Math.Abs(vec.X);
            double y = Math.Abs(vec.Y);
            double z = Math.Abs(vec.Z);
            return new XYZ(x, y, z);
        }
        public static List<XYZ> GetBBoxVertices(Element elem)
        {
            Document doc = elem.Document;
            BoundingBoxXYZ box = elem.get_BoundingBox(doc.ActiveView);
            XYZ diagonal = box.Max - box.Min;
            double diagonalL = box.Min.DistanceTo(box.Max);
            double L = diagonalL * Math.Cos(diagonal.AngleTo(XYZ.BasisX));
            double B = diagonalL * Math.Cos(diagonal.AngleTo(XYZ.BasisY));
            double H = diagonalL * Math.Cos(diagonal.AngleTo(XYZ.BasisZ));
            List<XYZ> vertices = new List<XYZ>
            {
                box.Min,
                box.Min + XYZ.BasisX*L,
                box.Min + XYZ.BasisY*B,
                box.Min + XYZ.BasisZ*H,
                box.Max,
                box.Max - XYZ.BasisX*L,
                box.Max - XYZ.BasisY*B,
                box.Max - XYZ.BasisZ*H
            };
            return vertices;
        }
        public static XYZ GetElementOrientation(Element elem)
        {
            XYZ dir = new XYZ();
            if (elem is FamilyInstance) dir = (elem as FamilyInstance).GetTransform().BasisY;
            else if (elem is Wall) dir = (elem as Wall).Orientation;
            else if (elem is Floor) dir = Transform.CreateRotation(XYZ.BasisZ, (elem as Floor).SpanDirectionAngle).OfVector(dir);
            return dir;
        }
        public static object GetElementZValue(Element elem)
        {
            Document doc = elem.Document;
            if (elem is Group) elem = (from id in (elem as Group).GetMemberIds()
                                       where !doc.GetElement(id).get_Parameter(BuiltInParameter.PHASE_CREATED).IsReadOnly
                                       select doc.GetElement(id)).ToList().FirstOrDefault();
            else if (elem is AssemblyInstance) elem = (from id in (elem as AssemblyInstance).GetMemberIds()
                                                       where !doc.GetElement(id).get_Parameter(BuiltInParameter.PHASE_CREATED).IsReadOnly
                                                       select doc.GetElement(id)).ToList().FirstOrDefault();

            if (elem is Rebar) elem = doc.GetElement((elem as Rebar).GetHostId());
            else if (elem is RebarContainer) elem = doc.GetElement((elem as RebarContainer).GetHostId());
            else if (elem is AreaReinforcement) elem = doc.GetElement((elem as AreaReinforcement).GetHostId());

            double z;
            if (elem is FamilyInstance)
            {
                if (StructureElementFilters.Columns.PassesFilter(elem))
                {
                    GeometryColumnCache cGeomCache = new GeometryColumnCache(elem as FamilyInstance);
                    z = cGeomCache.Origins.CenterMiddleMiddle.Z;
                }
                else if (StructureElementFilters.Beams.PassesFilter(elem))
                {
                    GeometryBeamCache cBeamCache = new GeometryBeamCache(elem as FamilyInstance);
                    z = cBeamCache.Origins.CenterMiddleBottom.Z;
                }
                else if (VoidFilters.Union.PassesFilter(elem))
                {
                    VoidCache voidCache = new VoidCache(elem as FamilyInstance);
                    if (voidCache.Host is Floor)
                    {
                        GeometrySlabCache slabCache = new GeometrySlabCache(voidCache.Host as Floor);
                        z = slabCache.Dims.ZBot;
                        if (Math.Round(voidCache.Host.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble() * 304.8)
                            == Math.Round(slabCache.Dims.T * 304.8)) z -= 10 / 304.8;
                    }
                    else
                        z = voidCache.Geom.Origins.CenterMiddleMiddle.Z;
                }
                else
                {
                    z = (elem as FamilyInstance).GetTransform().Origin.Z;
                }
            }
            else if (elem is Wall)
            {
                GeometryWallCache wCache = new GeometryWallCache(elem as Wall);
                z = wCache.Origins.CenterMiddleMiddle.Z;
            }
            else if (elem is Floor)
            {
                GeometrySlabCache slabCache = new GeometrySlabCache(elem as Floor);
                z = slabCache.Dims.ZBot;
                if (Math.Round(elem.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble() * 304.8)
                    == Math.Round(slabCache.Dims.T * 304.8)) z -= 10 / 304.8;
            }
            else return null;
            return z;
        }
        public static double GetPointArrayLength(List<XYZ> points)
        {
            List<double> dsts = new List<double>();
            foreach (XYZ point1 in points) { foreach (XYZ point2 in points) { dsts.Add(point1.DistanceTo(point2)); } }
            return dsts.Max();
        }
        public static List<XYZ> GetOrderedVerticesFromLineSet(List<Line> lines)
        {
            if (lines.Count > 1)
            {
                List<XYZ> points = [.. lines.Select(l => l.GetEndPoint(0))];

                foreach (Line line in lines)
                {
                    XYZ p1 = line.GetEndPoint(1);
                    bool next = false;
                    foreach (XYZ point in points)
                        if (point.IsAlmostEqualTo(p1)) { next = true; break; }

                    if (next) continue;
                    else { points.Add(p1); }
                }

                XYZ centerPoint = new(points.Sum(p => p.X) / points.Count,
                                     points.Sum(p => p.Y) / points.Count,
                                     points.Sum(p => p.Z) / points.Count);
                Plane workPlane = Plane.CreateByThreePoints(points[0], points[1], points[2]);
                points = [.. points.OrderBy(p => p.AngleBetweenPointAndCenterPoint(workPlane, centerPoint))];
                return points;
            }
            return null;
        }
        public static XYZ ConvertPointToCurrentDocCoordinateSystem(Document currentDoc, RevitLinkInstance link, XYZ sourcePoint)
        {
#if REVIT2024 || REVIT2025
            XYZ currentDocBasePoint = BasePoint.GetProjectBasePoint(currentDoc).Position;
            XYZ linkDocBasePoint = BasePoint.GetProjectBasePoint(link.GetLinkDocument()).Position;
#else
            XYZ currentDocBasePoint = (new FilteredElementCollector(currentDoc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).FirstElement() as BasePoint).Position;
            XYZ linkDocBasePoint = (new FilteredElementCollector(link.GetLinkDocument()).OfCategory(BuiltInCategory.OST_ProjectBasePoint).FirstElement() as BasePoint).Position;
#endif
            XYZ linkLocationPoint = link.GetTransform().Origin;
            XYZ movement = linkDocBasePoint + linkLocationPoint + currentDocBasePoint;
            XYZ convertedPoint = sourcePoint + movement;
            return convertedPoint;
        }
        public static List<XYZ> GetFilteredPointsInSightOfHostCache(List<XYZ> pointCandidates, GeometryCache geomCache)
        {
            Plane plane = Plane.CreateByOriginAndBasis(geomCache.Origins.CenterMiddleBottom, geomCache.Dirs.X, geomCache.Dirs.Y);
            List<XYZ> filteredPoints = new List<XYZ>();
            foreach (XYZ point in pointCandidates)
            {
                plane.Project(point, out UV uv, out _);
                if (uv.U > -geomCache.Dims.L / 2 & uv.U < geomCache.Dims.L / 2)
                    filteredPoints.Add(point);
            }
            return filteredPoints;
        }
        public static XYZ ProjectPointOntoPlane(Plane plane, XYZ point)
        {
            plane.Project(point, out UV uv, out _);
            XYZ projectPoint = plane.Origin + plane.XVec * uv.U + plane.YVec * uv.V;
            return projectPoint;
        }

        public static bool IsPointNearHostEdge(this XYZ point, GeometryCache hostCache, GeometryCache attachmentCache, double offsetX, double tolerance)
        {
            List<XYZ> checkPoints = [
                point - attachmentCache.Dirs.X*(hostCache.Dims.T/2 - offsetX/304.8 + tolerance/304.8) + hostCache.Dirs.Z*tolerance/304.8,
                point + attachmentCache.Dirs.X*(hostCache.Dims.T/2 - offsetX/304.8 + tolerance/304.8) + hostCache.Dirs.Z*tolerance/304.8,
                ];
            return checkPoints.Select(p => new BoundingBoxContainsPointFilter(p).PassesFilter(attachmentCache.Elem)).Any(c => false);
        }
        #endregion

        #region Faces
        public static List<PlanarFace> GetOutsideFaces(GeometryCache geomCache, ReferenceIntersector intersector)
        {
            List<PlanarFace> outsideFaces = new List<PlanarFace>();
            List<PlanarFace> faces;
            switch (geomCache.GetType().Name)
            {
                case "WallCache":
                    faces = geomCache.Faces.SideLong;
                    break;
                case "ColumnCache":
                    faces = geomCache.Faces.Sides;
                    break;
                default: return outsideFaces;
            }

            List<UV> uvs = new List<UV>
            {
                new UV (10/304.8, 10/304.8),
                new UV (geomCache.Lines.CenterBot.Length/2, 10/304.8),
                new UV (geomCache.Lines.CenterBot.Length - 10/304.8, 10/304.8),
            };

            foreach (PlanarFace face in faces)
            {
                XYZ normal = face.FaceNormal.Normalize();
                foreach (UV uv in uvs)
                {
                    XYZ origin = face.Evaluate(uv);
                    ReferenceWithContext targets = intersector.FindNearest(origin, normal);
                    if (targets == null)
                    {
                        outsideFaces.Add(face);
                        continue;
                    }
                }
            }
            return outsideFaces;
        }
        #endregion

        #region Angles
        private static double AngleBetweenPointAndCenterPoint(this XYZ point, Plane workPlane, XYZ centerPoint) => workPlane.XVec.AngleOnPlaneTo(point - centerPoint, workPlane.Normal);
        #endregion

        public static List<double> SummaryXYAngleDataOld(double alphaX, double alphaY)
        {
            double modX = Math.Round(Math.Round(alphaX, 5) % Math.Round(Math.PI, 5), 1);
            double modY = Math.Round(Math.Round(alphaY, 5) % Math.Round(Math.PI, 5), 1);

            if (modX == 0 & modY == 0) return new List<double> { 0, 0 };
            else if (modX == 0) return new List<double> { alphaY, Math.PI / 2 };
            else if (modY == 0) return new List<double> { alphaX, 0 };
            else
            {
                double b = Math.Sqrt(2 + Math.Pow(Math.Tan(alphaX) - Math.Tan(alphaY), 2));
                double halfP = (1 / Math.Cos(alphaX) + 1 / Math.Cos(alphaY) + b) / 2;
                double h = 2 * Math.Sqrt(halfP * (halfP - 1 / Math.Cos(alphaX)) * (halfP - 1 / Math.Cos(alphaY)) * (halfP - b)) / b;
                double bx = Math.Sqrt(Math.Pow(1 / Math.Cos(alphaX), 2) - Math.Pow(h, 2));
                double by = Math.Sqrt(Math.Pow(1 / Math.Cos(alphaY), 2) - Math.Pow(h, 2));
                double hsum = by * (Math.Tan(alphaX) - Math.Tan(alphaY)) / b + Math.Tan(alphaY);
                double alphaSum = Math.Asin(hsum / h);

                double m = h * Math.Cos(alphaSum);
                double mx = Math.Sqrt(Math.Pow(bx, 2) - Math.Pow(Math.Tan(alphaX) - hsum, 2));
                double betta = Math.Asin(mx * Math.Sin(Math.PI / 4) / m);
                return new List<double> { alphaSum, betta };
            }
        }

        public static List<double> SummaryXYAngleData(double alphaX, double alphaY)
        {
            double betta = Math.Atan2(alphaY, alphaX);
            double alphaSum = alphaX / Math.Cos(betta);
            return new List<double> { alphaSum, betta };
        }

        public class Translation
        {
            private static Element sourceElement;
            private static Element targetElement;
            private readonly XYZ sourcePoint;
            private readonly XYZ targetPoint;
            public RotationData RD;

            public Translation(Element sourceElem, Element targetElem)
            {
                sourceElement = sourceElem;
                targetElement = targetElem;

                Solid sourceSolid = SolidTools.GetSolid(sourceElem, false);
                sourcePoint = sourceSolid.ComputeCentroid();
                Solid targetSolid = SolidTools.GetSolid(targetElem, false);
                targetPoint = targetSolid.ComputeCentroid();

                GetRotationData();
            }

            public Translation(FamilyInstance sourceInst, Solid sourceSolid)
            {
                Solid targetSolid = SolidTools.GetSolid(sourceInst, false);
                GeometryCache targetGeomCache = new GeometryFamilyInstanceCache(sourceInst);
                sourcePoint = sourceSolid.ComputeCentroid();
                targetPoint = targetSolid.ComputeCentroid();
                RD = new RotationData
                {
                    Angle1 = XYZ.BasisX.AngleOnPlaneTo(targetGeomCache.Dirs.X, XYZ.BasisZ),
                    Angle2 = XYZ.BasisY.AngleOnPlaneTo(targetGeomCache.Dirs.Y, XYZ.BasisZ),
                    Angle3 = 0,
                    Axis1 = Line.CreateBound(targetPoint, targetPoint + XYZ.BasisZ),
                    Axis2 = Line.CreateBound(targetPoint, targetPoint + XYZ.BasisZ),
                    Axis3 = Line.CreateBound(targetPoint, targetPoint + XYZ.BasisX),
                    Dir1 = XYZ.BasisZ,
                    Dir2 = XYZ.BasisZ,
                    Dir3 = XYZ.BasisX,
                };
            }

            public Translation(Solid sourceSolid, Solid targetSolid)
            {
                sourcePoint = sourceSolid.ComputeCentroid();
                targetPoint = targetSolid.ComputeCentroid();
            }

            public Transform AsTransform() { return Transform.CreateTranslation(targetPoint - sourcePoint); }
            public List<Transform> AsRotationTransforms()
            {
                List<Transform> transforms = new List<Transform>()
                {
                    Transform.CreateRotationAtPoint(RD.Dir1, RD.Angle1, targetPoint),
                    Transform.CreateRotationAtPoint(RD.Dir2, RD.Angle2, targetPoint),
                    Transform.CreateRotationAtPoint(RD.Dir3, RD.Angle3, targetPoint)
                };
                return transforms;
            }
            public XYZ AsVec() { return targetPoint - sourcePoint; }

            private void GetRotationData()
            {
                RD = new RotationData
                {
                    Angle1 = 0,
                    Angle2 = 0,
                    Angle3 = 0,
                    Axis1 = Line.CreateBound(targetPoint, targetPoint + XYZ.BasisZ),
                    Axis2 = Line.CreateBound(targetPoint, targetPoint + XYZ.BasisX),
                    Axis3 = Line.CreateBound(targetPoint, targetPoint + XYZ.BasisY),
                    Dir1 = XYZ.BasisZ,
                    Dir2 = XYZ.BasisX,
                    Dir3 = XYZ.BasisY,
                };

                GeometryCache sourceGeomCache = null;
                if (sourceElement is FamilyInstance) sourceGeomCache = new GeometryFamilyInstanceCache(sourceElement as FamilyInstance);
                else if (sourceElement is Wall) sourceGeomCache = new GeometryWallCache(sourceElement as Wall);
                else if (sourceElement is Floor) sourceGeomCache = new GeometrySlabCache(sourceElement as Floor);

                GeometryCache targetGeomCache = null;
                if (targetElement is FamilyInstance) targetGeomCache = new GeometryFamilyInstanceCache(targetElement as FamilyInstance);
                else if (targetElement is Wall) targetGeomCache = new GeometryWallCache(targetElement as Wall);
                else if (targetElement is Floor) targetGeomCache = new GeometrySlabCache(targetElement as Floor);

                if (sourceGeomCache != null && targetGeomCache != null)
                {
                    RD.Angle1 = sourceGeomCache.Dirs.X.AngleOnPlaneTo(targetGeomCache.Dirs.X, XYZ.BasisZ);
                    RD.Angle2 = CutPeriodsFromAngle(sourceGeomCache.Dirs.Z.AngleOnPlaneTo(targetGeomCache.Dirs.Z, XYZ.BasisX));
                    RD.Angle3 = CutPeriodsFromAngle(sourceGeomCache.Dirs.Z.AngleOnPlaneTo(targetGeomCache.Dirs.Z, -XYZ.BasisY));
                }
            }

            public struct RotationData
            {
                public double Angle1 { get; set; }
                public double Angle2 { get; set; }
                public double Angle3 { get; set; }
                public Line Axis1 { get; set; }
                public Line Axis2 { get; set; }
                public Line Axis3 { get; set; }
                public XYZ Dir1 { get; set; }
                public XYZ Dir2 { get; set; }
                public XYZ Dir3 { get; set; }
            }

            private double CutPeriodsFromAngle(double sourceAngle)
            {
                double newAngle;
                if (sourceAngle >= 0 & sourceAngle <= Math.PI / 2) newAngle = sourceAngle;
                else if (sourceAngle > Math.PI / 2 & sourceAngle < Math.PI) newAngle = -(Math.PI - sourceAngle);
                else if (Math.Round(sourceAngle, 5) == Math.Round(Math.PI, 5)) newAngle = 0;
                else if (sourceAngle > Math.PI & sourceAngle <= 3 * Math.PI / 2) newAngle = sourceAngle - Math.PI;
                else if (sourceAngle > 3 * Math.PI / 2 & sourceAngle <= 2 * Math.PI) newAngle = -(2 * Math.PI - sourceAngle);
                else newAngle = sourceAngle;
                return newAngle;
            }

        }
    }
}
