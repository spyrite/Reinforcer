using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static RevitOSA.WallReinforcer.Tools.ExtractingTools;
using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using Document = Autodesk.Revit.DB.Document;

namespace RevitOSA.WallReinforcer.Transfer
{
    public static class ReinforcementTransfer
    {
        private static Document doc;
        private static Dictionary<Type, List<ElementId>> sourceRebarHostMembers;
        private static Dictionary<Type, List<ElementId>> targetRebarHostMembers;
        private static Dictionary<Type, int> sourceWorksets;
        private static List<ElementId> sourceCoverIds;
        private static RebarHostCache sourceRHC;
        private static RebarHostCache targetRHC;
        private static List<RebarHostCache> tempRHCs;
        private static List<double> tempFloorHostAngleData;
        private static CopyPasteOptions cpo;
        private static GeometryTools.Translation movement;

        public static void CopyFromHostToHosts(Element elem, List<Element> targetHosts)
        {
            // Инициализация
            sourceRHC = GetHostCache(elem);
            doc = sourceRHC.Elem.Document;
            sourceRebarHostMembers = GetRebarHostMembers(sourceRHC.Elem);
            GetSourceWorksets();
            sourceCoverIds = RebarCoverAssistant.GetHostRebarCoverIds(sourceRHC.Elem);
            cpo = new CopyPasteOptions();
            cpo.SetDuplicateTypeNamesHandler(new DTNHadler());

            foreach (Element targetHost in targetHosts)
            {
                targetRHC = GetHostCache(targetHost);

                // Сопоставление защитных слоёв исходного хоста и целевого хоста
                if (sourceRHC.Elem.GetType() == targetHost.GetType()) RebarCoverAssistant.SetRebarCoversToHost(targetHost, sourceCoverIds);

                // Получение данных по перемещению (вектор и углы поворота)
                movement = new GeometryTools.Translation(sourceRHC.Elem, targetHost);

                // Создание временного хоста 1 поверх исходного хоста
                tempRHCs = new List<RebarHostCache>();
                CreateTempHost1AsFloor();

                //tempHost = doc.GetElement(ElementTransformUtils.CopyElements(doc, new List<ElementId> { sourceHost.Id }, doc, movement.AsTransform(), cpo).ToList().FirstOrDefault());

                if (tempRHCs.First().Elem != null)
                {
                    // Временный рехост арматуры, контейнеров и сборок из арматуры на временный хост 1
                    if (ReinfSettings.Default.reinf_Tranfer_Rebars) SetRebarsToHost(sourceRebarHostMembers[typeof(Rebar)], tempRHCs.First(), false);
                    if (ReinfSettings.Default.reinf_Tranfer_RebarContainers) SetRebarsToHost(sourceRebarHostMembers[typeof(RebarContainer)], tempRHCs.First(), false);
                    if (ReinfSettings.Default.reinf_Tranfer_AIs) SetAIsToHost(sourceRebarHostMembers[typeof(AssemblyInstance)], tempRHCs.First(), false);

                    // Копирование временного хоста 2 вместе с арматурой (получение временного хоста 2 поверх целевого хоста)
                    ElementId tempHost2Id = ElementTransformUtils.CopyElement(doc, tempRHCs.First().Elem.Id, movement.AsVec()).ToList().First();
                    tempRHCs.Add(new RebarHostCache(doc.GetElement(tempHost2Id)));

                    // Вращение временного хоста 2 
                    //PerformRotation(tempRHCs.Last().Elem);

                    // Перенос арматуры с временного хоста 2 на целевой
                    targetRebarHostMembers = GetRebarHostMembers(tempRHCs.Last().Elem);

                    //if (StructureElementFilters.Columns.PassesFilter(tempHost))
                    //{
                    //    ElementId tempGroupId = CreateTempGroup(targetRebarHostMembers, null);
                    //    doc.Regenerate();
                    //    ElementTransformUtils.MoveElement(doc, tempGroupId, new XYZ(0, 0, movement.AsVec().Z));
                    //    (doc.GetElement(tempGroupId) as Group).UngroupMembers();
                    //}


                    if (ReinfSettings.Default.reinf_Tranfer_Rebars) SetRebarsToHost(targetRebarHostMembers[typeof(Rebar)], targetRHC, true);
                    if (ReinfSettings.Default.reinf_Tranfer_RebarContainers) SetRebarsToHost(targetRebarHostMembers[typeof(RebarContainer)], targetRHC, true);
                    if (ReinfSettings.Default.reinf_Tranfer_AIs) SetAIsToHost(targetRebarHostMembers[typeof(AssemblyInstance)], targetRHC, true);
                    if (ReinfSettings.Default.reinf_Tranfer_ARs) CopyMembersToTargetHost(typeof(AreaReinforcement));
                    if (ReinfSettings.Default.reinf_Tranfer_CIs && sourceRebarHostMembers.ContainsKey(typeof(FamilyInstance))) CreateNewCIsOnTargetHost();
                    //CopyMembersToTargetHost(typeof(FamilyInstance)); 

                    // Перенос арматуры с временного хоста 1 на исходный
                    if (ReinfSettings.Default.reinf_Tranfer_Rebars) SetRebarsToHost(sourceRebarHostMembers[typeof(Rebar)], sourceRHC, false);
                    if (ReinfSettings.Default.reinf_Tranfer_RebarContainers) SetRebarsToHost(sourceRebarHostMembers[typeof(RebarContainer)], sourceRHC, false);
                    if (ReinfSettings.Default.reinf_Tranfer_AIs) SetAIsToHost(sourceRebarHostMembers[typeof(AssemblyInstance)], sourceRHC, false);

                    // Удаление временного хостов
                    foreach (RebarHostCache tempRHC in tempRHCs)
                        doc.Delete(tempRHC.Elem.Id);
                }
            }
        }
        public static void MoveFromHostToHost(Element elem, Element targetHost)
        {
            doc = targetHost.Document;
            sourceRHC = new RebarHostCache(elem);

            GetSourceWorksets();
            movement = new GeometryTools.Translation(sourceRHC.Elem, targetHost);

            targetRebarHostMembers = GetRebarHostMembers(sourceRHC.Elem);
            targetRHC = new RebarHostCache(targetHost);

            ElementId tempGroupId = CreateTempGroup(targetRebarHostMembers, null);
            ElementTransformUtils.MoveElement(doc, tempGroupId, movement.AsVec());
            SetRebarsToHost(targetRebarHostMembers[typeof(Rebar)], targetRHC, true);
            SetRebarsToHost(targetRebarHostMembers[typeof(RebarContainer)], targetRHC, true);
            SetAIsToHost(targetRebarHostMembers[typeof(AssemblyInstance)], targetRHC, true);
            (doc.GetElement(tempGroupId) as Group).UngroupMembers();
        }
        public static void SetCopyHostParameters(DirectShape copyHost, Element sourceHost)
        {
            if (!sourceHost.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue)
                copyHost.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(sourceHost.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString());
            if (sourceHost.get_Parameter(Guid.Parse(p_Identity_Zone)) != null && copyHost.get_Parameter(Guid.Parse(p_Identity_Zone)) != null)
                copyHost.get_Parameter(Guid.Parse(p_Identity_Zone)).Set(sourceHost.get_Parameter(Guid.Parse(p_Identity_Zone)).AsString());
            if (sourceHost.get_Parameter(Guid.Parse(p_Identity_Section)) != null && copyHost.get_Parameter(Guid.Parse(p_Identity_Section)) != null)
                copyHost.get_Parameter(Guid.Parse(p_Identity_Section)).Set(sourceHost.get_Parameter(Guid.Parse(p_Identity_Section)).AsString());
        }
        private static Dictionary<Type, List<ElementId>> GetRebarHostMembers(Element host)
        {
            Dictionary<Type, List<ElementId>> rebarHostMembers = new Dictionary<Type, List<ElementId>>();
            List<ElementId> memberIds;
            if (RebarHostData.IsValidHost(host))
            {
                RebarHostData hostData = RebarHostData.GetRebarHostData(host);
                memberIds = (from rebar in hostData.GetRebarsInHost()
                             where rebar.AssemblyInstanceId == ElementId.InvalidElementId
                             select rebar.Id).ToList();
                rebarHostMembers.Add(typeof(Rebar), memberIds);
                memberIds = (from rebar in hostData.GetRebarsInHost()
                             where rebar.AssemblyInstanceId != ElementId.InvalidElementId
                             select rebar.AssemblyInstanceId).Distinct().ToList();
                rebarHostMembers.Add(typeof(AssemblyInstance), memberIds);
                memberIds = (from ar in hostData.GetAreaReinforcementsInHost()
                             select ar.Id).ToList();
                rebarHostMembers.Add(typeof(AreaReinforcement), memberIds);
                memberIds = (from container in hostData.GetRebarContainersInHost()
                             select container.Id).ToList();
                rebarHostMembers.Add(typeof(RebarContainer), memberIds);
            }
            memberIds = new ConcreteInsertExtractor(doc, host).ToElementIds();
            if (memberIds.Count > 0) rebarHostMembers.Add(typeof(FamilyInstance), memberIds);

            return rebarHostMembers;
        }
        private static ElementId CreateTempGroup(Dictionary<Type, List<ElementId>> rebarHostMembers, Element host)
        {
            List<ElementId> tempGroupMemberIds = new List<ElementId>();
            if (host != null) tempGroupMemberIds.Add(host.Id);
            foreach (KeyValuePair<Type, List<ElementId>> entity in rebarHostMembers)
            {
                if (entity.Key == typeof(AssemblyInstance))
                    foreach (ElementId id in entity.Value)
                        tempGroupMemberIds.AddRange((doc.GetElement(id) as AssemblyInstance).GetMemberIds());
                else tempGroupMemberIds.AddRange(entity.Value);
            }
            Group tempGroup = doc.Create.NewGroup(tempGroupMemberIds);
            return tempGroup.Id;
        }
        private static void GetSourceWorksets()
        {
            int worksetId;
            sourceWorksets = new Dictionary<Type, int>();
            foreach (KeyValuePair<Type, List<ElementId>> entity in sourceRebarHostMembers)
            {
                if (entity.Value.Count > 0)
                {
                    Element elem = doc.GetElement(entity.Value.First());
                    worksetId = elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).AsInteger();
                }
                else worksetId = -1;
                sourceWorksets.Add(entity.Key, worksetId);
            }
            ;
        }
        private static void SetRebarsToHost(List<ElementId> rebarIds, RebarHostCache hostCache, bool allowSettingHostParameters)
        {
            foreach (ElementId id in rebarIds)
            {
                Element member = doc.GetElement(id);
                switch (member.GetType().Name)
                {
                    case "Rebar": (member as Rebar).SetHostId(doc, hostCache.Elem.Id); break;
                    case "RebarContainer": (member as RebarContainer).SetHostId(doc, hostCache.Elem.Id); break;
                }
                ;

                if (allowSettingHostParameters) RebarHostTools.SetHostParameters(member, hostCache, sourceWorksets[member.GetType()]);
            }
        }
        private static void SetAIsToHost(List<ElementId> aiIds, RebarHostCache hostCache, bool allowSettingHostParameters)
        {
            foreach (ElementId id in aiIds)
            {
                AssemblyInstance ai = doc.GetElement(id) as AssemblyInstance;
                foreach (ElementId subId in ai.GetMemberIds())
                {
                    Rebar rebar = doc.GetElement(subId) as Rebar;
                    rebar.SetHostId(doc, hostCache.Elem.Id);
                }
                RebarHostTools.SetHostParameters(ai, hostCache, sourceWorksets[typeof(AssemblyInstance)]);
            }
        }
        /*private static void CopyMembersToTempHost(Type membersType)
        {
            if (sourceRebarHostMembers.ContainsKey(membersType) && sourceRebarHostMembers[membersType].Count > 0)
            {
                List<ElementId> ids = ElementTransformUtils.CopyElements(doc, sourceRebarHostMembers[membersType], new XYZ()).ToList();
                foreach (ElementId id in ids)
                {
                    Element member = doc.GetElement(id);
                    switch (membersType.Name)
                    {
                        case "Rebar": (member as Rebar).SetHostId(doc, tempHost.Id); break;
                        case "RebarContainer": (member as RebarContainer).SetHostId(doc, tempHost.Id); break;
                        case "AssemblyInstance": AssemblyTools.SetAIToHost(member as AssemblyInstance, tempHost); break;
                    }
                }
            }
        }*/
        private static void CopyMembersToTargetHost(Type membersType)
        {
            if (sourceRebarHostMembers.ContainsKey(membersType) && sourceRebarHostMembers[membersType].Count > 0)
            {
                List<ElementId> ids = ElementTransformUtils.CopyElements(doc, sourceRebarHostMembers[membersType], doc, movement.AsTransform(), cpo).ToList();
                foreach (ElementId id in ids)
                {
                    Element member = doc.GetElement(id);
                    PerformRotation(member);
                    RebarHostTools.SetHostParameters(member, targetRHC, sourceWorksets[membersType]);
                }
            }
        }
        private static void CreateNewCIsOnTargetHost()
        {
            List<FamilyInstanceCreationData> fiCreationDatas = new List<FamilyInstanceCreationData>();
            targetRHC.Geom.GetSolidData();
            foreach (ElementId sourceCIId in sourceRebarHostMembers[typeof(FamilyInstance)])
            {
                FamilyInstance fi = doc.GetElement(sourceCIId) as FamilyInstance;
                if (ConcreteInsertExtractor.CheckAndAddConcreteInsert(fi))
                {
                    XYZ loc = (fi.Location as LocationPoint).Point;
                    loc = movement.AsTransform().OfPoint(loc);
                    XYZ dir = fi.GetTransform().BasisX;
                    foreach (Transform rotationTransform in movement.AsRotationTransforms())
                    {
                        loc = rotationTransform.OfPoint(loc);
                        dir = rotationTransform.OfVector(dir);
                    }
                    Face targetFace = (from face in targetRHC.Geom.Faces.All
                                       where face.Project(loc) != null && Math.Round(face.Project(loc).Distance * 304.8) == 0
                                       select face).ToList().FirstOrDefault();
                    if (targetFace != null)
                    {
                        FamilySymbol fs = fi.Symbol;
                        FamilyInstanceCreationData fiCreationData = new FamilyInstanceCreationData(targetFace, loc, dir, fs);
                        fiCreationDatas.Add(fiCreationData);
                    }
                }
            }
            if (fiCreationDatas.Count > 0)
            {
                List<ElementId> ids = doc.Create.NewFamilyInstances2(fiCreationDatas).ToList();
                foreach (ElementId id in ids)
                {
                    Element ci = doc.GetElement(id);
                    RebarHostTools.SetHostParameters(ci, targetRHC, sourceWorksets[typeof(FamilyInstance)]);
                }
            }
        }
        private static void PerformRotation(Element elem)
        {
            ElementTransformUtils.RotateElement(doc, elem.Id, movement.RD.Axis1, movement.RD.Angle1);
            if (elem is Floor && tempFloorHostAngleData != null)
            {
                string degrees = Math.Round(180 / Math.PI * tempFloorHostAngleData.First(), 2).ToString();
                (elem as Floor).get_Parameter(BuiltInParameter.ROOF_SLOPE).SetValueString(degrees);
            }
            else
            {
                ElementTransformUtils.RotateElement(doc, elem.Id, movement.RD.Axis2, movement.RD.Angle2);
                ElementTransformUtils.RotateElement(doc, elem.Id, movement.RD.Axis3, movement.RD.Angle3);
            }
        }
        private static void CreateTempHost1AsFloor()
        {
            Solid sourceSolid = GeometryTools.GetSolid(sourceRHC.Elem, false);
            XYZ origin = sourceSolid.ComputeCentroid();
            List<XYZ> points = new List<XYZ>
            {
                origin - XYZ.BasisX * 10 - XYZ.BasisY * 10,
                origin - XYZ.BasisX * 10 + XYZ.BasisY * 10,
                origin + XYZ.BasisX * 10 + XYZ.BasisY * 10,
                origin + XYZ.BasisX * 10 - XYZ.BasisY * 10
            };
#if REVIT2023 || REVIT2024 || REVIT2025
            List<CurveLoop> tempHostProfile = new List<CurveLoop> { new CurveLoop() };
            tempHostProfile.First().Append(Line.CreateBound(points[0], points[1]));
            tempHostProfile.First().Append(Line.CreateBound(points[1], points[2]));
            tempHostProfile.First().Append(Line.CreateBound(points[2], points[3]));
            tempHostProfile.First().Append(Line.CreateBound(points[3], points[0]));
            ElementId floorTypeId = Floor.GetDefaultFloorType(doc, false);
            ElementId levelId = Level.GetNearestLevelId(doc, origin.Z);
#else
            CurveArray tempHostProfile = new CurveArray();
            tempHostProfile.Append(Line.CreateBound(points[0], points[1]));
            tempHostProfile.Append(Line.CreateBound(points[1], points[2]));
            tempHostProfile.Append(Line.CreateBound(points[2], points[3]));
            tempHostProfile.Append(Line.CreateBound(points[3], points[0]));
            FloorType floorType = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).FirstElement() as FloorType;
            double min = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().Min(lvl => lvl.Elevation - origin.Z);
            Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().Select(lvl => lvl.Elevation - origin.Z == min) as Level;
#endif

            tempFloorHostAngleData = GeometryTools.SummaryXYAngleData(movement.RD.Angle3, movement.RD.Angle2);
            double tokenX = 1;
            if (movement.RD.Angle3 < 0) tokenX = -1;
            double tokenY = 1;
            if (movement.RD.Angle2 < 0) tokenY = -1;
            Line slopeArrow = Line.CreateBound(origin, origin + XYZ.BasisX * Math.Cos(tempFloorHostAngleData.Last()) * tokenX + XYZ.BasisY * Math.Sin(tempFloorHostAngleData.Last()) * tokenY);
#if REVIT2023 || REVIT2024 || REVIT2025
            Floor tempFloorHost = Floor.Create(doc, tempHostProfile, floorTypeId, levelId, true, slopeArrow, 0);
#else
            Floor tempFloorHost = doc.Create.NewFloor(tempHostProfile, floorType, level, true);
#endif
            doc.Regenerate();

            tempRHCs.Add(new RebarHostCache(tempFloorHost));
        }

        /*private static void CreateTempHostAsFamilyInstance()
        {
            Solid sourceSolid = GeometryTools.GetSolid(sourceHost, false);
            XYZ origin = sourceSolid.ComputeCentroid();
            FamilySymbol tempSym = (from sym in new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>()
                                    where sym.FamilyName.StartsWith("322_Временный хост")
                                    select sym).FirstOrDefault();
            if (tempSym != null) tempHost = doc.Create.NewFamilyInstance(origin, tempSym, StructuralType.NonStructural);
        }*/

        private class DTNHadler : IDuplicateTypeNamesHandler
        {
            public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
            {
                return DuplicateTypeAction.UseDestinationTypes;
            }
        }

        /*public struct SketchData
        {
            public CurveArrArray CurveSets { get; set; }
            public Plane Plane { get; set; }
        }*/
    }
}
