using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

#if REVIT2023 || REVIT2024 || REVIT2025
using static Autodesk.Revit.DB.SpecTypeId;
#endif

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

using Line = Autodesk.Revit.DB.Line;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace RevitOSA.WallReinforcer.Caching
{
    public class VoidCache
    {
        //Поля
        private protected Document doc;

        //Свойства
        public GeometryVoidCache Geom { get; set; }
        public ReinforcementVoidCache Reinf { get; set; }
        public Element Elem { get; set; }
        public ElementType ElemType { get; set; }
        public Element Host { get; set; }
        public RebarHostCache HostCache { get; set; }
        public AttachedElements AttachedElems { get; set; }
        public LintelCache LintelCache { get; set; }
        public bool IsOpening { get; private set; }

        //Конструкторы
        public VoidCache(FamilyInstance inst)
        {
            Elem = inst;
            ElemType = inst.Symbol;
            Host = inst.Host;
            HostCache = null;
            Geom = new GeometryVoidCache(inst);
            Reinf = new ReinforcementVoidCache(inst);

            if (ElemType.FamilyName.Contains("Отверстие")
                || ElemType.FamilyName.Contains("Ниша"))
                IsOpening = true;
            else IsOpening = false;
        }

        public VoidCache(Wall curtain, Wall host)
        {
            Elem = curtain;
            ElemType = curtain.WallType;
            Host = host;
            HostCache = null;
            Geom = new GeometryVoidCache(curtain, host.Width);
            //Reinf = new ReinforcementVoidCache(curtain);

            IsOpening = false;
        }

        //Методы
        public void GetAttachedElemsForWallVoid(double catchDstX, double catchDstZ)
        {
            if (Host is Wall)
            {
                doc = Host.Document;
                AttachedElements attElems = new AttachedElements();
                if (Geom.Solid == null) Geom.GetWallVoidSolid(HostCache as WallCache);
                if (Geom.Solid != null)
                {
                    foreach (PlanarFace face in Geom.Faces.FrontContour)
                    {
                        double catchDst = catchDstZ;
                        //double offsetX = catchDstX;
                        //double offsetY = 0;

                        if (face.FaceNormal.IsAlmostEqualTo(Geom.Dirs.X) || face.FaceNormal.IsAlmostEqualTo(-Geom.Dirs.X))
                        {
                            catchDst = catchDstX;
                            //offsetX = 0;
                            //offsetY = catchDstZ;
                        }

                        List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
                        Solid catchSolid = GetExtendedSolidFromFace(face, 0, 0, catchDst, false);
                        if (catchSolid != null)
                        {
                            List<Element> catchedConcreteElems = (from elem in new ExtractingTools.ConcreteExtractor(doc, catchSolid).ToElements()
                                                                  where elem.Id != Host.Id
                                                                  select elem).ToList();

                            if (face.FaceNormal.IsAlmostEqualTo(Geom.Dirs.Z)) attElems.OnTop = catchedConcreteElems;
                            else if (face.FaceNormal.IsAlmostEqualTo(-Geom.Dirs.Z)) attElems.OnBottom = catchedConcreteElems;
                            else if (face.FaceNormal.IsAlmostEqualTo(-Geom.Dirs.X)) attElems.OnLeft = catchedConcreteElems;
                            else if (face.FaceNormal.IsAlmostEqualTo(Geom.Dirs.X)) attElems.OnRight = catchedConcreteElems;
                        }
                        ;
                    }
                }
                AttachedElems = attElems;
            }
        }
        public Solid GetSolidAroundVoid(double dstX, double dstY)
        {
            if (HostCache.Geom.Solid == null) HostCache.Geom.GetSolidData();
            if (HostCache.Geom.Solid != null)
            {
                Solid solid1 = SolidUtils.Clone(HostCache.Geom.Solid);
                Solid solid2 = GetExtendedSolidFromFace(Geom.Faces.SideLong.First(), dstX, dstY, HostCache.Geom.Dims.T, true);
                Solid solidAroundVoid = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect);

                List<Element> attElems = new List<Element>();
                if (AttachedElems.OnTop != null && AttachedElems.OnTop.Count > 0) attElems.AddRange(AttachedElems.OnTop);
                if (AttachedElems.OnLeft != null && AttachedElems.OnLeft.Count > 0) attElems.AddRange(AttachedElems.OnLeft);
                if (AttachedElems.OnRight != null && AttachedElems.OnRight.Count > 0) attElems.AddRange(AttachedElems.OnRight);
                foreach (Element elem in attElems)
                {
                    GeometryCache geom = new GeometryCache(elem);
                    geom.GetSolidData();
                    Solid solid = BooleanOperationsUtils.ExecuteBooleanOperation(solid2, geom.Solid, BooleanOperationsType.Intersect);
                    BooleanOperationsUtils.ExecuteBooleanOperationModifyingOriginalSolid(solidAroundVoid, solid, BooleanOperationsType.Union);
                }
                return solidAroundVoid;
            }
            return null;
        }
        public FamilySymbol GetVoidTypeFromFamily(Document doc, int familyIntId)
        {
#if REVIT2024 || REVIT2025
            Family fam = doc.GetElement(new ElementId((long)familyIntId)) as Family;
#else
            Family fam = doc.GetElement(new ElementId(familyIntId)) as Family;
#endif
            if (fam != null)
            {
                FamilySymbol voidType = null;
                double voidTypeH = Math.Round(Geom.Dims.H * 304.8);
                double voidTypeL = Math.Round(Geom.Dims.L * 304.8);

                List<FamilySymbol> syms = (from id in fam.GetFamilySymbolIds()
                                           select doc.GetElement(id) as FamilySymbol).ToList();
                foreach (FamilySymbol sym in syms)
                {
                    List<Parameter> pars = GetHBParameters(sym);
                    double symH = Math.Round(pars.First().AsDouble() * 304.8);
                    double symL = Math.Round(pars.Last().AsDouble() * 304.8);
                    if (symH == voidTypeH && symL == voidTypeL)
                    {
                        voidType = sym;
                        break;
                    }
                }
                if (voidType == null)
                {
                    string voidTypeName = voidTypeL.ToString() + "x" + voidTypeH.ToString() + "(h)";
                    voidType = syms.Last().Duplicate(voidTypeName) as FamilySymbol;
                    List<Parameter> pars = GetHBParameters(voidType);
                    pars.First().Set(Geom.Dims.H);
                    pars.Last().Set(Geom.Dims.L);
                }

                return voidType;
            }
            return null;
        }
        public static FamilySymbol GetOpeningTypeFromFamily(Document doc, int familyIntId)
        {
#if REVIT2024 || REVIT2025
            Family fam = doc.GetElement(new ElementId((long)familyIntId)) as Family;
#else
            Family fam = doc.GetElement(new ElementId(familyIntId)) as Family;
#endif
            if (fam != null)
            {
                List<FamilySymbol> syms = (from id in fam.GetFamilySymbolIds()
                                           select doc.GetElement(id) as FamilySymbol).ToList();
                FamilySymbol openingType = (from sym in syms
                                            where sym.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString() == "Стандарт"
                                            select sym).FirstOrDefault();
                if (openingType == null) openingType = syms.Last().Duplicate("Стандарт") as FamilySymbol;
                return openingType;
            }
            return null;
        }
        public double GetRemaningOfWallHeight()
        {
            if (HostCache == null) HostCache = new WallCache(Host);
            double remaning = HostCache.Geom.Dims.ZTop - Geom.Dims.ZTop;
            return remaning;
        }
        public Solid GetWallRegionSolid()
        {
            if (HostCache == null) HostCache = new WallCache(Host);
            XYZ p0 = Geom.Origins.CenterMiddleBottom + Geom.Dirs.Z * 10 / 304.8 - Geom.Dirs.X * (Geom.Dims.B / 2 + 10 / 304.8);
            XYZ p1 = Geom.Origins.CenterMiddleBottom + Geom.Dirs.Z * 10 / 304.8 + Geom.Dirs.X * (Geom.Dims.B / 2 * 10 / 304.8);
            Line cutLine = Line.CreateBound(p0, p1);
            foreach (WallRegionCache regionCache in (HostCache as WallCache).Regions)
            {
                if (regionCache.Geom.Solid == null) regionCache.Geom.GetSolidData();
                List<Curve> spotLines = regionCache.Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                if (spotLines.Count > 0) return regionCache.Geom.Solid;
            }
            return null;
        }


        private List<Parameter> GetHBParameters(FamilySymbol sym)
        {
            List<Parameter> pars = new List<Parameter>();

            Parameter parH = null;
            if (sym.get_Parameter(new Guid(p_Dims_Height)) != null) parH = sym.get_Parameter(new Guid(p_Dims_Height));
            else if (sym.get_Parameter(BuiltInParameter.GENERIC_HEIGHT) != null) parH = sym.get_Parameter(BuiltInParameter.GENERIC_HEIGHT);
            else if (sym.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM) != null) parH = sym.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM);
            pars.Add(parH);

            Parameter parB = null;
            if (sym.get_Parameter(new Guid(p_Dims_Width)) != null) parB = sym.get_Parameter(new Guid(p_Dims_Width));
            else if (sym.get_Parameter(BuiltInParameter.DOOR_WIDTH) != null) parH = sym.get_Parameter(BuiltInParameter.DOOR_WIDTH);
            else if (sym.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM) != null) parH = sym.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM);
            pars.Add(parH);

            return pars;
        }
        private Solid GetExtendedSolidFromFace(PlanarFace face, double dstX, double dstY, double extrudeDst, bool inversed)
        {
            if (Geom.Solid == null) Geom.GetSolidData();
            if (Geom.Solid != null)
            {
                XYZ xDir = Geom.Dirs.X;
                XYZ yDir = Geom.Dirs.Y;
                if (Host is Wall) yDir = Geom.Dirs.Z;

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
                Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(newCLs, extrudeDir, extrudeDst);
                return solid;
            }
            return null;
        }

    }
}
