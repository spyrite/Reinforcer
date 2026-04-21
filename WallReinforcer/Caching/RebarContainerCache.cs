using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

namespace RevitOSA.WallReinforcer.Caching
{
    public class RebarContainerCache
    {
        // Поля
        private protected Document doc;

        // Конструкторы
        public RebarContainerCache(RebarContainer container)
        {
            doc = container.Document;
            Elem = container;
            ElemType = (RebarContainerType)doc.GetElement(container.GetTypeId());
            ElemTypeName = ElemType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME).AsString();
            BBox = container.get_BoundingBox(doc.ActiveView);
            Transform = BBox.Transform;
            Origin = Transform.OfPoint(BBox.Min);
            Center = Line.CreateBound(Transform.OfPoint(BBox.Min), Transform.OfPoint(BBox.Max)).Evaluate(0.5, true);
            PhaseId = container.get_Parameter(BuiltInParameter.PHASE_CREATED).AsElementId();
            Host = doc.GetElement(container.GetHostId());
            if (ElemType.get_Parameter(Guid.Parse(p_Dims_InRunningMeters)) != null
                && ElemType.get_Parameter(Guid.Parse(p_Dims_InRunningMeters)).AsInteger() == 1) InMeters = true;
            else InMeters = false;
            ATMark = null;

            RCICs = new List<RebarCache>();
            for (int i = 0; i < container.ItemsCount; i++)
            {
                RCICs.Add(new RebarCache(doc, container.GetItem(i)));
            }
            ;

            if (container.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue && container.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString() != "")
                Mark = container.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
            else Mark = null;

            if (container.AssemblyInstanceId != ElementId.InvalidElementId) AIName = doc.GetElement(container.AssemblyInstanceId).Name;
            else AIName = null;

            if (container.get_Parameter(new Guid(p_Identity_Zone)) != null) Zone = container.get_Parameter(new Guid(p_Identity_Zone)).AsString();
            else Zone = null;
        }
        public DirectShape CreateHost(Document doc, Category cat, string name, XYZ origin)
        {
            Solid hostSolid = GeometryTools.CreateSolidFromBBox(BBox, origin);
            DirectShape host = DirectShape.CreateElement(doc, cat.Id);
            host.SetShape(new List<GeometryObject> { hostSolid });
            host.SetName(name);
            doc.Regenerate();
            RebarCoverAssistant.SetRebarCoversToHost(host, doc, 0);
            host.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(name);
            return host;
        }
        public double CalculateMass()
        {
            double volume = 0;
            if (InMeters)
            {
                foreach (RebarCache rp in RCICs)
                {
                    if (rp.PrimaryData.InRunningMeters) volume += rp.Volume / rp.Ltot * (1000 / 304.8);
                    else if (rp.Rule != RebarLayoutRule.Single) volume += rp.Volume / rp.PrimaryData.N * Math.Round((1000 / 304.8) / rp.PrimaryData.Step);
                    else volume += rp.Volume;
                }
            }
            else foreach (RebarCache rp in RCICs) { volume += rp.Volume; }
            ;
            double mass = 7850 * (volume * Math.Pow(304.8, 3)) / Math.Pow(10, 9);
            return mass;
        }
        public double GetAmount()
        {
            double amount = 1 / (double)Elem.ItemsCount;
            if (InMeters) foreach (RebarCache rp in RCICs) { if (rp.PrimaryData.InRunningMeters) amount = Math.Round(rp.Ltot * 304.8) / 1000 / (double)Elem.ItemsCount; break; }
            ;
            return amount;
        }
        public string GetPartitionName()
        {
            bool condition1 = Host is FamilyInstance;
            bool condition2 = false;
            bool condition3 = false;
            if (condition1)
            {
                condition2 = (Host as FamilyInstance).Symbol.get_Parameter(Guid.Parse(p_Dims_Width)) != null;
                condition3 = (Host as FamilyInstance).Symbol.get_Parameter(Guid.Parse(p_Dims_Height)) != null;
            }
            string suffix = "";
            if (condition1 & condition2 & condition3)
            {
                string hostB = (Host as FamilyInstance).Symbol.get_Parameter(Guid.Parse(p_Dims_Width)).AsValueString();
                string hostH = (Host as FamilyInstance).Symbol.get_Parameter(Guid.Parse(p_Dims_Height)).AsValueString();
                suffix = "_" + hostB + "x" + hostH + "(h)";
            }
            return "03_Каркасы" + suffix;
        }

        public RebarContainer Elem { get; private set; }
        public RebarContainerType ElemType { get; private set; }
        public string ElemTypeName { get; private set; }
        public BoundingBoxXYZ BBox { get; private set; }
        public Transform Transform { get; private set; }
        public XYZ Origin { get; private set; }
        public XYZ Center { get; private set; }
        public ElementId PhaseId { get; private set; }
        public Element Host { get; set; }
        public string ATMark { get; set; }
        public List<RebarCache> RCICs { get; private set; }
        public string Mark { get; private set; }
        public string AIName { get; private set; }
        public bool InMeters { get; }
        public string Zone { get; private set; }


    }
}
