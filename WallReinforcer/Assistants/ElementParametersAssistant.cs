using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using Parameter = Autodesk.Revit.DB.Parameter;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;
using RevitOSA.WallReinforcer.Tools;
using RevitOSA.WallReinforcer.Revit.Filters;
using RevitOSA.WallReinforcer.Resources;

namespace RevitOSA.WallReinforcer.Assistants
{
    public static class ElementParametersAssistant
    {
        private static Document doc;
        private static Element mainHost;
        private static List<Element> subElems;
        private static Dictionary<object, object> inheritParameters;
        private static Dictionary<object, object> otherParameters;
        private static Dictionary<ElementId, int> levelDict;

        public static List<ElementId> SelectedElemIds;

        public static void InheritValuesFromHost(Element host, Element elem, bool CheckInheritParametersDict)
        {
            mainHost = host;
            if (CheckInheritParametersDict) FillInheritParametersDict();
            doc = host.Document;
            foreach (KeyValuePair<object, object> entity in inheritParameters)
            {
                Parameter par;
                switch (entity.Key)
                {
                    case BuiltInParameter _:
                        par = elem.get_Parameter((BuiltInParameter)entity.Key);
                        break;
                    case string _:
                        {
                            if (Guid.TryParse(entity.Key as string, out Guid guid)) par = elem.get_Parameter(guid);
                            else par = elem.GetParameters(entity.Key as string).First();
                            break;
                        }

                    default:
                        par = null;
                        break;
                }
                if (par != null && !par.IsReadOnly)
                {
                    switch (par.StorageType)
                    {
                        case StorageType.Integer: par.Set((int)entity.Value); break;
                        case StorageType.Double: par.Set((double)entity.Value); break;
                        case StorageType.String: par.Set((string)entity.Value); break;
                        case StorageType.ElementId:
                        {
                            if (entity.Value.GetType() == typeof(string)) par.Set((string)entity.Value);
                            else par.Set((ElementId)entity.Value); 
                            break;
                        }
                    }
                }
            }
            if (Properties.ElementParametersAssistant.Default.bip_Identity_Workset_AllowToSetValue)
            {
                SetDefaultWorkset(elem);
            }
        }

        public static void InheritValuesForSubsFromHost(Element host)
        {
            mainHost = host;
            FillInheritParametersDict();
            doc = host.Document;
            GetSubElems();
            foreach (Element elem in subElems) InheritValuesFromHost(host, elem, false);
        }

        public static void InheritValuesForSubsFromGroup(Group gr)
        {
            mainHost = gr;
            FillInheritParametersDict();
            doc = gr.Document;
            subElems = (from id in gr.GetMemberIds()
                        select doc.GetElement(id)).ToList();
            foreach (Element elem in subElems) InheritValuesFromHost(gr, elem, false);
        }

        public static void AssignValuesFromLevel(Element elem)
        {
            mainHost = elem;
            doc = elem.Document;
            FillOtherParametersDict();
            if (levelDict == null) FillLevelDict(doc);

            if (mainHost.LevelId != null)
            {
                Level level = doc.GetElement(mainHost.LevelId) as Level;

                if (otherParameters.ContainsKey(pp_Number_OfLevel))
                {
                    int nLvl = 0;
                    if (levelDict.Keys.Contains(mainHost.LevelId)) nLvl = levelDict[mainHost.LevelId];
                    else if (otherParameters[pp_Number_OfLevel] != null) int.TryParse(otherParameters[pp_Number_OfLevel].ToString(), out nLvl);
                    
                    if (nLvl != 0)
                    {
                        if (elem.GetParameters(pp_Number_OfLevel).Count() > 0)
                            elem.GetParameters(pp_Number_OfLevel).First().Set(nLvl);
                        GetSubElems();
                        foreach (Element subElem in subElems)
                            if (!SelectedElemIds.Contains(subElem.Id) && subElem.GetParameters(pp_Number_OfLevel).Count() > 0)
                                subElem.GetParameters(pp_Number_OfLevel).First().Set(nLvl);
                    }
                }

                if (otherParameters.ContainsKey(p_Dims_Elevation) && elem.get_Parameter(Guid.Parse(p_Dims_Elevation)) != null)
                {
                    Parameter par1 = elem.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                    if (par1 != null)
                    {
                        double elev = LevelElevationMM(level)/304.8 + par1.AsDouble();
                        elem.get_Parameter(Guid.Parse(p_Dims_Elevation)).Set(elev);
                    }
                }
            }
        }

        public static List<object> GetParameterKeyValuePairAsList(Element elem, object parameterIdent)
        {
            Parameter par = null;
            switch (parameterIdent)
            {
                case BuiltInParameter _:
                    par = elem.get_Parameter((BuiltInParameter)parameterIdent);
                    break;
                case string _:
                    {
                        try
                        {
                            Guid guid = Guid.Parse((string)parameterIdent);
                            par = elem.get_Parameter(guid);
                        }
                        catch
                        {
                            List<Parameter> pars = elem.GetParameters((string)parameterIdent).ToList();
                            if (pars.Count > 0) par = pars.First();
                        }

                        break;
                    }
            }
            if (par != null) return new List<object> { parameterIdent, GetParameterValue(par, elem) };
            else return new List<object> { null, null };
        }

        public static List<object> GetParameterKeyValuePairAsList(Element elem, object parameterIdent, Parameter par2)
        {
            Parameter par = null;
            switch (parameterIdent)
            {
                case BuiltInParameter _:
                    par = elem.get_Parameter((BuiltInParameter)parameterIdent);
                    break;
                case string _:
                    {
                        try
                        {
                            Guid guid = Guid.Parse((string)parameterIdent);
                            par = elem.get_Parameter(guid);
                        }
                        catch
                        {
                            List<Parameter> pars = elem.GetParameters((string)parameterIdent).ToList();
                            if (pars.Count > 0) par = pars.First();
                        }

                        break;
                    }
            }
            if (par != null) return new List<object> { parameterIdent, GetParameterValue(par2, elem) };
            else return new List<object> { null, null };
        }

        public static object GetParameterValue(Parameter par, Element sourceElem)
        {
            object value = null;
            switch (par.StorageType)
            {
                case StorageType.Integer: value = par.AsInteger(); break;
                case StorageType.Double: value = Math.Round(par.AsDouble() * 304.8); break;
                case StorageType.String: value = par.AsString(); break;
                case StorageType.ElementId:
                    {
                        doc = sourceElem.Document;
                        Element valueElem = doc.GetElement(par.AsElementId());
                        if (valueElem is ImageType) value = (valueElem as ImageType).Name;
                        else value = par.AsElementId();
                        break;
                    }
            }
            return value;
        }

        public static string GetElementMark(Element elem)
        {
            if (doc == null) doc = elem.Document;

            List<Parameter> pars = new List<Parameter>
            {
                elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK),
                elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK),
                elem.get_Parameter(new Guid(p_Mark_Item)),
                elem.get_Parameter(new Guid(p_Mark_Construction))
            };
            if (elem is FamilyInstance)
            {
                FamilySymbol sym = (elem as FamilyInstance).Symbol;
                pars.Add(sym.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK));
                pars.Add(sym.get_Parameter(new Guid(p_Mark_Item)));
                pars.Add(sym.get_Parameter(new Guid(p_Mark_Construction)));
            }
            else if (elem is AssemblyInstance)
            {
                AssemblyType at = doc.GetElement((elem as AssemblyInstance).GetTypeId()) as AssemblyType;
                pars.Add(at.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK));
                pars.Add(at.get_Parameter(new Guid(p_Mark_Item)));
                pars.Add(at.get_Parameter(new Guid(p_Mark_Construction)));
            }

                string mark = (from par in pars
                           where par != null && !string.IsNullOrEmpty((string)GetParameterValue(par, elem))
                           select (string)GetParameterValue(par, elem)).FirstOrDefault();
            return mark;
        }

        public static void FillLevelDict(Document doc)
        {
            List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements().Cast<Level>().ToList();
            levels = levels.OrderBy(LevelElevationMM).ToList();
            List<Level> levelsSub0 = (from level in levels
                                         where LevelElevationMM(level) < -500
                                      select level).ToList();
            int nLvlSub0 = -1*levelsSub0.Count;
            int nLvl = 1;
            double currentElev = LevelElevationMM(levels.First());

            levelDict = new Dictionary<ElementId, int>();
            
            foreach (Level level in levels)
            {
                if (levelsSub0.Contains(level))
                {
                    levelDict.Add(level.Id, nLvlSub0);
                    if (level != levels.First() && Math.Abs(LevelElevationMM(level) - currentElev) > 500)
                    {
                        nLvlSub0 += 1;
                        currentElev = LevelElevationMM(level);
                    }
                }
                else
                {
                    levelDict.Add(level.Id, nLvl);
                    if (level != levels.First() && Math.Abs(LevelElevationMM(level) - currentElev) > 500)
                    {
                        nLvl += 1;
                        currentElev = LevelElevationMM(level);
                    } 
                }
                //if (int.TryParse(level.Name.Split(' ').Last(), out int nLvl)) levelDict.Add(level.Id, nLvl);
            }
        }

        public static bool IsParameterExistAndHasValue(Element elem, string parName)
        {
            if (Guid.TryParse(parName, out Guid guid))
            {
                Parameter par = elem.get_Parameter(guid);
                if (par != null && par.HasValue) return true;
                else return false;
            }
            else
            {
                List<Parameter> pars = elem.GetParameters(parName).ToList();
                if (pars != null && pars.Count > 0 && pars.First().HasValue) return true;
                else return false;
            }
        }

        public static Parameter GetParameterFromElement(Element elem, ElementId parId)
        {
            Document doc = elem.Document;
            Parameter par;
#if REVIT2024||REVIT2025

            par = elem.get_Parameter((BuiltInParameter)parId.Value);
            if (par == null) par = doc.GetElement(elem.GetTypeId()).get_Parameter((BuiltInParameter)parId.Value);

#else
            par = elem.get_Parameter((BuiltInParameter)parId.IntegerValue);
            if (par == null) par = doc.GetElement(elem.GetTypeId()).get_Parameter((BuiltInParameter)parId.IntegerValue);
#endif
            if (par == null)
            {
                List<Material> mats = ExtractingTools.GetMaterials(elem);
                foreach (Material mat in mats)
                {
#if REVIT2024||REVIT2025
                    par = mat.get_Parameter((BuiltInParameter)parId.Value);           
#else
                    par = mat.get_Parameter((BuiltInParameter)parId.IntegerValue);
#endif
                    if (par != null) break;
                } 
            }
            return par;
        }

        private static void FillInheritParametersDict()
        {
            inheritParameters = new Dictionary<object, object>();
            //Стадия
            if (Properties.ElementParametersAssistant.Default.bip_Phases_PhaseCreated_AllowToSetValue
                && !Properties.ElementParametersAssistant.Default.bip_Identity_Workset_EnableDefaultValue)
            {
                List<object> keyValuePair = GetParameterKeyValuePairAsList(mainHost, BuiltInParameter.PHASE_CREATED);
                inheritParameters.Add(keyValuePair.First(), keyValuePair.Last());
            }       
            //Блок
            if (Properties.ElementParametersAssistant.Default.p_Identity_Section_AllowToSetValue
                && !Properties.ElementParametersAssistant.Default.p_Identity_Section_EnableDefaultValue
                && IsParameterExistAndHasValue(mainHost, p_Identity_Section))
            {
                List<object> keyValuePair = GetParameterKeyValuePairAsList(mainHost, p_Identity_Section);
                inheritParameters.Add(keyValuePair.First(), keyValuePair.Last());
            }
            //Зона
            if (Properties.ElementParametersAssistant.Default.p_Identity_Zone_AllowToSetValue
               && !Properties.ElementParametersAssistant.Default.p_Identity_Zone_EnableDefaultValue
               && IsParameterExistAndHasValue(mainHost, p_Identity_Zone))
            {
                List<object> keyValuePair = GetParameterKeyValuePairAsList(mainHost, p_Identity_Zone);
                inheritParameters.Add(keyValuePair.First(), keyValuePair.Last());
            }
            //Марка конструкции
            if (Properties.ElementParametersAssistant.Default.p_Mark_Construction_AllowToSetValue
                && !Properties.ElementParametersAssistant.Default.p_Mark_Construction_EnableDefaultValue)
            {
                List<object> keyValuePair = GetParameterKeyValuePairAsList(mainHost, p_Mark_Construction, mainHost.get_Parameter(BuiltInParameter.ALL_MODEL_MARK));
                inheritParameters.Add(keyValuePair.First(), keyValuePair.Last());
            }
            //Эталон
            if (Properties.ElementParametersAssistant.Default.pp_StandartItem_AllowToSetValue
               && !Properties.ElementParametersAssistant.Default.pp_StandartItem_EnableDefaultValue
               && IsParameterExistAndHasValue(mainHost, pp_StandartItem))
            {
                List<object> keyValuePair = GetParameterKeyValuePairAsList(mainHost, pp_StandartItem);
                inheritParameters.Add(keyValuePair.First(), keyValuePair.Last());
            }
            //Этаж
            if (Properties.ElementParametersAssistant.Default.p_Identity_Level_AllowToSetValue
                && !Properties.ElementParametersAssistant.Default.p_Identity_Level_EnableDefaultValue
                && IsParameterExistAndHasValue(mainHost, p_Identity_Level))
            {
                List<object> keyValuePair = GetParameterKeyValuePairAsList(mainHost, p_Identity_Level);
                inheritParameters.Add(keyValuePair.First(), keyValuePair.Last());
            }
        }

        private static void FillOtherParametersDict()
        {
            otherParameters = new Dictionary<object, object>();
            //Рабочий набор
            if (Properties.ElementParametersAssistant.Default.bip_Identity_Workset_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.bip_Identity_Workset_EnableDefaultValue)
                    otherParameters.Add(BuiltInParameter.ELEM_PARTITION_PARAM, Properties.ElementParametersAssistant.Default.bip_Identity_Workset_DefaultValue);
            //Стадия
            if (Properties.ElementParametersAssistant.Default.bip_Phases_PhaseCreated_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.bip_Identity_Workset_EnableDefaultValue)
                    otherParameters.Add(BuiltInParameter.PHASE_CREATED, Properties.ElementParametersAssistant.Default.bip_Phases_PhaseCreated_DefaultValue);
            //Блок
            if (Properties.ElementParametersAssistant.Default.p_Identity_Section_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.p_Identity_Section_EnableDefaultValue)
                    otherParameters.Add(Guid.Parse(p_Identity_Section), Properties.ElementParametersAssistant.Default.p_Identity_Section_DefaultValue);
            //Зона
            if (Properties.ElementParametersAssistant.Default.p_Identity_Zone_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.p_Identity_Zone_EnableDefaultValue)
                    otherParameters.Add(Guid.Parse(p_Identity_Zone), Properties.ElementParametersAssistant.Default.p_Identity_Zone_DefaultValue);
            //Марка конструкции
            if (Properties.ElementParametersAssistant.Default.p_Mark_Construction_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.p_Mark_Construction_EnableDefaultValue)
                    otherParameters.Add(Guid.Parse(p_Mark_Construction), Properties.ElementParametersAssistant.Default.p_Mark_Construction_DefaultValue);
            //Наименование
            if (Properties.ElementParametersAssistant.Default.p_Identity_Name_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.p_Identity_Name_EnableDefaultValue)
                    otherParameters.Add(Guid.Parse(p_Identity_Name), Properties.ElementParametersAssistant.Default.p_Identity_Name_DefaultValue);
            //Главная деталь
            if (Properties.ElementParametersAssistant.Default.p_Identity_MainElement_AllowToSetValue)
            {
                if (Properties.ElementParametersAssistant.Default.p_Identity_MainElement_EnableDefaultValue)
                    otherParameters.Add(Guid.Parse(p_Identity_MainElement), Properties.ElementParametersAssistant.Default.p_Identity_MainElement_DefaultValue);
                else
                    otherParameters.Add(Guid.Parse(p_Identity_MainElement), false);
            }
            //Эталон
            if (Properties.ElementParametersAssistant.Default.pp_StandartItem_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.pp_StandartItem_EnableDefaultValue)
                    otherParameters.Add(pp_StandartItem, Properties.ElementParametersAssistant.Default.pp_StandartItem_DefaultValue);
            //Этаж
            if (Properties.ElementParametersAssistant.Default.p_Identity_Level_AllowToSetValue
                && Properties.ElementParametersAssistant.Default.p_Identity_Level_EnableDefaultValue)
                    otherParameters.Add(Guid.Parse(p_Identity_Level), Properties.ElementParametersAssistant.Default.p_Identity_Level_DefaultValue);
            //Номер этажа
            if (Properties.ElementParametersAssistant.Default.pp_Number_OfLevel_AllowToSetValue)
            {
                object defaultValue = null;
                if (Properties.ElementParametersAssistant.Default.pp_Number_OfLevel_EnableDefaultValue)
                    defaultValue = Properties.ElementParametersAssistant.Default.pp_Number_OfLevel_DefaultValue;
                otherParameters.Add(pp_Number_OfLevel, defaultValue);
            }
            //Размер_Отметка расположения
            if (Properties.ElementParametersAssistant.Default.p_Dims_Elevation_AllowToSetValue)
                otherParameters.Add(p_Dims_Elevation, null);
        }

        private static void GetSubElems()
        {
            subElems = new List<Element>();
            if (RebarHostData.IsValidHost(mainHost))
            {
                RebarHostData hostData = RebarHostData.GetRebarHostData(mainHost);
                subElems.AddRange(hostData.GetRebarsInHost());
                subElems.AddRange(hostData.GetRebarContainersInHost());
                List<AreaReinforcement> ars = hostData.GetAreaReinforcementsInHost().ToList();
                subElems.AddRange(ars);
                foreach (AreaReinforcement ar in ars)
                {
                    List<RebarInSystem> rebarsInSystem = (from id in ar.GetRebarInSystemIds()
                                                          select doc.GetElement(id) as RebarInSystem).ToList();
                    subElems.AddRange(rebarsInSystem);
                }
            }
            List<FamilyInstance> cis = new ExtractingTools.ConcreteInsertExtractor(doc, mainHost).ToElements();
            if (cis != null && cis.Count > 0) subElems.AddRange(cis);
            foreach (FamilyInstance ci in cis)
            {
                List<FamilyInstance> subs = (from id in ExtractingTools.ExtractFamilyInstanceSubComponents(doc, ci as FamilyInstance)
                                             select doc.GetElement(id) as FamilyInstance).ToList();
                if (subs != null && subs.Count > 0) subElems.AddRange(subs);
            }

            if (mainHost is FamilyInstance)
            {
                List<FamilyInstance> subs = (from id in ExtractingTools.ExtractFamilyInstanceSubComponents(doc, mainHost as FamilyInstance)
                                             select doc.GetElement(id) as FamilyInstance).ToList();
                if (subs != null && subs.Count > 0) subElems.AddRange(subs);
            }

            if (mainHost is Wall)
            {
                List<FamilyInstance> subs = (from id in (mainHost as Wall).FindInserts(false, false, false, false)
                                             where doc.GetElement(id) is FamilyInstance 
                                             && (doc.GetElement(id) as FamilyInstance).Host != null
                                             && (doc.GetElement(id) as FamilyInstance).Host.Id == mainHost.Id
                                             select doc.GetElement(id) as FamilyInstance).ToList();
                if (subs != null && subs.Count > 0) subElems.AddRange(subs);
            }

            if (mainHost is AssemblyInstance)
            {
                List<Element> subs = (from id in (mainHost as AssemblyInstance).GetMemberIds()
                                      select doc.GetElement(id)).ToList();
                if (subs != null && subs.Count > 0) subElems.AddRange(subs);
            }
        }

        private static int LevelElevationMM(Level level)
        {
            return (int)Math.Round(level.Elevation * 304.8);
        }

        private static void SetDefaultWorkset(Element elem)
        {
            string hostWorksetName = mainHost.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).AsValueString();
            string sheetSet = hostWorksetName.Split('_').First();
            if (sheetSet == "КЖ") 
            {
                string structureName = hostWorksetName.Split('_')[1];
                string suffix = null;
                
                if (RebarFilters.All.PassesFilter(elem))
                {
                    string partition = elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM).AsString();
                    switch (partition)
                    {
                        case "04_Выпуски": suffix = WorksetAssistant.StructureSuffixes[WorksetStructureSuffix.anchors]; break;
                        default: suffix = WorksetAssistant.StructureSuffixes[WorksetStructureSuffix.reinf]; break;
                    }
                }
                else if (ExtractingTools.ConcreteInsertExtractor.IsConcreteInsert(elem)) suffix = WorksetAssistant.StructureSuffixes[WorksetStructureSuffix.cis];
                else if (elem is AssemblyInstance) suffix = WorksetAssistant.StructureSuffixes[WorksetStructureSuffix.ais];
                else if (VoidFilters.Union.PassesFilter(elem)) suffix = WorksetAssistant.StructureSuffixes[WorksetStructureSuffix.concrete];


                if (!string.IsNullOrEmpty(suffix))
                {
                    string subWorksetName = sheetSet + "_" + structureName + "_" + suffix;
                    Workset defaultWorkset = (from workset in new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                                                where workset.Name == subWorksetName
                                                select workset).FirstOrDefault();
                    if (defaultWorkset != null)
                        elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(defaultWorkset.Id.IntegerValue);
                }              
            }
        }

    }
}
