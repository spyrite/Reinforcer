using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Assistants
{
    public static class RebarBarTypeAssistant
    {
        private static readonly List<BuiltInParameter> builtInParameterNames = new List<BuiltInParameter>
            {
                BuiltInParameter.MATERIAL_ID_PARAM,
                BuiltInParameter.REBAR_BAR_DIAMETER,
                BuiltInParameter.REBAR_BAR_STIRRUP_BEND_DIAMETER,
                BuiltInParameter.REBAR_BAR_STYLE,
                BuiltInParameter.REBAR_STANDARD_BEND_DIAMETER,
                BuiltInParameter.REBAR_STANDARD_HOOK_BEND_DIAMETER,
                BuiltInParameter.SYMBOL_NAME_PARAM,
                BuiltInParameter.REBAR_BAR_DEFORMATION_TYPE,
#if REVIT2024 || REVIT2025
                BuiltInParameter.REBAR_MODEL_BAR_DIAMETER
#else
                BuiltInParameter.REBAR_BAR_DIAMETER
#endif
        };
        private static readonly List<string> userParameterNames = new List<string>
            {
                "1П_Размер_В погонных метрах",
                "1П_Арм_Класс",
                "1П_Обозначение",
                "1П_Наименование",
                "1П_Арм_Семейство",
            };

        public static RebarBarType GetOrCreateRebarBarType(Document doc, double d1, double d2, string rClass, bool inRM)
        {
            FilteredElementCollector allBarTypes = new FilteredElementCollector(doc).OfClass(typeof(RebarBarType));
            foreach (RebarBarType barType in allBarTypes.Cast<RebarBarType>())
            {
                string currentName = barType.get_Parameter(builtInParameterNames[6]).AsString();
                double currentD = Math.Round(barType.get_Parameter(builtInParameterNames[1]).AsDouble() * 304.8);
                double currentDBend = Math.Round(barType.get_Parameter(builtInParameterNames[2]).AsDouble() * 304.8);
                bool currentInRM = false;
                if (barType.GetParameters(userParameterNames[0]).Count > 0
                    && barType.GetParameters(userParameterNames[0])[0].HasValue
                    && barType.GetParameters(userParameterNames[0])[0].AsInteger() == 1)
                    currentInRM = true;
                bool bendCondition = true;
                if (d2 > 0) bendCondition = currentDBend == Math.Round(d2 * 304.8);
                if (currentName.Contains(rClass)
                    && currentD == Math.Round(d1 * 304.8)
                    && currentInRM == inRM
                    && int.TryParse(currentName.Split('_').Last(), out _)
                    && bendCondition)
                    return barType;
            }
            RebarBarType newBarType = null;
            string suffix = string.Empty;
            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Создание нового типа арматурного стержня");
                newBarType = RebarBarType.Create(doc);
                if (d2 > 0) suffix += "_Загиб D" + ((int)Math.Round(d2 * 304.8)).ToString();
                if (inRM) suffix += "_ПМ";
                newBarType.Name = ((int)Math.Round(d1 * 304.8)).ToString() + '-' + rClass + suffix;
                SetRebarBarTypeParameters(doc, newBarType, d1, d2, rClass, inRM);
                tx.Commit();
            }
            return newBarType;
        }
        private static void SetRebarBarTypeParameters(Document doc, RebarBarType barType, double d1, double d2, string rClass, bool inRM)
        {
            List<string> values = new List<string>()
                {
                    (inRM ? 1 : 0).ToString(),
                    rClass.Split('А').Last().Split('С').First(),
                    "ГОСТ 34028-2016",
                    rClass,
                    "0",
                };
            for (int i = 0; i < values.Count; i++)
            {
                if (barType.GetParameters(userParameterNames[i]).Count > 0)
                    barType.GetParameters(userParameterNames[i])[0].SetValueString(values[i]);
            }

            if (rClass == "А240") barType.get_Parameter(builtInParameterNames[7]).Set(1);
            else barType.get_Parameter(builtInParameterNames[7]).Set(0);

            barType.get_Parameter(builtInParameterNames[1]).Set(d1);
            barType.get_Parameter(builtInParameterNames[8]).Set(d1);

            double db;
            if (d2 > 0) db = d2;
            else db = ReinforcementTools.ComputeBendDiameter(d1, rClass);
            barType.get_Parameter(builtInParameterNames[2]).Set(db);
            barType.get_Parameter(builtInParameterNames[4]).Set(db);
            barType.get_Parameter(builtInParameterNames[5]).Set(db);

            FilteredElementCollector col = new FilteredElementCollector(doc).OfClass(typeof(Material));
            ElementId materialId = (from material in col
                                    where material.Name.StartsWith("Арматура D")
                                    && int.Parse(material.Name.Split('D').Last()) == (int)Math.Round(d1 * 304.8)
                                    select material.Id).First();
            if (materialId != null) barType.get_Parameter(builtInParameterNames[0]).Set(materialId);

            ElementId subCatId = (from Category subCat in Category.GetCategory(doc, BuiltInCategory.OST_Rebar).SubCategories
                                  where subCat.Name == "D" + ((int)Math.Round(d1 * 304.8)).ToString()
                                  select subCat.Id).First();
            if (subCatId != null) barType.get_Parameter(builtInParameterNames[3]).Set(subCatId);
        }
    }
}
