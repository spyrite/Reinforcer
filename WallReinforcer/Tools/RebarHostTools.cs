using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Revit.Filters;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class RebarHostTools
    {
        public static bool IsHostIsConcrete(Element elem)
        {
            bool condition1 = StructureElementFilters.Union.PassesFilter(elem);
            bool condition2 = false;
            List<string> materialNames = ExtractingTools.GetMaterialNames(elem);
            foreach (string materialName in materialNames)
                if (materialName.StartsWith("Бетон"))
                {
                    condition2 = true;
                    break;
                }
            if (condition1 && condition2) return true;
            else return false;
        }

        public static bool IsHostIsReinforced(Element elem)
        {
            if (RebarHostData.IsValidHost(elem))
            {
                RebarHostData hostData = RebarHostData.GetRebarHostData(elem);
                List<bool> conditions = new List<bool>
            {
                hostData.GetRebarsInHost().Count > 0,
                hostData.GetRebarContainersInHost().Count > 0,
                hostData.GetAreaReinforcementsInHost().Count > 0
            };
                foreach (bool condition in conditions) { if (condition) return true; }
                return false;
            }
            else return false;
        }

        public static bool IsHostIsReinforcedWithOneRCOnly(Element elem)
        {
            if (RebarHostData.IsValidHost(elem))
            {
                RebarHostData hostData = RebarHostData.GetRebarHostData(elem);
                List<bool> conditions = new List<bool>
            {
                hostData.GetRebarsInHost().Count == 0,
                hostData.GetRebarContainersInHost().Count == 1,
                hostData.GetAreaReinforcementsInHost().Count == 0
            };
                foreach (bool condition in conditions) { if (!condition) return false; }
                return true;
            }
            else return false;
        }

        public static bool IsHostIsReinforcedWithOneRCAndDetails(Element elem)
        {
            if (RebarHostData.IsValidHost(elem))
            {
                RebarHostData hostData = RebarHostData.GetRebarHostData(elem);
                List<Rebar> nonDetailRebars = (from rebar in hostData.GetRebarsInHost()
                                               where !rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM).AsString().Contains("Фрагмент")
                                               select rebar).ToList();
                List<bool> conditions = new List<bool>
            {
                nonDetailRebars.Count == 0,
                hostData.GetRebarContainersInHost().Count == 1,
                hostData.GetAreaReinforcementsInHost().Count == 0
            };
                foreach (bool condition in conditions) { if (!condition) return false; }
                return true;
            }
            else return false;
        }

        public static void SetHostParameters(Element targetElem, RebarHostCache sourceHP, int worksetId)
        {
            Document doc = targetElem.Document;
            if (targetElem is Rebar) (targetElem as Rebar).SetHostId(doc, sourceHP.Elem.Id);
            else if (targetElem is RebarContainer) (targetElem as RebarContainer).SetHostId(doc, sourceHP.Elem.Id);

            if (!targetElem.get_Parameter(BuiltInParameter.PHASE_CREATED).IsReadOnly)
                targetElem.get_Parameter(BuiltInParameter.PHASE_CREATED).Set(sourceHP.PhaseId);

            List<List<string>> parametersData = new List<List<string>>()
            {
                new List<string>{"1П_Марка конструкции", sourceHP.HostMark },
                new List<string>{"1П_Зона", sourceHP.Zone },
                new List<string>{"1П_Блок", sourceHP.Section }
            };

            for (int i = 0; i < parametersData.Count; i++)
                if (targetElem.GetParameters(parametersData[i][0]).Count > 0 & parametersData[i][1] != null)
                    targetElem.GetParameters(parametersData[i][0])[0].Set(parametersData[i][1]);

            if (!targetElem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).IsReadOnly & worksetId > 0)
                targetElem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(worksetId);
        }

        public static void HostsHighlight(Autodesk.Revit.UI.Selection.Selection uiSel, List<Element> reinf)
        {
            List<ElementId> hostIds = new List<ElementId>();
            foreach (Element elem in reinf)
            {
                if (elem is Rebar) hostIds.Add((elem as Rebar).GetHostId());
                else if (elem is RebarInSystem) hostIds.Add((elem as RebarInSystem).GetHostId());
                else if (elem is RebarContainer) hostIds.Add((elem as RebarContainer).GetHostId());
            }
            hostIds = hostIds.Distinct().ToList();
            List<ElementId> ids = hostIds;
            ids.AddRange(uiSel.GetElementIds());
            uiSel.SetElementIds(ids);
        }


        /*public static List<Element> GetOtherPartsForMainElementAsWall(Wall mainWall)
        {
            WallCache wCache = new WallCache(mainWall);
            if (wCache.Geom.Solid == null) wCache.Geom.GetSolidData();
            List<Element> attWalls = (from host in wCache.GetAttachedRebarHosts() 
                                      where host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue
                                      && host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString() == wCache.HostMark
                                      select host).ToList();
            if (attWalls.Count > 0)
            {
                int i = 0;
                while (i < attWalls.Count)
                {
                    wCache = new WallCache(attWalls[i]);
                    if (wCache.Geom.Solid == null) wCache.Geom.GetSolidData();
                    List<Element> nextAttWalls = wCache.GetAttachedRebarHosts();
                    foreach (Element wall in nextAttWalls)
                        if (attWalls.Contains(wall) && wall != mainWall)
                            attWalls.Add(wall);
                    i++;
                }
            }
            return attWalls;
        }*/

        public static List<Element> GetOtherPartsForMainElement(Element mainElem)
        {
            List<Element> otherParts = new List<Element> { mainElem };
            List<ElementId> otherPartIds = new List<ElementId>();
            List<Element> catchedElems;
            RebarHostCache rhc;
            int i = 0;
            while (i < otherParts.Count)
            {
                if (StructureElementFilters.Beams.PassesFilter(otherParts[i])) rhc = new BeamCache(otherParts[i]);
                else if (StructureElementFilters.Walls.PassesFilter(otherParts[i])) rhc = new WallCache(otherParts[i]);
                else if (StructureElementFilters.Floors.PassesFilter(otherParts[i])) rhc = new SlabCache(otherParts[i]);
                else { i++; continue; }

                if (rhc.Geom.Solid == null) rhc.Geom.GetSolidData();
                catchedElems = (from host in ExtractingTools.GetAttachedRebarHosts(rhc, 10/304.8)
                                where host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue
                                && host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString() == rhc.HostMark
                                select host).ToList();
                foreach (Element elem in catchedElems)
                    if (!otherPartIds.Contains(elem.Id) && elem.Id != mainElem.Id)
                    {
                        otherParts.Add(elem);
                        otherPartIds.Add(elem.Id);
                    }
                i++;
            }
            otherParts.Remove(mainElem);
            return otherParts;
        }

        public class Reinforcement
        {
            private readonly List<Element> hosts;
            private readonly List<ElementId> reinfIds = new List<ElementId>();
            private RebarHostData hostData;

            public Reinforcement(Element elem) { hosts = new List<Element> { elem }; SetReinfIds(); }
            public Reinforcement(List<Element> elems) { hosts = elems; SetReinfIds(); }

            public void Delete()
            {
                if (reinfIds.Count > 0) hosts[0].Document.Delete(reinfIds);
            }
            public void Highlight(Autodesk.Revit.UI.Selection.Selection uiSel) { uiSel.SetElementIds(reinfIds); }

            private void SetReinfIds()
            {
                foreach (Element elem in hosts)
                {
                    if (RebarHostData.IsValidHost(elem))
                    {
                        hostData = RebarHostData.GetRebarHostData(elem);
                        foreach (AreaReinforcement ar in hostData.GetAreaReinforcementsInHost()) reinfIds.Add(ar.Id);
                        foreach (AreaReinforcement ar in hostData.GetAreaReinforcementsInHost()) reinfIds.AddRange(ar.GetRebarInSystemIds());
                        foreach (Rebar rebar in hostData.GetRebarsInHost()) reinfIds.Add(rebar.Id);
                        foreach (RebarContainer container in hostData.GetRebarContainersInHost()) reinfIds.Add(container.Id);
                    }
                }
            }
        }
    }
}
