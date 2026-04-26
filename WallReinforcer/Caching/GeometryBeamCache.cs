using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryBeamCache : GeometryFamilyInstanceCache
    {
        public GeometryBeamCache(FamilyInstance beam) : base(beam as Element)
        {
            // 1. Получаем параметры размеров
            List<Parameter> parsHB = GetLBHParameters(beam);
            double width = parsHB[1].AsDouble();
            double height = parsHB[2].AsDouble();
            double lengthParam = parsHB[0].AsDouble();

            // 2. Получаем направления из трансформации
            XYZ basisX = beam.GetTransform().BasisX;
            XYZ basisY = beam.GetTransform().BasisY;

            // 3. Инициализация уровней
            LvlIds = new LevelIds
            {
                Base = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId()
            };

            // 4. Инициализация направлений
            Dirs = new Directions
            {
                X = basisX,
                Y = basisY,
                Z = basisX.CrossProduct(basisY)
            };

            // 5. Расчет размеров и координат
            double length1 = beam.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble();
            double length2 = beam.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH).AsDouble();
            double offsetY = GetOffsetY(beam, width, height);
            double offsetZ = GetOffsetZ(beam, width, height);

            Dims = new ControlDimensions
            {
                L1 = length1,
                L2 = length2,
                L = lengthParam,
                B = width,
                H = height,
                OffsetY = offsetY,
                OffsetZ = offsetZ
            };

            // 6. Инициализация контрольных точек
            Curve locationCurve = (beam.Location as LocationCurve)?.Curve;
            if (locationCurve == null)
                throw new InvalidOperationException("Beam location is not a curve.");

            XYZ shiftVector = Dirs.Y * Dims.OffsetY + Dirs.Z * Dims.OffsetZ;
            
            Origins = new ControlPoints
            {
                CenterStartBottom = locationCurve.GetEndPoint(0) + shiftVector,
                CenterMiddleBottom = locationCurve.Evaluate(0.5, true) + shiftVector,
                CenterEndBottom = locationCurve.GetEndPoint(1) + shiftVector,
            };
            Origins.CenterStartMiddle = Origins.CenterStartBottom + Dirs.Z * Dims.H / 2;
            Origins.CenterMiddleMiddle = Origins.CenterMiddleBottom + Dirs.Z * Dims.H / 2;
            Origins.CenterEndMiddle = Origins.CenterEndBottom + Dirs.Z * Dims.H / 2;

            // 7. Инициализация линий
            Lines = new ControlLines
            {
                CenterBot = Line.CreateBound(Origins.CenterStartBottom, Origins.CenterEndBottom)
            };

            // 8. BoundingBox
            Outline = new Outline(beam.get_BoundingBox(null).Min, beam.get_BoundingBox(null).Max);
        }

        public override void GetSolidData()
        {
            base.GetSolidData();
            GetExtendedFacesData();
        }
        private List<Parameter> GetLBHParameters(FamilyInstance inst)
        {
            List<Parameter> pars = new List<Parameter>();

            Parameter parL = null;
            if (inst.get_Parameter(new Guid(p_Dims_Length)) != null
                && inst.get_Parameter(new Guid(p_Dims_Length)).HasValue) parL = inst.get_Parameter(new Guid(p_Dims_Length));
            pars.Add(parL);

            Parameter parB = null;
            if (inst.Symbol.get_Parameter(new Guid(p_Dims_Width)) != null
                && inst.Symbol.get_Parameter(new Guid(p_Dims_Width)).HasValue) parB = inst.Symbol.get_Parameter(new Guid(p_Dims_Width));
            else if (inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH) != null
                && inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH).HasValue) parB = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
            pars.Add(parB);

            Parameter parH = null;
            if (inst.Symbol.get_Parameter(new Guid(p_Dims_Height)) != null
                && inst.Symbol.get_Parameter(new Guid(p_Dims_Height)).HasValue) parH = inst.Symbol.get_Parameter(new Guid(p_Dims_Height));
            else if (inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT) != null
                && inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT).HasValue) parH = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
            pars.Add(parH);

            return pars;
        }

        private double GetOffsetY(FamilyInstance beam, double width, double height)
        {
            if (Dirs.Z.IsAlmostEqualTo(XYZ.BasisZ) || Dirs.Z.IsAlmostEqualTo(-XYZ.BasisZ))
            {
                int yAlign = beam.get_Parameter(BuiltInParameter.Y_JUSTIFICATION).AsInteger();
                double yOffset = beam.get_Parameter(BuiltInParameter.Y_OFFSET_VALUE).AsDouble();
                switch (yAlign)
                {
                    case 0: yOffset += width / 2; break;
                    case 3: yOffset -= width / 2; break;
                }
                return yOffset;
            }
            else return 0;
        }
        private double GetOffsetZ(FamilyInstance beam, double width, double height)
        {
            if (Dirs.Z.IsAlmostEqualTo(XYZ.BasisZ) || Dirs.Z.IsAlmostEqualTo(-XYZ.BasisZ))
            {
                int zAlign = beam.get_Parameter(BuiltInParameter.Z_JUSTIFICATION).AsInteger();
                double zOffset = beam.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE).AsDouble();
                switch (zAlign)
                {
                    case 0: zOffset -= height; break;
                    case 3: break;
                    default: zOffset -= height / 2; break;
                }
                return zOffset;
            }
            else return 0;
        }
    }
}
