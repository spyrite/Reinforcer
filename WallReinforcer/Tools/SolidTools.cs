using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Revit.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class SolidTools
    {
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

        /// <summary>
        /// Создает твердое тело путем смещения контуров грани на заданные расстояния по локальным осям и последующей экструзии.
        /// Смещение применяется дифференцированно: для ребер, параллельных локальной оси X грани, используется dstX, 
        /// для ребер, параллельных локальной оси Y — dstY.
        /// </summary>
        /// <param name="face">Исходная плоская грань (PlanarFace), на основе которой строится тело.</param>
        /// <param name="offsetX">Величина смещения для ребер, ориентированных вдоль локальной оси X грани (face.XVector).</param>
        /// <param name="offsetY">Величина смещения для ребер, ориентированных вдоль локальной оси Y грани (face.YVector).</param>
        /// <param name="extrudeDst">Расстояние экструзии полученного замкнутого контура.</param>
        /// <param name="inversed">Если true, направление экструзии будет противоположно нормали грани (-FaceNormal). Если false — по нормали (FaceNormal).</param>
        /// <returns>Новое твердое тело (Solid) или null, если создание геометрии завершилось ошибкой.</returns>
        public static Solid GetExtendedSolidFromFace(this PlanarFace face, double offsetX, double offsetY, double extrudeDst, bool inversed)
        {
            XYZ xDir = face.XVector;
            XYZ yDir = face.YVector;

            List<CurveLoop> cls = [.. face.GetEdgesAsCurveLoops()];
            List<CurveLoop> newCLs = [];
            foreach (CurveLoop cl in cls)
            {
                XYZ clNormal = cl.GetPlane().Normal;
                List<double> offsetDsts = [];
                foreach (Curve curve in cl)
                {
                    if (curve is Line line)
                    {
                        XYZ lineYDir = line.Direction.CrossProduct(clNormal);
                        if (lineYDir.IsAlmostEqualTo(xDir) || lineYDir.IsAlmostEqualTo(-xDir)) offsetDsts.Add(offsetX);
                        else if (lineYDir.IsAlmostEqualTo(yDir) || lineYDir.IsAlmostEqualTo(-yDir)) offsetDsts.Add(offsetY);
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
                        List<XYZ> insertPoints = GeometryTools.GetBBoxVertices(elem);
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

        #region Методы расширения
        public static double GetSolidHeight(this Solid solid)
        {
            List<double> zs = new List<double>();
            foreach (PlanarFace face in solid.Faces) zs.Add(face.Origin.Z);
            double H = zs.Max() - zs.Min();
            return H;
        }

        public static void CutExtendSolidWithBoundOpenings(this Solid solid, List<VoidCache> openings)
        {
            double offset = 10.0 / 304.8;
            List<Solid> solidsToExtend = [];
            List<Solid> solidsToCut = [];

            foreach (VoidCache opening in openings)
            {
                if (opening.Geom.Solid == null) opening.Geom.GetSolidData();
                Solid openingSolid = SolidUtils.Clone(opening.Geom.Solid);
                Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(solid, openingSolid, BooleanOperationsType.Intersect);

                if (intersection == null || intersection.Volume <= 0) continue;

                List<PlanarFace> attFaces = opening.Geom.GetAttachedFaces(solid);
                if (attFaces == null || attFaces.Count == 0) continue;

                foreach (PlanarFace attFace in attFaces)
                {
                    XYZ normal = attFace.FaceNormal.Normalize();

                    PlanarFace solidFace = solid.Faces.Cast<PlanarFace>()
                        .FirstOrDefault(f => f.FaceNormal.Normalize().IsAlmostEqualTo(normal));

                    if (solidFace == null) continue;

                    UV uv1 = new(offset, offset);
                    UV uv2 = new(offset, opening.Geom.Dims.B / 2.0);
                    UV uv3 = new(offset, opening.Geom.Dims.B - offset);

                    List<XYZ> points = [
                            attFace.Evaluate(uv1),
                            attFace.Evaluate(uv2),
                            attFace.Evaluate(uv3)];

                    var validPointData = points.Select(p => new { Point = p, Result = solidFace.Project(p) })
                                               .FirstOrDefault(x => x.Result != null && x.Result.Distance > offset);

                    if (validPointData == null) continue;

                    double dist = Math.Abs(validPointData.Result.Distance);
                    List<CurveLoop> attLoops = [.. attFace.GetEdgesAsCurveLoops()];

                    if (normal.IsAlmostEqualTo(-XYZ.BasisZ))
                    {
                        List<CurveLoop> solidLoops = [.. solidFace.GetEdgesAsCurveLoops()];
                        Solid temp1 = GeometryCreationUtilities.CreateExtrusionGeometry(attLoops, -normal, dist);
                        Solid temp2 = GeometryCreationUtilities.CreateExtrusionGeometry(solidLoops, normal, dist);
                        Solid addit = BooleanOperationsUtils.ExecuteBooleanOperation(temp1, temp2, BooleanOperationsType.Intersect);

                        if (addit != null)
                        {
                            solidsToExtend.Add(addit);
                        }
                    }
                    else
                    {
                        Solid addit = GeometryCreationUtilities.CreateExtrusionGeometry(attLoops, normal, dist);
                        if (addit != null)
                        {
                            BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(openingSolid, addit, BooleanOperationsType.Union);
                        }
                    }

                    break;
                }

                solidsToCut.Add(openingSolid);
            }

            solidsToExtend.ForEach(s => BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, s, BooleanOperationsType.Union));
            solidsToCut.ForEach(s => BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, s, BooleanOperationsType.Difference));
        }

        public static void CutExtendSolidWithUpperOpenings(this Solid solid, List<VoidCache> openings, double hostZBot, double topOv, double cutDepth)
        {
            double offset = 10.0 / 304.8;
            List<PlanarFace> solidFaces = solid.Faces.Cast<PlanarFace>()
                .Where(f => f.FaceNormal.Normalize().IsAlmostEqualTo(XYZ.BasisZ))
                .ToList();

            if (!solidFaces.Any()) return;

            foreach (VoidCache opening in openings)
            {
                double heightDiffMm = Math.Round((opening.Geom.Dims.ZBot - hostZBot) * 304.8);
                double thresholdMm = Math.Round((topOv * 2.3 + 100.0 / 304.8) * 304.8) * 2;

                if (heightDiffMm >= thresholdMm)
                    continue;

                PlanarFace openFace = opening.Geom.Solid.Faces.Cast<PlanarFace>()
                    .FirstOrDefault(f => f.FaceNormal.Normalize().IsAlmostEqualTo(-XYZ.BasisZ));

                if (openFace == null)
                    continue;

                UV uv1 = new(offset, offset);
                UV uv2 = new(offset, opening.Geom.Dims.B / 2.0);
                UV uv3 = new(offset, opening.Geom.Dims.B - offset);

                List<XYZ> points = [
                    openFace.Evaluate(uv1),
                    openFace.Evaluate(uv2),
                    openFace.Evaluate(uv3)
                ];

                PlanarFace targetSolidFace = null;
                IntersectionResult project = null;

                bool found = false;
                foreach (PlanarFace sFace in solidFaces)
                {
                    foreach (XYZ point in points)
                    {
                        IntersectionResult proj = sFace.Project(point);
                        if (proj != null)
                        {
                            targetSolidFace = sFace;
                            project = proj;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (!found || targetSolidFace == null || project == null) continue;

                if (Math.Round(opening.Geom.Dims.ZBot * 304.8) > Math.Round(targetSolidFace.Origin.Z * 304.8))
                {
                    List<CurveLoop> openLoops = [.. openFace.GetEdgesAsCurveLoops()];
                    List<CurveLoop> solidLoops = [.. targetSolidFace.GetEdgesAsCurveLoops()];

                    Solid temp1 = GeometryCreationUtilities.CreateExtrusionGeometry(openLoops, -XYZ.BasisZ, project.Distance);
                    Solid temp2 = GeometryCreationUtilities.CreateExtrusionGeometry(solidLoops, XYZ.BasisZ, project.Distance);

                    Solid extendSolid = BooleanOperationsUtils.ExecuteBooleanOperation(temp1, temp2, BooleanOperationsType.Intersect);

                    if (extendSolid != null)
                        BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, extendSolid, BooleanOperationsType.Union);
                }
                else BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, opening.Geom.Solid, BooleanOperationsType.Difference);

                List<CurveLoop> cutLoops = [.. openFace.GetEdgesAsCurveLoops()];
                Solid cutSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cutLoops, -XYZ.BasisZ, cutDepth);

                if (cutSolid != null) BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, cutSolid, BooleanOperationsType.Difference);
            }
        }

        public static void ExtendSolidWithMiddleOpenBotFace(this Solid solid, List<VoidCache> openings, double hostZBot, double botOv)
        {
            List<PlanarFace> solidFaces = solid.Faces.Cast<PlanarFace>()
                .Where(f => f.FaceNormal.Normalize().IsAlmostEqualTo(XYZ.BasisZ))
                .ToList();

            if (!solidFaces.Any())
                return;

            foreach (VoidCache opening in openings)
            {
                double dz1 = opening.Geom.Dims.ZBot - hostZBot;
                double dz2 = Math.Ceiling((botOv * 1.3 * 304.8) / 10.0) * 10.0 / 304.8 - dz1;

                if (dz2 <= 0 || opening.Geom.GetAttachedFaces(solid)?.Any() == true) continue;

                PlanarFace openFace = opening.Geom.Solid.Faces.Cast<PlanarFace>()
                    .FirstOrDefault(f => f.FaceNormal.Normalize().IsAlmostEqualTo(-XYZ.BasisZ));

                if (openFace == null) continue;

                List<CurveLoop> openLoops = [.. openFace.GetEdgesAsCurveLoops()];

                Solid tempSolid1 = GeometryCreationUtilities.CreateExtrusionGeometry(openLoops, -XYZ.BasisZ, dz1);
                Solid tempSolid2 = GeometryCreationUtilities.CreateExtrusionGeometry(openLoops, XYZ.BasisZ, dz2);

                Solid extendSolid = BooleanOperationsUtils.ExecuteBooleanOperation(tempSolid1, tempSolid2, BooleanOperationsType.Union);

                if (extendSolid != null) BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solid, extendSolid, BooleanOperationsType.Union);
            }
        }

        public static Solid UnionSolids(this List<Solid> solids)
        {
            if (!solids.Any()) return null;
            Solid result = solids.First();
            solids.Skip(1).ToList().ForEach(s => BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(result, s, BooleanOperationsType.Union));
            return result;
        }
        #endregion
    }
}
