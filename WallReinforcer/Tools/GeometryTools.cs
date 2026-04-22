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
        //Поля 
        private static XYZ centerPoint;
        private static Plane workPlane;

        #region Solids
        public static Solid CreateSolidFromBBox(BoundingBoxXYZ box, XYZ origin)
        {
            XYZ minPoint = box.Transform.OfPoint(box.Min);
            XYZ maxPoint = box.Transform.OfPoint(box.Max);
            XYZ diagonal = maxPoint - minPoint;
            double diagonalL = minPoint.DistanceTo(maxPoint);
            Transform transform = box.Transform;

            List<XYZ> dirs = new List<XYZ> { transform.BasisX, transform.BasisY, transform.BasisZ };
            var dims = new List<double>();
            for (int i = 0; i < dirs.Count; i++)
            {
                dims.Add(diagonalL * Math.Cos(diagonal.AngleTo(dirs[i])));
            }
            XYZ p1 = origin + dirs[0] * dims[0];
            XYZ p2 = p1 + dirs[1] * dims[1];
            XYZ p3 = p2 - dirs[0] * dims[0];
            List<XYZ> points = new List<XYZ> { origin, p1, p2, p3 };

            List<Curve> lines = new List<Curve>
            {
                Line.CreateBound(points[3], points[0]),
                Line.CreateBound(points[0], points[1]),
                Line.CreateBound(points[1], points[2]),
                Line.CreateBound(points[2], points[3])
            };

            List<CurveLoop> cls = new List<CurveLoop> { CurveLoop.Create(lines) };
            Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, dirs[2], dims[2]);
            return solid;
        }
        public static Solid GetSolid(Element elem, bool cloneSolid)
        {
            Solid solid = null;
            Options opt = new Options();
            if (!cloneSolid) opt.ComputeReferences = true;
            List<GeometryObject> geometry = elem.get_Geometry(opt).ToList();

            if (elem is FamilyInstance)
            {
                FamilyInstance fi = elem as FamilyInstance;
                List<GeometryObject> geomInst = (geometry.Find(geom => geom is GeometryInstance) as GeometryInstance).GetInstanceGeometry().ToList();

                if (fi.Symbol.Family.IsInPlace) solid = geomInst.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid;
                else if (fi.HasModifiedGeometry()) solid = geometry.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid;
                else
                {
                    try
                    {
                        GetModifiedGeometry(elem.Document, elem.Id);
                        elem.Document.Regenerate();
                        geometry = elem.get_Geometry(opt).ToList();
                        solid = geometry.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid;
                    }
                    catch { solid = geomInst.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid; }
                }
            }

            if (solid == null) solid = geometry.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid;
            if (cloneSolid && solid != null) solid = SolidUtils.Clone(solid);

            return solid;
        }
        public static Solid GetSolidInTargetDoc(Element elem, Document targetDoc)
        {
            Solid solid = null;
            Options opt = new Options();
            List<GeometryObject> geometry = elem.get_Geometry(opt).ToList();

            if (elem is FamilyInstance)
            {
                FamilyInstance fi = elem as FamilyInstance;
                List<GeometryObject> geomInst = (geometry.Find(geom => geom is GeometryInstance) as GeometryInstance).GetInstanceGeometry().ToList();

                if (fi.Symbol.Family.IsInPlace) solid = geomInst.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid;
                else if (!fi.HasModifiedGeometry())
                {
                    try { GetModifiedGeometry(elem.Document, elem.Id); }
                    catch { solid = geomInst.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid; }
                }
            }

            if (solid == null) solid = geometry.Find(geom => geom is Solid && (geom as Solid).Volume > 0) as Solid;

            XYZ sourceBasePoint = (new FilteredElementCollector(elem.Document).OfCategory(BuiltInCategory.OST_ProjectBasePoint).FirstElement() as BasePoint).Position;
            XYZ targetBasePoint = (new FilteredElementCollector(targetDoc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).FirstElement() as BasePoint).Position;
            Transform transform = Transform.CreateTranslation(targetBasePoint - sourceBasePoint);
            solid = SolidUtils.CreateTransformed(solid, transform);

            return solid;
        }
        public static Solid GetClonedSolidForComplicateFI(FamilyInstance fi)
        {
            Document doc = fi.Document;
            Solid solid = null;
            Options opt = new Options();
            foreach (ElementId subId in fi.GetSubComponentIds())
            {
                FamilyInstance subFI = doc.GetElement(subId) as FamilyInstance;
                List<GeometryObject> geometry = subFI.get_Geometry(opt).ToList();
                List<GeometryObject> geomInst = (geometry.Find(geom => geom is GeometryInstance) as GeometryInstance).GetInstanceGeometry().ToList();

                List<Solid> subSolids = geomInst.FindAll(geom => geom is Solid && (geom as Solid).Volume > 0).Cast<Solid>().ToList();
                if (solid == null) solid = SolidUtils.Clone(subSolids.First());
                foreach (Solid subSolid in subSolids)
                    BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, subSolid, BooleanOperationsType.Union);
            }
            return solid;
        }
        public static double GetSolidHeight(Solid solid)
        {
            List<double> zs = new List<double>();
            foreach (PlanarFace face in solid.Faces) zs.Add(face.Origin.Z);
            double H = zs.Max() - zs.Min();
            return H;
        }
        public static Solid GetScaledSolid(Element elem, double scale)
        {
            Solid sourceSolid = GetSolid(elem, false);
            double H = GetSolidHeight(sourceSolid);
            Solid scaledSolid = null;
            List<PlanarFace> faces = new List<PlanarFace>();
            foreach (PlanarFace face in sourceSolid.Faces)
            {
                if (face.FaceNormal.Normalize().IsAlmostEqualTo(-XYZ.BasisZ))
                {
                    List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
                    List<CurveLoop> newCLs = (from cl in cls
                                              select CurveLoop.CreateViaOffset(cl, scale / 304.8, -XYZ.BasisZ)).ToList();
                    Solid newSolid = GeometryCreationUtilities.CreateExtrusionGeometry(newCLs, XYZ.BasisZ, H);
                    if (scaledSolid == null)
                    {
                        scaledSolid = newSolid;
                        continue;
                    }
                    BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(scaledSolid, newSolid, BooleanOperationsType.Union);
                }
            }
            return scaledSolid;
        }
        public static Solid GetExtendedSolidFromFace(PlanarFace face, double dstX, double dstY, double extrudeDst, bool inversed)
        {
            XYZ xDir = face.XVector;
            XYZ yDir = face.YVector;

            List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
            List<CurveLoop> newCLs = new List<CurveLoop>();
            foreach (CurveLoop cl in cls)
            {
                XYZ clNormal = cl.GetPlane().Normal;
                List<double> offsetDsts = new List<double>();
                foreach (Curve curve in cl)
                {
                    if (curve is Line)
                    {
                        Line line = curve as Line;
                        XYZ lineYDir = line.Direction.CrossProduct(clNormal);
                        if (lineYDir.IsAlmostEqualTo(xDir) || lineYDir.IsAlmostEqualTo(-xDir)) offsetDsts.Add(dstX);
                        else if (lineYDir.IsAlmostEqualTo(yDir) || lineYDir.IsAlmostEqualTo(-yDir)) offsetDsts.Add(dstY);
                        else offsetDsts.Add(0);
                    }
                    else offsetDsts.Add(0);
                }
                CurveLoop newCL = CurveLoop.CreateViaOffset(cl, offsetDsts, clNormal);
                newCLs.Add(newCL);
            }
            XYZ extrudeDir = face.FaceNormal;
            if (inversed) extrudeDir = -face.FaceNormal;
            try { Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(newCLs, extrudeDir, extrudeDst); return solid; }
            catch { return null; }
        }
        public static void GetModifiedGeometry(Document doc, ElementId id)
        {
            Element elem = doc.GetElement(id);
            ElementId tempId = null;

            if (elem is FamilyInstance && !(elem as FamilyInstance).Symbol.Family.IsInPlace && !(elem as FamilyInstance).HasModifiedGeometry())
            {
                if (StructureElementFilters.Columns.PassesFilter(elem) || StructureElementFilters.Beams.PassesFilter(elem))
                {
                    GeometryFamilyInstanceCache geomFamInstCache = new GeometryFamilyInstanceCache(elem as FamilyInstance);
                    using (SubTransaction subTx = new SubTransaction(doc))
                    {
                        List<XYZ> points =
                            [
                                geomFamInstCache.Origins.CenterMiddleMiddle - geomFamInstCache.Dirs.X/2*10/304.8 - geomFamInstCache.Dirs.Z/2*10/304.8,
                                geomFamInstCache.Origins.CenterMiddleMiddle - geomFamInstCache.Dirs.X/2*10/304.8 + geomFamInstCache.Dirs.Z/2*10/304.8,
                                geomFamInstCache.Origins.CenterMiddleMiddle + geomFamInstCache.Dirs.X/2*10/304.8 + geomFamInstCache.Dirs.Z/2*10/304.8,
                                geomFamInstCache.Origins.CenterMiddleMiddle + geomFamInstCache.Dirs.X/2*10/304.8 - geomFamInstCache.Dirs.Z/2*10/304.8,
                            ];
                        List<Line> lines =
                            [
                                Line.CreateBound(points[0], points[1]),
                                Line.CreateBound(points[1], points[2]),
                                Line.CreateBound(points[2], points[3]),
                                Line.CreateBound(points[3], points[0])
                            ];
                        CurveArray curveArray = new CurveArray();
                        foreach (Line line in lines) curveArray.Append(line);

                        try
                        {
                            subTx.Start();
                            Opening tempOpening = doc.Create.NewOpening(elem as FamilyInstance, curveArray, Autodesk.Revit.Creation.eRefFace.CenterY);
                            tempId = tempOpening.Id;
                            subTx.Commit();
                        }
                        catch { subTx.RollBack(); }
                        ;

                        if (tempId != null) { subTx.Start(); doc.Delete(tempId); subTx.Commit(); }
                    }
                }

                else
                {
                    FamilySymbol tempSymbol = (from sym in new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
                                               where (sym as FamilySymbol).FamilyName == "Семейство1"
                                               select sym as FamilySymbol).FirstOrDefault();

                    if (tempSymbol is not null)
                    {
                        if (!tempSymbol.IsActive) tempSymbol.Activate();
                        List<XYZ> insertPoints = GetBBoxVertices(elem);
                        using (SubTransaction subTx = new SubTransaction(doc))
                        {
                            foreach (XYZ point in insertPoints)
                            {
                                subTx.Start();
                                Element tempElem = doc.Create.NewFamilyInstance(point, tempSymbol, StructuralType.NonStructural);
                                try
                                {
                                    InstanceVoidCutUtils.AddInstanceVoidCut(doc, elem, tempElem);
                                    tempId = tempElem.Id;
                                    subTx.Commit();
                                    break;
                                }
                                catch { subTx.RollBack(); continue; }
                                ;
                            }
                            if (tempId != null) { subTx.Start(); doc.Delete(tempId); subTx.Commit(); }
                        }
                    }
                }
            }
        }
        #endregion

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
                List<XYZ> points = (from line in lines
                                    select line.GetEndPoint(0)).ToList();
                foreach (Line line in lines)
                {
                    XYZ p1 = line.GetEndPoint(1);
                    bool next = false;
                    foreach (XYZ point in points)
                        if (point.IsAlmostEqualTo(p1)) { next = true; break; }

                    if (next) continue;
                    else { points.Add(p1); }
                }

                centerPoint = new XYZ(points.Sum(p => p.X) / points.Count,
                                     points.Sum(p => p.Y) / points.Count,
                                     points.Sum(p => p.Z) / points.Count);
                workPlane = Plane.CreateByThreePoints(points[0], points[1], points[2]);
                points = points.OrderBy(AngleBetweenPointAndCenterPoint).ToList();
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
        private static double AngleBetweenPointAndCenterPoint(XYZ point) { return workPlane.XVec.AngleOnPlaneTo(point - centerPoint, workPlane.Normal); }
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

                Solid sourceSolid = GetSolid(sourceElem, false);
                sourcePoint = sourceSolid.ComputeCentroid();
                Solid targetSolid = GetSolid(targetElem, false);
                targetPoint = targetSolid.ComputeCentroid();

                GetRotationData();
            }

            public Translation(FamilyInstance sourceInst, Solid sourceSolid)
            {
                Solid targetSolid = GetSolid(sourceInst, false);
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
