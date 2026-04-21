using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Resources1P;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class AdaptUtils
    {
        public enum Response
        {
            OK,
            FailedAdaptFamilySharedParameters,
            FailedReshareFamilyParameters
        }

        public struct LinePatternData
        {
            public LinePattern LP;
            public double Segment1Length;
            public double Segment2Length;
        }



        public static void ReplaceRefPlaneLinePatternElementByDash4(Document doc, out ElementId rpCatLPEId)
        {
            rpCatLPEId = Category.GetCategory(doc, BuiltInCategory.OST_CLines).GetLinePatternId(GraphicsStyleType.Projection);
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(LinePatternElement));
            foreach (LinePatternElement lpe in collector) lpe.Name += "_temp";
            foreach (LinePatternElement lpe in collector)
            {
                if (lpe.Id == rpCatLPEId)
                {
                    LinePattern lp = lpe.GetLinePattern();
                    List<LinePatternSegment> lpSegments = lp.GetSegments().ToList();
#if COMPANY_FP
                    lpSegments.First().Length = 3.5 / 304.8;
#else
                    lpSegments.First().Length = 3 / 304.8;
#endif
                    lpSegments.Last().Length = 2 / 304.8;
                    lp.SetSegments(lpSegments);
                    lpe.SetLinePattern(lp);
#if COMPANY_FP
                    lpe.Name = LinePatterns.dash4_Name;
#else
                    lpe.Name = LinePatterns.dash3_Name;
#endif
                }
            }
        }

        /*public static void AdaptRebarShapeFamily(Document doc)
        {
            DefinitionFile spf = doc.Application.OpenSharedParameterFile();
            if (doc.IsFamilyDocument && spf.Filename == "P:\\Общие параметры\\1П_Общие параметры.txt")
            {
                List<Definition> defs = new List<Definition>();
                foreach (DefinitionGroup gr in spf.Groups) defs.AddRange(gr.Definitions);
                List<string> existDimParameterNames = (from dim in new FilteredElementCollector(doc).OfClass(typeof(Dimension))
                                              where (doc.GetElement(dim.get_Parameter(BuiltInParameter.DIM_LABEL).AsElementId()) as SharedParameterElement).Name.Contains("def")
                                              select (doc.GetElement(dim.get_Parameter(BuiltInParameter.DIM_LABEL).AsElementId()) as SharedParameterElement).Name).ToList();
                List<string> reqDimSPE =  
            }
        }*/

        //ВНИМАНИЕ! Данный метод использует собственные транзации!
        public static void UnshareFamilySharedParameters(FamilyDocumentCache fDocCache)
        {
            Document doc = fDocCache.Doc;
            using (Transaction tx = new Transaction(doc))
            {
                FailureHandlingOptions failureHandlingOptions = tx.GetFailureHandlingOptions();
                failureHandlingOptions.SetForcedModalHandling(false);
                tx.SetFailureHandlingOptions(failureHandlingOptions);

                tx.Start("Замена общих параметров параметрами семейства в документе: " + doc.Title);
                for (int i = 0; i < fDocCache.SharedFamilyParameterDatas.Count; i++)
                {
                    FamilyParameterData data = fDocCache.SharedFamilyParameterDatas[i];

                    data.Parameter = doc.FamilyManager.ReplaceParameter(data.Parameter, data.SourceName + "_temp", data.GroupTypeId, data.IsInstance);
                    doc.FamilyManager.RenameParameter(data.Parameter, data.SourceName);

                }

                List<ElementId> allSPEIds = new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement)).ToElementIds().ToList();

                doc.Delete(allSPEIds);
                tx.Commit();
            }
        }

        public static Response ReshareFamilyParameters(FamilyDocumentCache fDocCache)
        {
            DefinitionFile spf = fDocCache.Doc.Application.OpenSharedParameterFile();
            if (spf.Filename == "P:\\Общие параметры\\1П_Общие параметры.txt")
            {
                Dictionary<string, Definition> definitionsByGuids = GetDefinitionsByGuids(spf);
                Document doc = fDocCache.Doc;
                fDocCache.ResharedFamilyParameterNames = new Dictionary<string, string>();
                for (int i = 0; i < fDocCache.SharedFamilyParameterDatas.Count; i++)
                {
                    FamilyParameterData data = fDocCache.SharedFamilyParameterDatas[i];
                    if (definitionsByGuids.Keys.Contains(data.GuidAsString))
                    {
                        ExternalDefinition extDef = definitionsByGuids[data.GuidAsString] as ExternalDefinition;
                        data.NewName = extDef.Name;
#if REVIT2024 || REVIT2025
                        data.Parameter = doc.FamilyManager.ReplaceParameter(data.Parameter, extDef, data.GroupTypeId, data.IsInstance);
                        fDocCache.ResharedFamilyParameterNames.Add(data.SourceName, data.NewName);
#endif
                    }
                }
                return Response.OK;
            }
            return Response.FailedReshareFamilyParameters;
        }

        public static Response AdaptFamilySharedParameters(Document doc, out Dictionary<string, string> renamedPars, out List<string> unsharedPars)
        {
            renamedPars = new Dictionary<string, string>();
            unsharedPars = new List<string>();
            DefinitionFile spf = doc.Application.OpenSharedParameterFile();
            if (doc.IsFamilyDocument && spf != null && spf.Filename == "P:\\Общие параметры\\1П_Общие параметры.txt")
            {
                List<Definition> defs = new List<Definition>();
                foreach (DefinitionGroup gr in spf.Groups) defs.AddRange(gr.Definitions);
                Dictionary<string, Definition> definitionsByGuids = new Dictionary<string, Definition>();
                foreach (Definition def in defs) definitionsByGuids.Add((def as ExternalDefinition).GUID.ToString(), def);

                FamilyManager fm = doc.FamilyManager;
                foreach (FamilyParameter par in fm.Parameters)
                {
                    if (par.IsShared && !(par.Definition.Name.Split('_').First() == "1П"))
                    {
                        string oldName = par.Definition.Name;
                        string guid = par.GUID.ToString();

#if REVIT2024 || REVIT2025
                        ForgeTypeId groupTypeId = par.Definition.GetGroupTypeId();
#else
                        BuiltInParameterGroup groupTypeId = par.Definition.ParameterGroup;
#endif
                        bool isInst = par.IsInstance;

                        FamilyParameter renamingPar = fm.ReplaceParameter(par, oldName + "_temp", groupTypeId, isInst);
                        ElementId speId = (from elem in new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement))
                                           where (elem as SharedParameterElement).GuidValue.ToString() == guid
                                           select elem.Id).FirstOrDefault();
                        doc.Delete(speId);
                        if (definitionsByGuids.Keys.Contains(guid))
                        {
                            ExternalDefinition extDef = definitionsByGuids[guid] as ExternalDefinition;
                            string newName = extDef.Name;
                            fm.ReplaceParameter(renamingPar, extDef, groupTypeId, isInst);
                            renamedPars.Add(oldName, newName);
                        }
                        else
                        {
                            fm.RenameParameter(renamingPar, oldName);
                            unsharedPars.Add(oldName);
                        }
                    }
                }
                return Response.OK;
            }
            else return Response.FailedAdaptFamilySharedParameters;
        }

        public static void AddFamilyStandardParameters(FamilyDocumentCache fDocCache)
        {
            Document doc = fDocCache.Doc;
            FamilyStandardParameters.InitializeForShared(doc);
            List<FamilyParameterData> familyParameterDatas = new List<FamilyParameterData>();
            FamilyManager fm = doc.FamilyManager;

            switch (fDocCache.StructureClassification)
            {
                case FamilyStructureClassification.RebarShape: break;
                case FamilyStructureClassification.SteelDetail_Child:
#if COMPANY_OLP
                    /*familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_RowHeight, false));

                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_SheetReference, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.OLP_Identity_SheetReferenceКМ, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Name, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.OLP_Identity_NameKM, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Identity_NameStructureProfile, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Identity_Name_Text1, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Identity_Name_Prefix, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.OLP_Identity_SheetReferenceItem, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.OLP_Identity_NameItem, true));

                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Material_Ref, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Material, true));

                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Dims_Length, true));

                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Identity_SheetSet, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Level, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.OLP_Identity_BuildingSection, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Building, true));

                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Dims_InRunningMeters, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Data_ElemTypeKM, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Data_StructureGrouping, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Data_MassCountType, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_MassPerLength, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.OLP_MassPerSquare, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mass, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Identity_MainItemElement, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_MainElement, true));

                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Mark_ElemTakeoff, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Construction, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Item, true));*/
#else
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Complicate_Length1, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Complicate_Length2, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Complicate_Length3, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Complicate_Text1, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Complicate_Text2, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Zone, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Section, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Amount, false));
#endif

                    List<FamilyParameterData> standardFPDs = new List<FamilyParameterData>
                    {
                        FamilyStandardParameters.MassDensity,
                    };
                    for (int i = 0; i < standardFPDs.Count; i++)
                    {
                        FamilyParameterData fpd = standardFPDs[i];
                        fpd.IsInstance = false;
                        familyParameterDatas.Add(fpd);
                    }
                    break;
                case FamilyStructureClassification.SteelUnit:

#if COMPANY_FP
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Zone, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Section, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Amount, false));
#else
                    //Общие параметры
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Dims_InRunningMeters, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mass, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Construction, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Item, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Name, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_SheetReference, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Data_Grouping, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Data_StructureGrouping, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.ADSK_Identity_SheetSet, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_MainElement, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Level, true));

#endif
                    //Параметры семейства
                    standardFPDs = new List<FamilyParameterData>
                    {
                        FamilyStandardParameters.MassDensity,
                        FamilyStandardParameters.SubComponentVisibility,
                        FamilyStandardParameters.SubComponentMaterial
                    };

                    //Определение параметра как параметр экземлпяра или как параметр типа
                    for (int i = 0; i < standardFPDs.Count; i++)
                    {
                        FamilyParameterData fpd = standardFPDs[i];
                        if (i == 0) fpd.IsInstance = false;
                        else fpd.IsInstance = true;
                        familyParameterDatas.Add(fpd);
                    }
                    break;

                case FamilyStructureClassification.Fabric:
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Zone, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Section, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Amount, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mass, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Construction, true));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Mark_Item, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_Name, false));
                    familyParameterDatas.Add(FamilyStandardParameters.GetFPDOfGuid(RevitParameters.p_Identity_SheetReference, false));
                    standardFPDs = new List<FamilyParameterData>
                    {
                        FamilyStandardParameters.Length_Nominal,
                        FamilyStandardParameters.Length_Model,
                        FamilyStandardParameters.Width_Nominal,
                        FamilyStandardParameters.Width_Model,
                        FamilyStandardParameters.Height,
                        FamilyStandardParameters.Material
                    };
                    for (int i = 0; i < standardFPDs.Count; i++)
                    {
                        FamilyParameterData fpd = standardFPDs[i];
                        fpd.IsInstance = false;
                        familyParameterDatas.Add(fpd);
                    }
                    break;
                case FamilyStructureClassification.ConcreteInsert: break;
                case FamilyStructureClassification.Void: break;
                case FamilyStructureClassification.Unknow: break;
            }
            foreach (FamilyParameterData fpd in familyParameterDatas)
            {
                if (fpd.ExternalDefinition != null)
                    try { fm.AddParameter(fpd.ExternalDefinition, fpd.GroupTypeId, fpd.IsInstance); }
                    catch { }
                else
                    try { fm.AddParameter(fpd.SourceName, fpd.GroupTypeId, fpd.SpecTypeId, fpd.IsInstance); }
                    catch { }
            }
        }

        public static void AdaptProjectSharedParameters(Document doc)
        {
            DefinitionFile spf = doc.Application.OpenSharedParameterFile();
            if (!doc.IsFamilyDocument && spf.Filename == "P:\\Общие параметры\\1П_Общие параметры.txt")
            {

                List<Guid> existGUIDs = (from spe in new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement))
                                         select (spe as SharedParameterElement).GuidValue).ToList();


                List<Definition> defs = new List<Definition>();
                foreach (DefinitionGroup gr in spf.Groups) defs.AddRange(gr.Definitions);
                foreach (Definition def in defs)
                {
                    if (!existGUIDs.Contains((def as ExternalDefinition).GUID))
                        SharedParameterElement.Create(doc, def as ExternalDefinition);
                }



            }
        }


        public static Dictionary<string, Definition> GetDefinitionsByGuids(DefinitionFile spf)
        {
            List<Definition> defs = new List<Definition>();
            foreach (DefinitionGroup gr in spf.Groups) defs.AddRange(gr.Definitions);
            Dictionary<string, Definition> definitionsByGuids = new Dictionary<string, Definition>();
            foreach (Definition def in defs)
            {
                string guid = (def as ExternalDefinition).GUID.ToString();
                if (!definitionsByGuids.Keys.Contains(guid)) definitionsByGuids.Add(guid, def);
            }

            return definitionsByGuids;
        }
    }
}
