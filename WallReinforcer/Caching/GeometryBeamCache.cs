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
            List<Parameter> parsHB = GetLBHParameters(beam);
            preCalculations = new List<object>
            {
                parsHB[1].AsDouble(),
                parsHB[2].AsDouble(),
                beam.GetTransform().BasisX,
                beam.GetTransform().BasisY,
                parsHB[0].AsDouble()
            };

            LvlIds = new LevelIds
            {
                Base = beam.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM).AsElementId()
            };
            Dirs = new Directions
            {
                X = preCalculations[2] as XYZ,
                Y = preCalculations[3] as XYZ,
                Z = (preCalculations[2] as XYZ).CrossProduct(preCalculations[3] as XYZ)
            };
            Dims = new ControlDimensions
            {
                L1 = beam.get_Parameter(BuiltInParameter.INSTANCE_LENGTH_PARAM).AsDouble(),
                L2 = beam.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH).AsDouble(),
                L = (double)preCalculations[4],
                B = (double)preCalculations[0],
                H = (double)preCalculations[1],
                OffsetY = GetOffsetY(beam),
                OffsetZ = GetOffsetZ(beam)
            };
            Origins = new ControlPoints
            {
                CenterStartBottom = (beam.Location as LocationCurve).Curve.GetEndPoint(0) + Dirs.Y * Dims.OffsetY + Dirs.Z * Dims.OffsetZ,
                CenterMiddleBottom = (beam.Location as LocationCurve).Curve.Evaluate(0.5, true) + Dirs.Y * Dims.OffsetY + Dirs.Z * Dims.OffsetZ,
                CenterEndBottom = (beam.Location as LocationCurve).Curve.GetEndPoint(1) + Dirs.Y * Dims.OffsetY + Dirs.Z * Dims.OffsetZ,
            };
            Origins.CenterStartMiddle = Origins.CenterStartBottom + Dirs.Z * Dims.H / 2;
            Origins.CenterMiddleMiddle = Origins.CenterMiddleBottom + Dirs.Z * Dims.H / 2;
            Origins.CenterEndMiddle = Origins.CenterEndBottom + Dirs.Z * Dims.H / 2;

            Lines = new ControlLines
            {
                CenterBot = Line.CreateBound(Origins.CenterStartBottom, Origins.CenterEndBottom)
            };
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

        private double GetOffsetY(FamilyInstance beam)
        {
            if (Dirs.Z.IsAlmostEqualTo(XYZ.BasisZ) || Dirs.Z.IsAlmostEqualTo(-XYZ.BasisZ))
            {
                int yAlign = beam.get_Parameter(BuiltInParameter.Y_JUSTIFICATION).AsInteger();
                double yOffset = beam.get_Parameter(BuiltInParameter.Y_OFFSET_VALUE).AsDouble();
                switch (yAlign)
                {
                    case 0: yOffset += (double)preCalculations[0] / 2; break;
                    case 3: yOffset -= (double)preCalculations[0] / 2; break;
                }
                return yOffset;
            }
            else return 0;
        }
        private double GetOffsetZ(FamilyInstance beam)
        {
            if (Dirs.Z.IsAlmostEqualTo(XYZ.BasisZ) || Dirs.Z.IsAlmostEqualTo(-XYZ.BasisZ))
            {
                int zAlign = beam.get_Parameter(BuiltInParameter.Z_JUSTIFICATION).AsInteger();
                double zOffset = beam.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE).AsDouble();
                switch (zAlign)
                {
                    case 0: zOffset -= (double)preCalculations[1]; break;
                    case 3: break;
                    default: zOffset -= (double)preCalculations[1] / 2; break;
                }
                return zOffset;
            }
            else return 0;
        }
    }
}
