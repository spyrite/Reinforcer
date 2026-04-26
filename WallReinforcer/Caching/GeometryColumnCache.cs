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
            // 1. Получаем параметры размеров и смещений
            List<Parameter> parsLTH = GetLTHParameters(column);
            double length = parsLTH[0].AsDouble();
            double width = parsLTH[1].AsDouble();
            double height = parsLTH[2].AsDouble();
            
            double baseOffset = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).AsDouble();
            double topOffset = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM).AsDouble();

            // 2. Инициализация уровней
            LvlIds = new LevelIds
            {
                Bot = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId(),
                Top = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId(),
                Base = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId()
            };

            // 3. Инициализация направлений
            Dirs = new Directions
            {
                X = column.GetTransform().BasisX,
                Y = column.GetTransform().BasisY,
                Z = XYZ.BasisZ
            };

            // 4. Расчет размеров и координат
            Level baseLevel = doc.GetElement(LvlIds.Bot) as Level;
            if (baseLevel == null)
                throw new InvalidOperationException("Base level not found.");

            double baseLevelElevation = baseLevel.Elevation;
            double zBot = baseLevelElevation + baseOffset;
            double zTop = zBot + height;

            Dims = new ControlDimensions
            {
                L = length,
                T = width,
                H = height,
                OffsetBot = baseOffset,
                OffsetTop = topOffset,
                ZBot = zBot,
                ZTop = zTop
            };

            // 5. Инициализация контрольных точек
            XYZ centerPoint = (column.Location as LocationPoint).Point + Dirs.Z * zBot;
            
            Origins = new ControlPoints
            {
                CenterMiddleBottom = centerPoint,
                CenterStartBottom = centerPoint - Dirs.X * Dims.L / 2,
                CenterEndBottom = centerPoint + Dirs.X * Dims.L / 2,
                CenterMiddleMiddle = centerPoint + Dirs.Z * Dims.H / 2,
                CenterMiddleTop = centerPoint + Dirs.Z * Dims.H,
                CenterStartTop = centerPoint + Dirs.Z * Dims.H - Dirs.X * Dims.L / 2,
                CenterEndTop = centerPoint + Dirs.Z * Dims.H + Dirs.X * Dims.L / 2
            };

            // 6. Инициализация линий
            Lines = new ControlLines
            {
                CenterBot = Line.CreateBound(Origins.CenterStartBottom, Origins.CenterEndBottom),
                CenterTop = Line.CreateBound(Origins.CenterStartTop, Origins.CenterEndTop)
            };

            // 7. BoundingBox
            Outline = new Outline(column.get_BoundingBox(null).Min, column.get_BoundingBox(null).Max);
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
