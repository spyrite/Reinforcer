using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

//using Application = RevitOSA.AppServices.Application;
using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Tools;
using RevitOSA.WallReinforcer.Transfer;


namespace RevitOSA.WallReinforcer.Assistants
{
    public static class RebarContainerAssistant
    {
        private static readonly Dictionary<string, string> containerNames = new Dictionary<string, string>
        {
            {"Несущие колонны", "Колонна"},
            {"Стены", "Диафрагма"},
            {"Каркас несущий", "Армирование балок"},
            {"Перекрытия", "Армирование плит"}
        };

        private static bool RebarsOnly;

        public static List<ElementId> Assemble(Document doc, List<ElementId> elemIds, bool rebarsOnly)
        {
            RebarsOnly = rebarsOnly;
            List<RebarCache> rcs = ExtractingTools.ExtractRCs(doc, elemIds);

#if REVIT2023
            List<int> parentIdIntegerValues = (from rc in rcs
                                               select rc.ParentId.IntegerValue).Distinct().ToList();
#else
            List<int> parentIdIntegerValues = (from rc in rcs
                                               select (int)rc.ParentId.IntegerValue).Distinct().ToList();
#endif
            List<List<RebarCache>> rcSets = new List<List<RebarCache>>();
            foreach (int idInt in parentIdIntegerValues)
            {
#if REVIT2023 || REVIT2024 || REVIT2025
                List<RebarCache> rcSet = (from rc in rcs
                                          where rc.ParentId.IntegerValue == idInt
                                          select rc).ToList();
#else
                List<RebarCache> rcSet = (from rc in rcs
                                          where (int)rc.ParentId.IntegerValue == idInt
                                          select rc).ToList();
#endif
                rcSets.Add(rcSet);
            }

            List<RebarContainer> rebarContainers = new List<RebarContainer>();

            foreach (List<RebarCache> rcSet in rcSets)
            {
                RebarContainerType containerType = GetType(rcSet);
                RebarContainer container = RebarContainer.Create(doc, rcSet[0].Host, containerType.Id);
                rebarContainers.Add(container);
            }

            for (int i = 0; i < rebarContainers.Count; i++)
            {
                RebarContainer container = rebarContainers[i];
                List<RebarCache> filteredRCs = rcSets[i];
                //SetRebarContainerEntity(container, filteredRCs);
                for (int j = 0; j < filteredRCs.Count; j++)
                {
                    RebarCache rc = filteredRCs[j];
                    if (!rc.Partition.Contains("Фрагмент"))
                    {
                        if (rc.HasMovedBar()) AppendDividedRCIs(j, rc, container);
                        else if (rc.Rebar.DistributionType == DistributionType.VaryingLength) AppendDividedVaryingLengthRebar(rc, container);
                        else container.AppendItemFromRebar(rc.Rebar);
                    }
                    else { rcs.Remove(rc); }
                    ;
                }
                doc.Regenerate();
                container.SetUnobscuredInView(doc.ActiveView, true);
                RebarContainerCache rcc = new RebarContainerCache(container);
                SetMark(rcc, filteredRCs[0]);
                SetParameters(rcc);
            }

            if (ReinfSettings.Default.container_KeepSourceRebars == false)
            {
                List<ElementId> ids = new List<ElementId>();
                foreach (RebarCache rc in rcs) { ids.Add(rc.Rebar.Id); }
                ;
                doc.Delete(ids);
            }

            List<ElementId> rebarContainerIds = (from container in rebarContainers select container.Id).ToList();
            return rebarContainerIds;
        }
        public static List<ElementId> Disassemble(Document doc, List<ElementId> elemIds, bool saveContainer)
        {
            List<RebarContainerCache> RCCs = ExtractingTools.ExtractRCCs(doc, elemIds);
            List<ElementId> extractedRebarIds = new List<ElementId>();

            foreach (RebarContainerCache rcc in RCCs)
            {
                /*Schema schema = (from guid in rcc.Elem.GetEntitySchemaGuids()
                                   where guid.ToString() == RevitSchemas.RebarData_Guid.ToLower()
                                   select Schema.Lookup(guid)).FirstOrDefault();
                IList<string> partitions = new List<string>();
                if (schema != null)
                {
                    Entity entity = rcc.Elem.GetEntity(schema);
                    partitions = entity.Get<IList<string>>("Partitions");
                }*/

                for (int i = 0; i < rcc.RCICs.Count; i++)
                {
                    RebarCache rcic = rcc.RCICs[i];
                    Rebar rebar = Rebar.CreateFromCurvesAndShape(doc, rcic.Shape, rcic.PrimaryData.BarType,
                        rcic.HookTypes[0], rcic.HookTypes[1], rcc.Host, rcic.Normal,
                        rcic.PrimaryData.CenterLines, rcic.HookOrients[0], rcic.HookOrients[1]) ?? Rebar.CreateFromCurves(doc, rcic.Style, rcic.PrimaryData.BarType,
                        rcic.HookTypes[0], rcic.HookTypes[1], rcc.Host, rcic.Normal,
                        rcic.PrimaryData.CenterLines, rcic.HookOrients[0], rcic.HookOrients[1], true, true);
                    if (!(rebar == null))
                    {
                        rcic.MatchLayoutRuleFromRCI(rebar);
                        rebar.get_Parameter(BuiltInParameter.PHASE_CREATED).Set(rcc.PhaseId);
                        //if (partitions.Count > 0) rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM).Set(partitions[i]);
                        if (doc.ActiveView != null) rebar.SetUnobscuredInView(doc.ActiveView, true);
                        rebar.get_Parameter(new Guid(p_Identity_Zone)).Set(rcc.Zone);
                    }
                    extractedRebarIds.Add(rebar.Id);
                }
                if (!saveContainer) doc.Delete(rcc.Elem.Id);
            }
            return extractedRebarIds;
        }

        public static ElementId RebarContainerToAssembly(Document doc, ElementId elemId, bool newHost, bool saveContainer, out string failureMessage)
        {
            Element elem = doc.GetElement(elemId);
            if (!(elem is RebarContainer))
            {
                failureMessage = "Элемент не является арматурным контейнером";
                return null;
            }

            RebarContainerCache rcc = new RebarContainerCache(elem as RebarContainer);
            Element host = rcc.Host;
            if (newHost)
            {
                host = rcc.CreateHost(doc, rcc.Host.Category, rcc.Mark, rcc.Origin);
                ReinforcementTransfer.SetCopyHostParameters(host as DirectShape, rcc.Host);
            }

            RebarHostCache rHC = new RebarHostCache(host);

            if (rHC.HostMark == null)
            {
                failureMessage = "Арматурный контейнер не промаркирован (не заполнен встроенный параметр 'Марка'";
                return null;
            }

            if (rHC.HostMark.Split('.').Count() == 3)
            {
                rHC.HostMark = rHC.HostMark.Split('.').First() + "." + rHC.HostMark.Split('.').Last();
                rHC.Elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(rHC.HostMark);
            }

            if (AssemblyTools.ExistAssemblyTypeNames == null) AssemblyTools.UpdateExistAssemblyTypeList(doc);
            string atName = rHC.SheetSetNames.Split(';').Last() + "_" + rHC.HostMark;

            if (AssemblyTools.ExistAssemblyTypeNames.Contains(atName))
            {
                failureMessage = "Сборка с таким именем (" + atName + ") уже существует";
                return null;
            }

            AssemblyTools.ExistAssemblyTypeNames.Add(atName);
            ElementId aiCatId = Category.GetCategory(doc, BuiltInCategory.OST_Rebar).Id;

            List<ElementId> memberIds = new List<ElementId>();
            if (newHost)
            {
                RebarCoverAssistant.SetRebarCoversToHost(rHC.Elem, doc, 0);
                memberIds.Add(host.Id);
            }
            List<ElementId> containerIds = new List<ElementId> { rcc.Elem.Id };
            List<ElementId> rebarIds = Disassemble(doc, containerIds, true);
            foreach (ElementId id in rebarIds)
            {
                Rebar rebar = doc.GetElement(id) as Rebar;
                rebar.SetHostId(doc, host.Id);
            }
            memberIds.AddRange(rebarIds);

            doc.Regenerate();
            AssemblyInstance ai = AssemblyInstance.Create(doc, memberIds, aiCatId);
            doc.Regenerate();

            AssemblyTools.SetAIParameters(ai);
            failureMessage = null;

            if (!saveContainer) doc.Delete(elemId);

            return ai.Id;
        }


        /*public static void UpdateRCHost(RebarContainerCache rcp, List<ElementId> allConcreteIds)
        {
            Document doc = rcp.Elem.Document;
            Element newHost = null;
            ElementFilter filter = new BoundingBoxContainsPointFilter(rcp.Center);
            List<Element> concretes = (List<Element>)new FilteredElementCollector(doc, allConcreteIds).WherePasses(filter).ToElements();
            List<Curve> lines = new List<Curve>();
            foreach (RebarContainerItemCache rci in rcp.RPs) { lines.AddRange(rci.Lines); };
            foreach (Element elem in concretes)
            {
                Solid solid = GeometryTools.GetSolid(elem);
                foreach (Curve line in lines)
                {
                    List<Curve> cutLines = solid.IntersectWithCurve(line, null).ToList();
                    if (cutLines.Count > 0)
                    {
                        newHost = elem;
                        rcp.Elem.SetHostId(doc, newHost.Id);
                        rcp.Host = newHost;
                        break;
                    }
                }
                if (newHost != null) break;
            }
            SetContainerParameters(rcp);
        }
        public static List<ElementId> UpdateRCHost2(Document doc, RebarContainerCache rcp, List<ElementId> allConcreteIds)
        {
            ElementId oldHostId = rcp.Host.Id;
            List<ElementId> hostIds = new List<ElementId> { oldHostId, oldHostId };
            List<Curve> lines = new List<Curve>();
            foreach (RebarContainerItemCache rp in rcp.RPs) lines.AddRange(rp.Lines);

            ElementFilter filter = new BoundingBoxContainsPointFilter(rcp.Center);
            List<ElementId> filteredConcreteIds = new FilteredElementCollector(doc, allConcreteIds).WherePasses(filter).ToElementIds().ToList();

            foreach (ElementId id in filteredConcreteIds)
            {
                GeometryTools.GetModifiedGeometry(doc, id);
                Element elem = doc.GetElement(id);
                Solid solid = GeometryTools.GetSolid(elem);
                foreach (Curve line in lines)
                {
                    List<Curve> cutLines = solid.IntersectWithCurve(line, null).ToList();
                    if (cutLines.Count > 0)
                    {
                        rcp.Elem.SetHostId(doc, id);
                        rcp.Host = elem;
                        hostIds[1] = id;
                    }
                }
            }
            return hostIds;
        }*/

        public static void SetNewHost(Document doc, ElementId newHostId, List<ElementId> containerIds)
        {
            foreach (ElementId containerId in containerIds)
            {
                RebarContainer container = doc.GetElement(containerId) as RebarContainer;
                ElementId oldHostId = container.GetHostId();

                if (oldHostId != newHostId)
                {
                    container.SetHostId(doc, newHostId);
                    /*if (containerIds.Count == 1)
                    {
                        FailureMessage fm = new FailureMessage(Application.UserNotificationIds[NotificationName.RCChangingHost]);
                        fm.SetFailingElement(container.Id);
                        fm.SetAdditionalElements(new List<ElementId> { oldHostId, newHostId });
                        doc.PostFailure(fm);
                    }*/
                }

                RebarContainerCache rcc = new RebarContainerCache(container);
                SetParameters(rcc);
            }

        }
        public static void SetParameters(RebarContainerCache rcc)
        {
            rcc.Elem.PresentItemsAsSubelements = true;

            RebarContainerParameterManager overrideMan = rcc.Elem.GetParametersManager();
            overrideMan.ClearOverrides();
            ElementId partParId = rcc.Elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM).Id;
            overrideMan.AddOverride(partParId, ReinfSettings.Default.container_PartitionName);

            ElementId phaseId = rcc.Host.get_Parameter(BuiltInParameter.PHASE_CREATED).AsElementId();
            if (!(rcc.Elem.get_Parameter(BuiltInParameter.PHASE_CREATED).IsReadOnly)) rcc.Elem.get_Parameter(BuiltInParameter.PHASE_CREATED).Set(phaseId);

            List<string> userParameterNames = new List<string>()
            {
                "1П_Марка конструкции",
                "1П_Количество",
                "1П_Масса",
                "1П_Зона",
                "1Пп_Наименование контейнера"
            };

            IList<Parameter> parameters;

            if (rcc.Host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue)
            {
                string hostMark = rcc.Host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                parameters = rcc.Elem.GetParameters(userParameterNames[0]);
                if (parameters.Count > 0 && !parameters[0].IsReadOnly) parameters[0].Set(hostMark);
            }

            double n = rcc.GetAmount();
            parameters = rcc.Elem.GetParameters(userParameterNames[1]);
            if (parameters.Count > 0 && !parameters[0].IsReadOnly) parameters[0].Set(n);

            double mass = rcc.CalculateMass();
            parameters = rcc.Elem.GetParameters(userParameterNames[2]);
            if (parameters.Count > 0 && !parameters[0].IsReadOnly) parameters[0].Set(mass);

            if (rcc.Elem.GetParameters(userParameterNames[3]).Count > 0)
            {
                string zone = rcc.Host.GetParameters(userParameterNames[3])[0].AsString();
                parameters = rcc.Elem.GetParameters(userParameterNames[3]);
                if (parameters.Count > 0 && !parameters[0].IsReadOnly) parameters[0].Set(zone);
            }

            parameters = rcc.Elem.GetParameters(userParameterNames[4]);
            if (parameters.Count > 0 && !parameters[0].IsReadOnly)
                if (RebarsOnly && !string.IsNullOrEmpty(ReinfSettings.Default.container_Name)) parameters[0].Set(ReinfSettings.Default.container_Name);
                else parameters[0].Set(rcc.ElemTypeName);
        }
        public static void SetMark(RebarContainerCache rcc, RebarCache rc)
        {
            Document doc = rc.Rebar.Document;
            Element parent = doc.GetElement(rc.ParentId);
            if (parent is AssemblyInstance)
            {
                rcc.ATMark = rc.ATMark;
                rcc.Elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(rc.ATMark);
                rcc.Elem.get_Parameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK).Set(rc.ATMark);
            }
            else if (RebarsOnly)
            {
                rcc.Elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(ReinfSettings.Default.container_Mark);
                rcc.Elem.get_Parameter(BuiltInParameter.REBAR_ELEM_SCHEDULE_MARK).Set(ReinfSettings.Default.container_Mark);
            }
        }

        private static void AppendDividedRCIs(int j, RebarCache rc, RebarContainer container)
        {
            //Entity entity = SchemaCreator.GetEntityBySchemaGuid(container, new Guid(RevitSchemas.RebarData_Guid));
            //IList<string> partitions = entity.Get<IList<string>>("Partitions");
            //partitions.RemoveAt(j);
            List<RebarCache.LineSetData> lineSet = rc.GetLineSetsData();
            for (int k = 0; k < lineSet.Count; k++)
            {
                RebarCache.LineSetData data = lineSet[k];
                RebarContainerItem item = container.AppendItemFromCurvesAndShape(rc.Shape, rc.PrimaryData.BarType, rc.HookTypes[0], rc.HookTypes[1], rc.Normal, data.Curves, rc.HookOrients[0], rc.HookOrients[1]);
                rc.MatchLayoutRuleFromRebar(item, data.N);
                //partitions.Insert(j + k, rc.Partition);
            }
            //entity.Set("Partitions", partitions);
            //container.SetEntity(entity);
        }

        private static void AppendDividedVaryingLengthRebar(RebarCache rc, RebarContainer container)
        {
            List<RebarCache.LineSetData> lineSet = rc.GetLineSetsData();
        }

        private static bool IsInRunningMeters(List<RebarCache> rcs)
        {
            foreach (RebarCache rc in rcs) { if (rc.PrimaryData.InRunningMeters) return true; }
            ;
            return false;
        }
        private static string GetTypeName(RebarCache rc)
        {
            if (!RebarsOnly)
            {
                Document doc = rc.Rebar.Document;
                Element parent = doc.GetElement(rc.ParentId);
                if (parent is AssemblyInstance)
                {
                    AssemblyType at = doc.GetElement(parent.GetTypeId()) as AssemblyType;
                    bool condition1 = at.GetParameters("1П_Наименование").Count() > 0;
                    bool condition2 = at.GetParameters("1П_Наименование")[0].HasValue;
                    bool condition3 = at.GetParameters("1П_Наименование")[0].AsString() != "";
                    if (condition1 & condition2 & condition3) { return at.GetParameters("1П_Наименование")[0].AsString(); }
                    else return "Каркас";
                }
                else if (containerNames.ContainsKey(parent.Category.Name)) { return containerNames[parent.Category.Name]; }
                else return "Контейнер";
            }
            else return ReinfSettings.Default.container_TypeName;
        }
        private static RebarContainerType GetType(List<RebarCache> rcs)
        {
            Document doc = rcs[0].Rebar.Document;
            bool isRCInMeters = IsInRunningMeters(rcs);
            string suffix = "";
            int n = 0;
            if (isRCInMeters) { suffix = "_ПМ"; n = 1; }
            string containerTypeName = GetTypeName(rcs[0]) + suffix;
            ElementId containerTypeId = RebarContainerType.GetOrCreateRebarContainerType(doc, containerTypeName);
            RebarContainerType containerType = doc.GetElement(containerTypeId) as RebarContainerType;
            if (containerType.GetParameters("1П_Размер_В погонных метрах").Count() > 0) containerType.GetParameters("1П_Размер_В погонных метрах")[0].Set(n);
            return containerType;
        }
        /*private static void SetRebarContainerEntity(RebarContainer container, List<RebarCache> rcs)
        {
            Entity entity;
            try { entity = new Entity(new Guid(RevitSchemas.RebarData_Guid)); }
            catch 
            {
                Schema schema = SchemaCreator.CreateRebarSchema();
                entity = new Entity(schema);
            }
            Field field = entity.Schema.GetField("Partitions");
            IList<string> partitions = (from rc in rcs select rc.Partition).ToList();
            entity.Set(field, partitions);
            container.SetEntity(entity);
        }*/
    }
}
