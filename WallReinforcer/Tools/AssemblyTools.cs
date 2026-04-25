using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Resources1P;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

using DocSettings = RevitOSA.WallReinforcer.Properties.Docs;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class AssemblyTools
    {
        public static List<ElementId> ExistAssemblyTypeIds;
        public static List<string> ExistAssemblyTypeNames;
        private static Document doc;
        private static ResourceSet markDescriptions;
        private static List<int> userWorksetIntIds;
        private static readonly List<string> userWorkserSuffixes = new List<string> { "Сборки", "Бетон", "Армирование", "Закладные" };


        public static AssemblyInstance CreateAIFromFamilyInstanceComponents(FamilyInstance sourceInst)
        {
            doc = sourceInst.Document;
            List<ElementId> memberIds = ExtractingTools.ExtractFamilyInstanceSubComponents(doc, sourceInst);
            if (memberIds.Count > 0)
            {
                ElementId categoryId = doc.GetElement(memberIds[0]).Category.Id;
                AssemblyInstance ai = AssemblyInstance.Create(doc, memberIds, categoryId);
                return ai;
            }
            else return null;
        }
        public static AssemblyInstance CreateAIFromFamilyInstanceWithAttachedComponents(FamilyInstance sourceInst)
        {
            doc = sourceInst.Document;
            ElementId categoryId = sourceInst.Category.Id;
            AssemblyInstance ai = AssemblyInstance.Create(doc, new List<ElementId> { sourceInst.Id }, categoryId);
            if (ai != null)
            {
                Solid sourceSolid = SolidTools.GetSolid(sourceInst, false);

                if (sourceSolid != null)
                {
                    Transform sourceTransform = sourceInst.GetTransform();
                    List<Transform> scaleTransforms = new List<Transform> { sourceTransform.ScaleBasis(1.05), sourceTransform.ScaleBasis(0.95) };
                    List<ElementFilter> filters = new List<ElementFilter>();
                    foreach (Transform scaleTransform in scaleTransforms)
                    {
                        Solid catchSolid = SolidUtils.CreateTransformed(sourceSolid, scaleTransform);
                        GeometryTools.Translation translation = new GeometryTools.Translation(sourceInst, catchSolid);
                        List<Transform> transforms = new List<Transform> { translation.AsTransform() };
                        transforms.AddRange(translation.AsRotationTransforms());

                        foreach (Transform transform in transforms)
                            catchSolid = SolidUtils.CreateTransformed(catchSolid, transform);

                        TempGraphicTools.CreateDirectShapeFromSolid(doc, catchSolid);

                        filters.Add(new ElementIntersectsSolidFilter(catchSolid));
                    }

                    ElementFilter filter = new LogicalOrFilter(filters);

                    FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WherePasses(filter);
                    List<ElementId> attachedComponentIds = (from subInst in collector.Cast<FamilyInstance>()
                                                            where subInst.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString() != "Узел"
                                                            select subInst.Id).ToList();
                    List<FamilyInstance> nodes = (from subInst in collector.Cast<FamilyInstance>()
                                                  where subInst.Symbol.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString() == "Узел"
                                                  select subInst).ToList();
                    foreach (FamilyInstance node in nodes) attachedComponentIds.AddRange(node.GetSubComponentIds());

                    if (attachedComponentIds.Count > 0) ai.AddMemberIds(attachedComponentIds);
                    return ai;
                }
            }
            return null;
        }

        public static bool IsAIConsistOfRebarOnly(AssemblyInstance ai)
        {
            Document doc = ai.Document;
            List<ElementId> memberIds = ai.GetMemberIds().ToList();
            foreach (ElementId id in memberIds) { if (!(doc.GetElement(id) is Rebar)) return false; }
            return true;
        }
        public static double CalculateMass(AssemblyInstance ai)
        {
            Document doc = ai.Document;
            double mass = 0;
            foreach (ElementId elemId in ai.GetMemberIds())
            {
                Element elem = doc.GetElement(elemId);
                if (elem is Rebar)
                    mass += 7850 * ((elem as Rebar).Volume * Math.Pow(304.8, 3)) / Math.Pow(10, 9);
                else if (ElementParametersAssistant.IsParameterExistAndHasValue(elem, p_Mass))
                    mass += elem.get_Parameter(Guid.Parse(p_Mass)).AsDouble();
            }
            return mass;
        }
        public static void SetAIParameters(AssemblyInstance ai)
        {
            doc = ai.Document;
            ai.get_Parameter(Guid.Parse(p_Amount))?.Set(1);
            ai.get_Parameter(Guid.Parse(p_Mass))?.Set(CalculateMass(ai));

            if (ai.Document.IsWorkshared)
            {
                SetUserWorksetIntIds();
                if (userWorksetIntIds[0] != -1) ai.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(userWorksetIntIds[0]);
                foreach (Element elem in new ExtractingTools.ConcreteExtractor(doc, ai).ToElements())
                {
                    if (userWorksetIntIds[1] != -1 && !elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).IsReadOnly)
                        elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(userWorksetIntIds[1]);
                    if (userWorksetIntIds[2] != -1 && RebarHostData.IsValidHost(elem))
                    {
                        RebarHostData rhd = RebarHostData.GetRebarHostData(elem);
                        foreach (Rebar rebar in rhd.GetRebarsInHost())
                            if (!rebar.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).IsReadOnly)
                                rebar.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(userWorksetIntIds[2]);
                        foreach (RebarContainer container in rhd.GetRebarContainersInHost())
                            if (!container.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).IsReadOnly)
                                container.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(userWorksetIntIds[2]);
                    }
                }

                if (userWorksetIntIds[3] != -1)
                    foreach (Element elem in new ExtractingTools.ConcreteInsertExtractor(doc, ai).ToElements())
                        if (!elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).IsReadOnly)
                            elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(userWorksetIntIds[3]);
            }

        }
        public static void SetATParameters(AssemblyType at, RebarHostCache rHC)
        {
            if (at.get_Parameter(Guid.Parse(p_Identity_Name)) != null && rHC.HostMark != null)
            {
                at.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).Set(rHC.HostMark);
                if (markDescriptions == null) markDescriptions = ElemMarkDescriptions.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);
                at.get_Parameter(Guid.Parse(p_Identity_Name)).Set(markDescriptions.GetString(rHC.HostMark.Split('-')[0]).Split(';')[2]);
            }
        }
        public static void SetATName(AssemblyType at, RebarHostCache rHC)
        {
            bool condition1 = !ExistAssemblyTypeIds.Contains(at.Id);
            bool condition2 = rHC.SheetSetNames != null;
            bool condition3 = rHC.HostMark != null;
            if (condition1 && condition2 && condition3)
            {
                if (rHC.HostMark.Split('.').Count() == 3)
                    rHC.HostMark = rHC.HostMark.Split('.').First() + "." + rHC.HostMark.Split('.').Last();
                string name = rHC.SheetSetNames.Split(';').Last() + "_" + rHC.HostMark;
                at.Name = name;
            }
        }
        public static void SetATName(AssemblyType at, ViewSheetCache vSC)
        {
            bool condition1 = !ExistAssemblyTypeIds.Contains(at.Id);
            bool condition2 = !string.IsNullOrEmpty(vSC.SheetSetMark);
            bool condition3 = !string.IsNullOrEmpty(vSC.ElemMarksString);
            if (condition1 && condition2 && condition3)
            {
                string name = vSC.SheetSetMark + "_" + vSC.ElemMarksString;
                at.Name = name;
            }
        }
        public static void SetATName(AssemblyType at, string name)
        {
            doc = at.Document;
            UpdateExistAssemblyTypeList(doc);
            bool condition1 = !string.IsNullOrEmpty(name);
            bool condition2 = !ExistAssemblyTypeNames.Contains(name);
            if (condition1 && condition2) at.Name = name;
        }
        public static void SetAIToHost(AssemblyInstance ai, Element host)
        {
            bool condition1 = IsAIConsistOfRebarOnly(ai);
            bool condition2 = RebarHostData.IsValidHost(host);
            if (condition1 && condition2)
            {
                foreach (ElementId subId in ai.GetMemberIds())
                {
                    Rebar rebar = doc.GetElement(subId) as Rebar;
                    rebar.SetHostId(doc, host.Id);
                }
            }
        }


        public static void UpdateExistAssemblyTypeList(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(AssemblyType));
            ExistAssemblyTypeIds = collector.ToElementIds().ToList();
            ExistAssemblyTypeNames = (from elem in collector
                                      select elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsString()).ToList();
        }
        private static void SetUserWorksetIntIds()
        {
            userWorksetIntIds = new List<int>();
            foreach (string suffix in userWorkserSuffixes)
                userWorksetIntIds.Add(WorksetAssistant.GetOrCreateUserWorksetBySurnameIntId
                    (doc, DocSettings.Default.user_Surname + "_" + suffix));
        }


    }
}
