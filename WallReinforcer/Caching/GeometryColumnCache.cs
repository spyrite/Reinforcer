using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryColumnCache : GeometryFamilyInstanceCache
    {
        public GeometryColumnCache(FamilyInstance column) : base(column as Element)
        {
            List<Parameter> parsLTH = GetLTHParameters(column);
            preCalculations = new List<object>
            {
                parsLTH[0].AsDouble(),
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).AsDouble()
            };
            LvlIds = new LevelIds
            {
                Bot = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId(),
                Top = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId(),
                Base = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId()
            };
            Dirs = new Directions
            {
                X = column.GetTransform().BasisX,
                Y = column.GetTransform().BasisY,
                Z = XYZ.BasisZ
            };
            Dims = new ControlDimensions
            {
                L = parsLTH[0].AsDouble(),
                T = parsLTH[1].AsDouble(),
                H = parsLTH[2].AsDouble(),
                OffsetBot = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).AsDouble(),
                OffsetTop = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM).AsDouble(),
                ZBot = (doc.GetElement(LvlIds.Bot) as Level).Elevation + (double)preCalculations[1],
                ZTop = (doc.GetElement(LvlIds.Bot) as Level).Elevation + (double)preCalculations[1] + (double)preCalculations[0]
            };
            Origins = new ControlPoints();
            Origins.CenterMiddleBottom = (column.Location as LocationPoint).Point + Dirs.Z * (Dims.ZBot); //+ BasePoint.Position.Z);
            Origins.CenterStartBottom = Origins.CenterMiddleBottom - Dirs.X * Dims.L / 2;
            Origins.CenterEndBottom = Origins.CenterMiddleBottom + Dirs.X * Dims.L / 2;
            Origins.CenterMiddleMiddle = Origins.CenterMiddleBottom + Dirs.Z * Dims.H / 2;
            Origins.CenterMiddleTop = Origins.CenterMiddleBottom + Dirs.Z * Dims.H;
            Origins.CenterStartTop = Origins.CenterMiddleTop - Dirs.X * Dims.L / 2;
            Origins.CenterEndTop = Origins.CenterMiddleTop + Dirs.X * Dims.L / 2;

            Lines = new ControlLines
            {
                CenterBot = Line.CreateBound(Origins.CenterStartBottom, Origins.CenterEndBottom),
                CenterTop = Line.CreateBound(Origins.CenterStartTop, Origins.CenterEndTop)
            };
        }

        public override void GetSolidData()
        {
            base.GetSolidData();
            GetExtendedFacesData();
        }

        public override void GetSolidData(Document targetDoc)
        {
            base.GetSolidData(targetDoc);
            GetExtendedFacesData();
        }

        private List<Parameter> GetLTHParameters(FamilyInstance inst)
        {
            List<Parameter> pars = new List<Parameter>();

            Parameter parL = null;
            if (inst.Symbol.get_Parameter(new Guid(p_Dims_Length)) != null
                && inst.Symbol.get_Parameter(new Guid(p_Dims_Length)).HasValue) parL = inst.Symbol.get_Parameter(new Guid(p_Dims_Length));
            else if (inst.Symbol.get_Parameter(new Guid(p_Dims_Height)) != null
                && inst.Symbol.get_Parameter(new Guid(p_Dims_Height)).HasValue) parL = inst.Symbol.get_Parameter(new Guid(p_Dims_Height));
            else if (inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT) != null
                && inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT).HasValue) parL = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_HEIGHT);
            pars.Add(parL);

            Parameter parT = null;
            if (inst.Symbol.get_Parameter(new Guid(p_Dims_Width)) != null
                && inst.Symbol.get_Parameter(new Guid(p_Dims_Width)).HasValue) parT = inst.Symbol.get_Parameter(new Guid(p_Dims_Width));
            if (inst.Symbol.get_Parameter(new Guid(p_Dims_Thickness)) != null
                && inst.Symbol.get_Parameter(new Guid(p_Dims_Thickness)).HasValue) parT = inst.Symbol.get_Parameter(new Guid(p_Dims_Thickness));
            else if (inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH) != null
                && inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH).HasValue) parT = inst.Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH);
            pars.Add(parT);

            Parameter parH = null;
            if (inst.get_Parameter(new Guid(p_Dims_Height)) != null
                && inst.get_Parameter(new Guid(p_Dims_Height)).HasValue) parH = inst.get_Parameter(new Guid(p_Dims_Height));
            else if (inst.get_Parameter(new Guid(p_Dims_Length)) != null
                && inst.get_Parameter(new Guid(p_Dims_Length)).HasValue) parH = inst.get_Parameter(new Guid(p_Dims_Length));
            pars.Add(parH);

            return pars;
        }
    }
}
