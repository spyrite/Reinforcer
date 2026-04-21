using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

namespace RevitOSA.WallReinforcer.Resources1P
{
    public static class FamilyStandardParameters
    {
        public static readonly List<string> LintelElementTypeParameterNames = new List<string>
        {
            "Перемычка 1",
            "Перемычка 2",
            "Перемычка 3",
            "Перемычка 4",
            "Перемычка 5",
            "Перемычка 6",
            "Перемычка 7",
            "Перемычка 8",
            "Перемычка 9"
        };

        public static readonly List<string> LintelElementPositionParameterNames = new List<string>
        {
            "Перемычка 1_Привязка снаружи",
            "от 1й до 2й",
            "от 2й до 3й",
            "от 3й до 4й",
            "от 4й до 5й",
            "от 5й до 6й",
            "от 6й до 7й",
            "от 7й до 8й",
            "от 8й до 9й",
            "Перемычка 1_Смещение вниз"
        };

        public static FamilyParameterData MassDensity = new FamilyParameterData
        {
#if COMPANY_FP
            SourceName = "Плотность материала",
#else
            SourceName = "Материал_Плотность",
#endif
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Materials,
            SpecTypeId = SpecTypeId.MassDensity
#else
            GroupTypeId = BuiltInParameterGroup.PG_MATERIALS,
            SpecTypeId = ParameterType.MassDensity
#endif
        };
        public static FamilyParameterData SectionArea = new FamilyParameterData
        {
            SourceName = "Площадь сечения",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.AnalysisResults,
            SpecTypeId = SpecTypeId.Area,
#else
            GroupTypeId = BuiltInParameterGroup.PG_ANALYSIS_RESULTS,
            SpecTypeId = ParameterType.Area
#endif
        };
        public static FamilyParameterData SubComponentVisibility = new FamilyParameterData
        {
            SourceName = "Видимость деталей",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Visibility,
            SpecTypeId = SpecTypeId.Boolean.YesNo,
#else
            GroupTypeId = BuiltInParameterGroup.PG_VISIBILITY,
            SpecTypeId = ParameterType.YesNo
#endif
        };
        public static FamilyParameterData SubComponentMaterial = new FamilyParameterData
        {
            SourceName = "Материал деталей",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Materials,
            SpecTypeId = SpecTypeId.Reference.Material,
#else
            GroupTypeId = BuiltInParameterGroup.PG_MATERIALS,
            SpecTypeId = ParameterType.Material
#endif
        };
        public static FamilyParameterData Material = new FamilyParameterData
        {
            SourceName = "Материал",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Materials,
            SpecTypeId = SpecTypeId.Reference.Material
#else
            GroupTypeId = BuiltInParameterGroup.PG_MATERIALS,
            SpecTypeId = ParameterType.Material
#endif
        };
        public static FamilyParameterData Length_Nominal = new FamilyParameterData
        {
            SourceName = "Длина_Номинальная",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Geometry,
            SpecTypeId = SpecTypeId.Length,
#else
            GroupTypeId = BuiltInParameterGroup.PG_GEOMETRY,
            SpecTypeId = ParameterType.Length
#endif
        };
        public static FamilyParameterData Length_Model = new FamilyParameterData
        {
            SourceName = "Длина_Конструктивная",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Geometry,
            SpecTypeId = SpecTypeId.Length,
#else
            GroupTypeId = BuiltInParameterGroup.PG_GEOMETRY,
            SpecTypeId = ParameterType.Length
#endif
        };
        public static FamilyParameterData Width_Nominal = new FamilyParameterData
        {
            SourceName = "Ширина_Номинальная",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Geometry,
            SpecTypeId = SpecTypeId.Length,
#else
            GroupTypeId = BuiltInParameterGroup.PG_GEOMETRY,
            SpecTypeId = ParameterType.Length
#endif
        };
        public static FamilyParameterData Width_Model = new FamilyParameterData
        {
            SourceName = "Ширина_Конструктивная",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Geometry,
            SpecTypeId = SpecTypeId.Length,
#else
            GroupTypeId = BuiltInParameterGroup.PG_GEOMETRY,
            SpecTypeId = ParameterType.Length
#endif
        };
        public static FamilyParameterData Height = new FamilyParameterData
        {
            SourceName = "Высота",
#if REVIT2023 || REVIT2024 || REVIT2025
            GroupTypeId = GroupTypeId.Geometry,
            SpecTypeId = SpecTypeId.Length,
#else
            GroupTypeId = BuiltInParameterGroup.PG_GEOMETRY,
            SpecTypeId = ParameterType.Length
#endif
        };
#if REVIT2023 || REVIT2024 || REVIT2025
        private static readonly Dictionary<string, ForgeTypeId> definitionGroupTypeIds = new Dictionary<string, ForgeTypeId>
        {
            { p_Mark_Complicate_Length1, GroupTypeId.OverallLegend },
            { p_Mark_Complicate_Length2, GroupTypeId.OverallLegend },
            { p_Mark_Complicate_Length3, GroupTypeId.OverallLegend },
            { p_Mark_Complicate_Text1, GroupTypeId.OverallLegend },
            { p_Mark_Complicate_Text2, GroupTypeId.OverallLegend },
            { p_Identity_Zone, GroupTypeId.IdentityData },
            { p_Identity_Section, GroupTypeId.IdentityData },
            { p_Amount, GroupTypeId.Constraints },
            { p_Dims_InRunningMeters, GroupTypeId.Geometry },
            { p_Dims_Length, GroupTypeId.Geometry },
            { p_Mass, GroupTypeId.Data },
            { p_Mark_Construction, GroupTypeId.IdentityData },
            { p_Mark_Item, GroupTypeId.IdentityData },
            { p_Material, GroupTypeId.Materials },
            { p_Identity_Name, GroupTypeId.OverallLegend },
            { p_Identity_SheetReference, GroupTypeId.OverallLegend }
        };
#else
        private static readonly Dictionary<string, BuiltInParameterGroup> definitionGroupTypeIds = new Dictionary<string, BuiltInParameterGroup>
        {
#if COMPANY_FP
            { p_Mark_Complicate_Length1, BuiltInParameterGroup.PG_OVERALL_LEGEND },
            { p_Mark_Complicate_Length2, BuiltInParameterGroup.PG_OVERALL_LEGEND },
            { p_Mark_Complicate_Length3, BuiltInParameterGroup.PG_OVERALL_LEGEND },
            { p_Mark_Complicate_Text1, BuiltInParameterGroup.PG_OVERALL_LEGEND },
            { p_Mark_Complicate_Text2, BuiltInParameterGroup.PG_OVERALL_LEGEND },
            { p_Identity_Zone, BuiltInParameterGroup.PG_IDENTITY_DATA },
            { p_Identity_Section, BuiltInParameterGroup.PG_IDENTITY_DATA },
            { p_Amount, BuiltInParameterGroup.PG_CONSTRAINTS },
            { p_Dims_InRunningMeters, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Dims_Length, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Mass, BuiltInParameterGroup.PG_DATA },
            { p_Mark_Construction, BuiltInParameterGroup.PG_IDENTITY_DATA },
            { p_Mark_Item, BuiltInParameterGroup.PG_IDENTITY_DATA },
            { p_Material, BuiltInParameterGroup.PG_MATERIALS },
            { p_Identity_Name, BuiltInParameterGroup.PG_OVERALL_LEGEND },
            { p_Identity_SheetReference, BuiltInParameterGroup.PG_OVERALL_LEGEND },
#else
            { p_Amount, BuiltInParameterGroup.PG_COUPLER_ARRAY },
            { ADSK_RowHeight, BuiltInParameterGroup.PG_GRAPHICS },

            { p_Identity_SheetReference, BuiltInParameterGroup.PG_TEXT },
            { p_Identity_Name, BuiltInParameterGroup.PG_TEXT },
            { OLP_Identity_SheetReferenceКМ, BuiltInParameterGroup.PG_TEXT },
            { OLP_Identity_NameKM, BuiltInParameterGroup.PG_TEXT },
            { ADSK_Identity_NameStructureProfile, BuiltInParameterGroup.PG_TEXT },
            { ADSK_Identity_Name_Text1, BuiltInParameterGroup.PG_TEXT },
            { ADSK_Identity_Name_Prefix, BuiltInParameterGroup.PG_TEXT },
            { OLP_Identity_SheetReferenceItem, BuiltInParameterGroup.PG_TEXT },
            { OLP_Identity_NameItem, BuiltInParameterGroup.PG_TEXT },

            { ADSK_Material_Ref, BuiltInParameterGroup.PG_MATERIALS },
            { ADSK_Material_CountType, BuiltInParameterGroup.PG_MATERIALS },
            { ADSK_Material_Units, BuiltInParameterGroup.PG_MATERIALS },
            { p_Material, BuiltInParameterGroup.PG_MATERIALS },
            { OLP_Material_Code, BuiltInParameterGroup.PG_MATERIALS },

            { p_Dims_Diameter, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Dims_Elevation, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Dims_Height, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Dims_Length, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Dims_Thickness, BuiltInParameterGroup.PG_GEOMETRY },
            { p_Dims_Width, BuiltInParameterGroup.PG_GEOMETRY },

            { ADSK_Identity_SheetSet, BuiltInParameterGroup.PG_GENERAL },
            { p_Identity_Level, BuiltInParameterGroup.PG_GENERAL },
            { OLP_Identity_BuildingSection, BuiltInParameterGroup.PG_GENERAL },
            { p_Identity_Building, BuiltInParameterGroup.PG_GENERAL },


            { p_Dims_InRunningMeters, BuiltInParameterGroup.PG_DATA },
            { ADSK_Data_ElemTypeKM, BuiltInParameterGroup.PG_DATA },
            { ADSK_Data_StructureGrouping, BuiltInParameterGroup.PG_DATA },
            { ADSK_Data_MassCountType, BuiltInParameterGroup.PG_DATA },
            { ADSK_MassPerLength, BuiltInParameterGroup.PG_DATA },
            { OLP_MassPerSquare, BuiltInParameterGroup.PG_DATA },
            { p_Mass, BuiltInParameterGroup.PG_DATA },
            { ADSK_Identity_MainItemElement, BuiltInParameterGroup.PG_DATA },
            { p_Identity_MainElement, BuiltInParameterGroup.PG_DATA },
            { OLP_FamVersion, BuiltInParameterGroup.PG_DATA },

            { ADSK_Mark_ElemTakeoff, BuiltInParameterGroup.PG_IDENTITY_DATA },
            { p_Mark_Item, BuiltInParameterGroup.PG_IDENTITY_DATA },
            { p_Mark_Construction, BuiltInParameterGroup.PG_IDENTITY_DATA },
            
#endif
        };
#endif

        private static Dictionary<string, Definition> definitionsByGuid;

        public static void InitializeForShared(Document doc)
        {
            DefinitionFile spf = doc.Application.OpenSharedParameterFile();
#if COMPANY_FP
            if (spf != null && spf.Filename == "P:\\Общие параметры\\1П_Общие параметры.txt")
#else

            if (spf != null && spf.Filename == "\\\\DISKSTATION\\Производство\\Ревит\\REVIT_SETUP\\06_Файл_общих_параметров\\OLP_ФОП_2019.txt")
#endif
            {
                definitionsByGuid = AdaptUtils.GetDefinitionsByGuids(spf);
            }
        }
        public static FamilyParameterData GetFPDOfGuid(string guid, bool isInstance)
        {
            FamilyParameterData fpd = new FamilyParameterData();
            if (Guid.TryParse(guid, out _) && definitionsByGuid != null && definitionsByGuid.ContainsKey(guid))
            {
                fpd.ExternalDefinition = definitionsByGuid[guid] as ExternalDefinition;
                fpd.GroupTypeId = definitionGroupTypeIds[guid];
                fpd.IsInstance = isInstance;
            }
            return fpd;
        }
    }
}
