using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Resources1P;
using System;
using System.Collections.Generic;
using System.Linq;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

using ModelSettings = RevitOSA.WallReinforcer.Properties.Modelling;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class LintelUtils
    {
        private static Document doc;
        private static LintelElementData lintelElementData;

        public static List<Family> families;
        public static List<FamilySymbol> defaultlintelSyms;

        public static FamilySymbol GetOrCreateLintelElementType(Document document, LintelElementData led)
        {
            doc = document;
            lintelElementData = led;
            FamilySymbol linetelElementType = null;

            if (families == null) GetFamiliesAndDefaultFamilyTypes();
            int i = -1;
            switch (lintelElementData.Section)
            {
                case CrossSectionType.Bar: i = 1; break;
                case CrossSectionType.Rebar: i = 2; break;
                case CrossSectionType.Corner: i = 3; break;
                case CrossSectionType.CornerMirrored: i = 3; break;
            }

            if (i != -1 && families[i] != null)
            {
                linetelElementType = (from id in families[i].GetFamilySymbolIds()
                                      where (doc.GetElement(id) as FamilySymbol).get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString() == lintelElementData.TypeName
                                      select doc.GetElement(id) as FamilySymbol).FirstOrDefault();
                if (linetelElementType == null)
                {
                    linetelElementType = (FamilySymbol)defaultlintelSyms[i].Duplicate(lintelElementData.TypeName);
                    doc.Regenerate();
                    if (linetelElementType != null) FillParametersForCreatedLintelElementType(linetelElementType);
                }

                AssociateMatarialParWithGlobalPar(linetelElementType);
            }
            return linetelElementType;
        }
        public static FamilySymbol GetLintelType(Document doc, VoidCache voidCache)
        {
            if (defaultlintelSyms == null) GetFamiliesAndDefaultFamilyTypes();
            List<FamilySymbol> existLintelSyms = (from id in families[0].GetFamilySymbolIds()
                                                  select doc.GetElement(id) as FamilySymbol).ToList();

            foreach (FamilySymbol existLintelSym in existLintelSyms)
            {
                if (existLintelSym.GetParameters("Количество перемычек").First().AsInteger() != voidCache.LintelCache.NLintelElements) continue;

                bool next = false;
                for (int i = 0; i < voidCache.LintelCache.NLintelElements; i++)
                {
                    string parName1 = FamilyStandardParameters.LintelElementTypeParameterNames[i];
                    string parName2 = FamilyStandardParameters.LintelElementPositionParameterNames[i];
                    LintelElementData lintelElementData = voidCache.LintelCache.ElementsData[i];
                    if (existLintelSym.GetParameters(parName1).First().AsElementId() != lintelElementData.Type.Id)
                    {
                        next = true;
                        break;
                    }

                    if (Math.Round(existLintelSym.GetParameters(parName2).First().AsDouble() * 304.8) != Math.Round(voidCache.LintelCache.ElementsPlacementData[parName2] * 304.8))
                    {
                        next = true;
                        break;
                    }
                }
                if (next) continue;

                if (voidCache.LintelCache.SectionType == CrossSectionType.Corner)
                {
                    if (Math.Round(existLintelSym.GetParameters("Пластины_Длина").First().AsDouble() * 304.8) != Math.Round(voidCache.LintelCache.CornerElementsData.PlateL * 304.8)) continue;
                    if (Math.Round(existLintelSym.GetParameters("Пластины_Ширина").First().AsDouble() * 304.8) != Math.Round(voidCache.LintelCache.CornerElementsData.PlateB * 304.8)) continue;
                    if (Math.Round(existLintelSym.GetParameters("Пластины_Толщина").First().AsDouble() * 304.8) != Math.Round(voidCache.LintelCache.CornerElementsData.PlateT * 304.8)) continue;
                    if (Math.Round(existLintelSym.GetParameters("Пластины_Привязка").First().AsDouble() * 304.8) != Math.Round(voidCache.LintelCache.CornerElementsData.PlatesAlign * 304.8)) continue;
                    if (Math.Round(existLintelSym.GetParameters("Пластины_Шаг").First().AsDouble() * 304.8) != Math.Round(voidCache.LintelCache.CornerElementsData.PlatesStep * 304.8)) continue;
                    if (existLintelSym.GetParameters("Пластины_Количество").First().AsInteger() != voidCache.LintelCache.CornerElementsData.PlatesN) continue;
                }

                return existLintelSym;
            }
            return null;
        }
        public static FamilySymbol CreateLintelType(Document doc, VoidCache voidCache)
        {
            if (defaultlintelSyms == null) GetFamiliesAndDefaultFamilyTypes();

            string lintelTypeName = GetNewLintelTypeName(voidCache.LintelCache.SectionType);
            FamilySymbol lintelType = defaultlintelSyms.First().Duplicate(lintelTypeName) as FamilySymbol;

            lintelType.GetParameters("Количество перемычек").First().Set(voidCache.LintelCache.NLintelElements);
            for (int i = 0; i < voidCache.LintelCache.NLintelElements; i++)
            {
                string parName1 = FamilyStandardParameters.LintelElementTypeParameterNames[i];
                string parName2 = FamilyStandardParameters.LintelElementPositionParameterNames[i];
                LintelElementData lintelElementData = voidCache.LintelCache.ElementsData[i];
                lintelType.GetParameters(parName1).First().Set(lintelElementData.Type.Id);
                lintelType.GetParameters(parName2).First().Set(voidCache.LintelCache.ElementsPlacementData[parName2]);
            }

            if (voidCache.LintelCache.SectionType == CrossSectionType.Corner)
            {
                lintelType.GetParameters("Пластины").First().Set(1);
                lintelType.GetParameters("Пластины_Длина").First().Set(voidCache.LintelCache.CornerElementsData.PlateL);
                lintelType.GetParameters("Пластины_Ширина").First().Set(voidCache.LintelCache.CornerElementsData.PlateB);
                lintelType.GetParameters("Пластины_Толщина").First().Set(voidCache.LintelCache.CornerElementsData.PlateT);
                lintelType.GetParameters("Пластины_Привязка").First().Set(voidCache.LintelCache.CornerElementsData.PlatesAlign);
                lintelType.GetParameters("Пластины_Шаг").First().Set(voidCache.LintelCache.CornerElementsData.PlatesStep);
                lintelType.GetParameters("Пластины_Количество").First().Set(voidCache.LintelCache.CornerElementsData.PlatesN);
            }

            doc.Regenerate();

            if (lintelType.get_Parameter(new Guid(p_Mark_Construction)) != null) lintelType.get_Parameter(new Guid(p_Mark_Construction)).Set(lintelTypeName);
            if (lintelType.get_Parameter(new Guid(p_Identity_Name)) != null) lintelType.get_Parameter(new Guid(p_Identity_Name)).Set("Перемычка");
            if (lintelType.get_Parameter(new Guid(p_Identity_SheetReference)) != null) lintelType.get_Parameter(new Guid(p_Identity_SheetReference)).Set("");




            doc.Regenerate();

            return lintelType;
        }
        public static List<LintelElementData> GetAvailableBarData(Document document)
        {
            doc = document;
            List<LintelElementData> availabeBarData = new List<LintelElementData>();
            if (families == null) GetFamiliesAndDefaultFamilyTypes();
            List<FamilySymbol> barSyms = (from id in families[1].GetFamilySymbolIds()
                                          select doc.GetElement(id) as FamilySymbol).ToList();

            foreach (FamilySymbol barSym in barSyms)
            {
                int key = (int)Math.Round(barSym.GetParameters("Высота").First().AsDouble() * 304.8);

                if (barSym.GetParameters("Материал перемычек") != null && barSym.GetParameters("Материал перемычек").Count() > 0)
                {
                    Parameter par = barSym.GetParameters("Материал перемычек").First();
                    if (par.AsElementId() != ElementId.InvalidElementId)
                    {
                        string materialName = doc.GetElement(par.AsElementId()).Name;
                        if (materialName != null && materialName.Contains("Керамзитобетон"))
                            key = (int)(key + Math.Round(barSym.GetParameters("Ширина").First().AsDouble() * 304.8) * Math.Pow(10, 3));
                    }
                }

                LintelElementData barData = new LintelElementData()
                {
                    TypeName = barSym.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString(),
                    L = barSym.GetParameters("Длина").First().AsDouble(),
                    T = barSym.GetParameters("Ширина").First().AsDouble(),
                    H = barSym.GetParameters("Высота").First().AsDouble(),
                    LsupportMin = (int)SerialElementsData.LintelSectionTypes[key][1] / 304.8
                };
                availabeBarData.Add(barData);
            }
            return availabeBarData;
        }

        private static void GetFamiliesAndDefaultFamilyTypes()
        {
            families = new List<Family>();
            List<int> familyIntIds = new List<int>
            {
                ModelSettings.Default.lintelFamily_IntId,
                ModelSettings.Default.lintelElementBarFamily_IntId,
                ModelSettings.Default.lintelElementRebarFamily_IntId,
                ModelSettings.Default.lintelElementCornerFamily_IntId
            };
            foreach (int familyIntId in familyIntIds)
            {
                if (ModelSettings.Default.lintelFamily_IntId != -1)
#pragma warning disable CS0618 // Тип или член устарел
                    families.Add(doc.GetElement(new ElementId(familyIntId)) as Family);
#pragma warning restore CS0618 // Тип или член устарел
                else
                    families.Add(null);
            }
            ;
            defaultlintelSyms = new List<FamilySymbol>();
            foreach (Family family in families)
            {
                ElementId id = family.GetFamilySymbolIds().FirstOrDefault();
                if (id != null) defaultlintelSyms.Add(doc.GetElement(id) as FamilySymbol);
                else defaultlintelSyms.Add(null);
            }
        }
        private static void FillParametersForCreatedLintelElementType(FamilySymbol linetelElementType)
        {
            linetelElementType.Activate();

            if (linetelElementType.get_Parameter(new Guid(p_Identity_Name)) != null)
                linetelElementType.get_Parameter(new Guid(p_Identity_Name)).Set(lintelElementData.ScheduleName);

            if (lintelElementData.Section == CrossSectionType.Bar
                & (lintelElementData.TypeName.Contains("L=") || lintelElementData.TypeName.Contains("ПБк")))
            {
                if (linetelElementType.GetParameters("Высота_Ручной ввод").Count > 0)
                    linetelElementType.GetParameters("Высота_Ручной ввод").First().Set(lintelElementData.H);
                if (linetelElementType.GetParameters("Длина_Ручной ввод").Count > 0)
                    linetelElementType.GetParameters("Длина_Ручной ввод").First().Set(lintelElementData.L);
                if (linetelElementType.GetParameters("Ширина_Ручной ввод").Count > 0)
                    linetelElementType.GetParameters("Ширина_Ручной ввод").First().Set(lintelElementData.T);
            }

            if (lintelElementData.Section == CrossSectionType.Corner
                || lintelElementData.Section == CrossSectionType.CornerMirrored)
            {
                if (linetelElementType.GetParameters("hw").Count > 0)
                    linetelElementType.GetParameters("hw").First().Set(lintelElementData.H);
                if (linetelElementType.GetParameters("bf").Count > 0)
                    linetelElementType.GetParameters("bf").First().Set(lintelElementData.H);
                if (linetelElementType.GetParameters("t").Count > 0)
                    linetelElementType.GetParameters("t").First().Set(lintelElementData.T);
                if (linetelElementType.GetParameters("Длина").Count > 0)
                    linetelElementType.GetParameters("Длина").First().Set(lintelElementData.L);
            }

            if (lintelElementData.Section == CrossSectionType.CornerMirrored)
                if (linetelElementType.GetParameters("Зеркально").Count > 0)
                    linetelElementType.GetParameters("Зеркально").First().Set(1);

            if (lintelElementData.Section == CrossSectionType.Rebar)
            {
                if (linetelElementType.GetParameters("Длина").Count > 0)
                    linetelElementType.GetParameters("Длина").First().Set(lintelElementData.L);
                if (linetelElementType.get_Parameter(new Guid(p_Dims_Diameter)) != null)
                    linetelElementType.get_Parameter(new Guid(p_Dims_Diameter)).Set(lintelElementData.T);
            }
        }
        private static string GetNewLintelTypeName(CrossSectionType lintelSectionType)
        {
            string name = "ПР-";
            switch (lintelSectionType)
            {
                case CrossSectionType.Bar:
                    if (lintelElementData.MaterialName != null && lintelElementData.MaterialName.Contains("Керамзитобетон")) name = "ПРк-";
                    else name = "ПРб-";
                    break;
                case CrossSectionType.Rebar: name = "ПРа-"; break;
                case CrossSectionType.Corner: name = "ПРу-"; break;
            }
            List<double> lintelTypeOrderNs = (from id in families[0].GetFamilySymbolIds()
                                              where (doc.GetElement(id) as FamilySymbol).get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString().StartsWith(name)
                                              && double.TryParse((doc.GetElement(id) as FamilySymbol).get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString().Split('-').Last(), out _)
                                              select double.Parse((doc.GetElement(id) as FamilySymbol).get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString().Split('-').Last())).ToList();
            if (lintelTypeOrderNs.Count > 0)
            {
                int maxOrderN = (int)lintelTypeOrderNs.Max();
                name += (maxOrderN + 1).ToString();
            }
            else name += "1";
            return name;
        }

        private static void AssociateMatarialParWithGlobalPar(FamilySymbol linetelElementType)
        {
            ElementId globalMaterialParId1 = (from id in GlobalParametersManager.GetAllGlobalParameters(doc)
                                              where (doc.GetElement(id) as GlobalParameter).Name == "Материал_Перемычки_Брусковые_Тип 1"
                                              select id).FirstOrDefault();
            ElementId globalMaterialParId2 = (from id in GlobalParametersManager.GetAllGlobalParameters(doc)
                                              where (doc.GetElement(id) as GlobalParameter).Name == "Материал_Перемычки_Брусковые_Тип 2"
                                              select id).FirstOrDefault();
            List<Parameter> materialParameters = linetelElementType.GetParameters("Материал перемычек").ToList();

            if (materialParameters.Count > 0 && materialParameters.First().GetAssociatedGlobalParameter() == ElementId.InvalidElementId)
            {
                string typeName = linetelElementType.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM).AsString();
                if (typeName.Contains("ПБк")) materialParameters.First().AssociateWithGlobalParameter(globalMaterialParId1);
                else linetelElementType.GetParameters("Материал перемычек").First().AssociateWithGlobalParameter(globalMaterialParId2);
            }
        }
    }
}
