using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

using Line = Autodesk.Revit.DB.Line;
using Parameter = Autodesk.Revit.DB.Parameter;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Tools;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryFamilyInstanceCache : GeometryCache
    {
        // Поля
        private Plane plane;

        // Конструкторы
        public GeometryFamilyInstanceCache(Element elem) : base(elem) { }
        public GeometryFamilyInstanceCache(FamilyInstance inst) : base(inst)
        {
            preCalculations = new List<object>
            {
                inst.GetTransform().BasisX,
                inst.GetTransform().BasisY,
            };
            if (inst.Location is LocationPoint) preCalculations.Add((inst.Location as LocationPoint).Point - BasePoint.Position);

            else preCalculations.Add(inst.GetTransform().Origin - BasePoint.Position);
            if (inst.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM) != null)
                preCalculations.Add(inst.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).AsDouble());
            else preCalculations.Add(0);


            Dirs = new Directions
            {
                X = preCalculations[0] as XYZ,
                Y = preCalculations[1] as XYZ,
                Z = (preCalculations[0] as XYZ).CrossProduct(preCalculations[1] as XYZ)
            };
            List<double> HB = new List<double>();
            /*List<Parameter> parsHB = GetHBParameters(inst);
            if (parsHB.First() != null && parsHB.Last() != null)
            {
                HB.Add(parsHB.First().AsDouble());
                HB.Add(parsHB.Last().AsDouble());
            }
            else
            {
                BoundingBoxXYZ bbox = inst.get_BoundingBox(null);
                HB.Add(bbox.Max.Z - bbox.Min.Z);
                HB.Add(bbox.Max.Y - bbox.Min.Y);
            }*/

            BoundingBoxXYZ bbox = inst.get_BoundingBox(null);
            HB.Add(bbox.Max.Z - bbox.Min.Z);
            HB.Add(bbox.Max.Y - bbox.Min.Y);
            HB.Add(bbox.Max.X - bbox.Min.X);

            Dims = new ControlDimensions
            {
                H = HB[0],
                B = HB[1],
                L = HB[2],
                ZBot = (preCalculations[2] as XYZ).Z,
                ZTop = (preCalculations[2] as XYZ).Z + HB.First(),
                OffsetBot = (double)preCalculations[3],
                OffsetTop = (double)preCalculations[3] + HB.First()
            };

            Origins = new ControlPoints
            {
                CenterStartBottom = (preCalculations[2] as XYZ) - Dirs.X * Dims.L / 2,
                CenterMiddleBottom = preCalculations[2] as XYZ,
                CenterEndBottom = (preCalculations[2] as XYZ) + Dirs.X * Dims.L / 2,
                CenterMiddleMiddle = (preCalculations[2] as XYZ) + Dirs.Z * Dims.H / 2,
                CenterStartTop = (preCalculations[2] as XYZ) - Dirs.X * Dims.L / 2 + Dirs.Z * Dims.H,
                CenterMiddleTop = (preCalculations[2] as XYZ) + Dirs.Z * Dims.H,
                CenterEndTop = (preCalculations[2] as XYZ) + Dirs.X * Dims.L / 2 + Dirs.Z * Dims.H,
            };
        }

        // Методы
        public override void GetSolidData()
        {
            base.GetSolidData();
            GetExtendedFacesData();
        }
        public void GetWallVoidSolid(WallCache wallCache)
        {
            //Построение твёрдого тела для отверстия на основе невидимых линий
            Options opt = new Options();
            opt.IncludeNonVisibleObjects = true;
            List<GeometryObject> geometry = Elem.get_Geometry(opt).ToList();
            List<GeometryObject> geomInst = (geometry.Find(geom => geom is GeometryInstance) as GeometryInstance).GetInstanceGeometry().ToList();
            List<Line> lines = (from geom in geomInst
                                where geom is Line
                                select geom as Line).ToList();

            //Выборка невидимых линий, лежащих на одной из фронтальных граней стены
            List<Line> contourLines = new List<Line>();

            if (wallCache.Geom.Solid == null) wallCache.Geom.GetSolidData();
            plane = wallCache.Geom.UnboundFaces.SideLong.First();
            foreach (Line line in lines)
            {
                plane.Project(line.Evaluate(0.5, true), out UV uv, out double dst);
                if (Math.Round(dst * 304.8) == 0) contourLines.Add(line);
            }

            //Построение контура выдавливания твёрдого тела по точкам
            /*List<XYZ> contourPoints = (from line in contourLines
                                       select line.GetEndPoint(0)).ToList();
            foreach (Line line in contourLines)
            {
                XYZ p1 = line.GetEndPoint(1);
                bool next = false;
                foreach (XYZ point in contourPoints) 
                    if (point.IsAlmostEqualTo(p1)) { next = true; break; }
                if (next) continue;
                else { contourPoints.Add(p1); break; }
            }
            startPoint = new XYZ(contourPoints.Sum(p => p.X)/contourPoints.Count, 
                                 contourPoints.Sum(p => p.Y) / contourPoints.Count, 
                                 contourPoints.Sum(p => p.Z) / contourPoints.Count);

            contourPoints = contourPoints.OrderBy(AngleBetweenContourPoints).ToList();*/
            List<XYZ> contourPoints = GeometryTools.GetOrderedVerticesFromLineSet(contourLines);

            CurveLoop cl = new CurveLoop();
            for (int i = 0; i < contourPoints.Count - 1; i++) cl.Append(Line.CreateBound(contourPoints[i], contourPoints[i + 1]));
            cl.Append(Line.CreateBound(contourPoints.Last(), contourPoints.First()));

            //Создание твёрдого тела и сбор данных по граням
            Solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { cl }, -plane.Normal, wallCache.Geom.Dims.T);
            GetPrimaryFacesData();
            GetExtendedFacesData();
        }


        private protected List<Parameter> GetHBParameters(FamilyInstance inst)
        {
            List<Parameter> pars = new List<Parameter>();

            Parameter parH = null;
            if (inst.get_Parameter(new Guid(p_Dims_Height)) != null && inst.get_Parameter(new Guid(p_Dims_Height)).HasValue) parH = inst.get_Parameter(new Guid(p_Dims_Height));
            else if (inst.get_Parameter(BuiltInParameter.GENERIC_HEIGHT) != null && inst.get_Parameter(BuiltInParameter.GENERIC_HEIGHT).HasValue) parH = inst.get_Parameter(BuiltInParameter.GENERIC_HEIGHT);
            else if (inst.Symbol.get_Parameter(new Guid(p_Dims_Height)) != null && inst.Symbol.get_Parameter(new Guid(p_Dims_Height)).HasValue) parH = inst.Symbol.get_Parameter(new Guid(p_Dims_Height));
            else if (inst.Symbol.GetParameters("Высота двери").Count > 0) parH = inst.Symbol.GetParameters("Высота двери").First();
            else if (inst.Symbol.GetParameters("Высота балки").Count > 0) parH = inst.Symbol.GetParameters("Высота балки").First();
            else if (inst.Symbol.get_Parameter(BuiltInParameter.GENERIC_HEIGHT) != null) parH = inst.Symbol.get_Parameter(BuiltInParameter.GENERIC_HEIGHT);
            else if (inst.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM) != null) parH = inst.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_HEIGHT_PARAM);
            else if (inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT) != null) parH = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst.Symbol, "H")) parH = inst.Symbol.GetParameters("H").First();
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst.Symbol, "hw")) parH = inst.Symbol.GetParameters("hw").First();
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst, "Длина")) parH = inst.GetParameters("Длина").First();
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst.Symbol, "b")) parH = inst.Symbol.GetParameters("b").First();
            pars.Add(parH);

            Parameter parB = null;
            if (inst.get_Parameter(new Guid(p_Dims_Width)) != null && inst.get_Parameter(new Guid(p_Dims_Width)).HasValue) parB = inst.get_Parameter(new Guid(p_Dims_Width));
            else if (inst.get_Parameter(BuiltInParameter.DOOR_WIDTH) != null && inst.get_Parameter(BuiltInParameter.DOOR_WIDTH).HasValue) parB = inst.get_Parameter(BuiltInParameter.DOOR_WIDTH);
            else if (inst.Symbol.get_Parameter(new Guid(p_Dims_Width)) != null) parB = inst.Symbol.get_Parameter(new Guid(p_Dims_Width));
            else if (inst.Symbol.GetParameters("Ширина балки").Count > 0) parB = inst.Symbol.GetParameters("Ширина балки").First();
            else if (inst.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH) != null) parB = inst.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH);
            else if (inst.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM) != null) parB = inst.Symbol.get_Parameter(BuiltInParameter.FAMILY_ROUGH_WIDTH_PARAM);
            else if (inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH) != null) parB = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst.Symbol, "Ширина марша")) parB = inst.Symbol.GetParameters("Ширина марша").First();
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst, "Ширина")) parB = inst.GetParameters("Ширина").First();
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst.Symbol, "b")) parB = inst.Symbol.GetParameters("b").First();
            else if (ElementParametersAssistant.IsParameterExistAndHasValue(inst.Symbol, "bf")) parB = inst.Symbol.GetParameters("bf").First();
            pars.Add(parB);

            return pars;
        }

        //private double AngleBetweenContourPoints(XYZ point) { return hostCache.Geom.Dirs.X.AngleOnPlaneTo((point - startPoint), plane.Normal); }
    }
}
