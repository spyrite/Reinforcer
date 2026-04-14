using Autodesk.Revit.DB;
using RevitOSA.CoreMain.FB;
using RevitOSA.CoreMain.Assistants;
using System;
using System.Resources;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Line = Autodesk.Revit.DB.Line;
using Transform = Autodesk.Revit.DB.Transform;
using View = Autodesk.Revit.DB.View;
using Parameter = Autodesk.Revit.DB.Parameter;
using AnnSettings = RevitOSA.CoreSettings.Properties.Annotations;
using ReinfSettings = RevitOSA.CoreSettings.Properties.Reinforcement;
using ModelSettings = RevitOSA.CoreSettings.Properties.Modelling;
using Autodesk.Revit.DB.Architecture;
using RevitOSA.CoreSettings.ResourcesFP;


#if R2023 || R2024 || R2025
using static Autodesk.Revit.DB.SpecTypeId;
#endif

using Autodesk.Revit.DB.Structure;

#if COMPANY_FP
using RevitOSA.CoreSettings.ResourcesFP;
using static RevitOSA.CoreSettings.ResourcesFP.RevitParameters;

#elif COMPANY_FP
using static RevitOSA.CoreSettings.ResourcesOLP.RevitParameters;
using RevitOSA.CoreSettings.ResourcesOLP;

#else
#endif

namespace RevitOSA.CoreMain.Caching
{
    public class RebarHostCache
    {
        // Поля
        private protected Document doc;


        // Конструкторы
        private protected RebarHostCache() { }
        public RebarHostCache(Element elem)
        {
            doc = elem.Document;
            Elem = elem;
#if R2024 || R2025
            IntId = (int)elem.Id.Value;
#else
            IntId = elem.Id.IntegerValue;
#endif
            PhaseId = elem.get_Parameter(BuiltInParameter.PHASE_CREATED).AsElementId();
            if (elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue) HostMark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();

#if COMPANY_FP
            if (elem.get_Parameter(Guid.Parse(p_Identity_Zone)) != null) Zone = elem.get_Parameter(Guid.Parse(p_Identity_Zone)).AsString();
            if (elem.get_Parameter(Guid.Parse(p_Identity_Section)) != null) Section = elem.get_Parameter(Guid.Parse(p_Identity_Section)).AsString();
            if (ElementParametersAssistant.IsParameterExistAndHasValue(elem, pp_BClass)) BClass = elem.GetParameters(pp_BClass).First().AsString();
#endif


            if (RebarHostData.IsValidHost(elem)) ExistReinfData = RebarHostData.GetRebarHostData(elem);
            GetSheetSetNames();
            Geom = new GeometryCache(elem);
            PartitionIds = null;
            PartitionAttachments = null;
        }

        public Element Elem { get; set; }
        public int IntId { get; set; }
        public GeometryCache Geom { get; set; }
        public ReinforcementCache Reinf { get; set; }
        public string HostMark { get; set; }
        public ElementId PhaseId { get; set; }
        public string Zone { get; set; }
        public string Section { get; set; }
        public string BClass { get; set; }
        public AssemblyInstance AI { get; set; }
        public RebarHostData ExistReinfData { get; set; }
        public string SheetSetNames { get; set; }
        public List<ElementId> PartitionIds { get; set; }
        public List<Attachment> PartitionAttachments { get; set; }

        //public List<Attachment> LintelAttachments { get; set; }

        private void GetSheetSetNames()
        {
            if (Zone != null)
            {
#if COMPANY_FP
                ResourceSet zonesAndSheetSets = ElemZoneToSheetSet.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);
                foreach (DictionaryEntry entry in zonesAndSheetSets)
                {
                    bool condition1 = entry.Key.ToString() == Zone;
                    bool condition2 = entry.Key.ToString() == Zone.Split(new string[] { " №" }, StringSplitOptions.None).First();
                    bool condition3 = entry.Key.ToString() == Zone.Split(new string[] { "_№" }, StringSplitOptions.None).First();
                    if (condition1 || condition2 || condition3)
                    {
                        SheetSetNames = entry.Value.ToString();
                        break;
                    }
                }
#endif
                if (SheetSetNames == null) SheetSetNames = "КЖ;КЖ.И";
            }
        }
        public List<Element> GetAttachedRebarHosts()
        {
            List<Element> attHosts = new List<Element>();
            foreach (PlanarFace face in Geom.Faces.All)
            {
                List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
                Solid catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, face.FaceNormal, 10 / 304.8);
                ElementFilter filter1 = new ElementLevelFilter(Geom.LvlIds.Base);
                ElementFilter filter2 = new ElementIntersectsSolidFilter(catchSolid);
                ElementFilter filter = new LogicalAndFilter(filter1, filter2);
#if R2024 || R2025
                attHosts.AddRange(new FilteredElementCollector(doc).OfClass(Elem.GetType()).OfCategory(Elem.Category.BuiltInCategory).WherePasses(filter).ToElements().ToList());
#else
                attHosts.AddRange(new FilteredElementCollector(doc).OfClass(Elem.GetType()).OfCategory((BuiltInCategory)Elem.Category.Id.IntegerValue).WherePasses(filter).ToElements().ToList());
#endif
            }


            return attHosts;
        }
        public void GetPartitionIds(List<Document> docs)
        {
            Solid catchSolid = GeometryTools.GetScaledSolid(Elem, 20);
            PartitionIds = new List<ElementId>();
            foreach (Document doc in docs) PartitionIds.AddRange(new ExtractingTools.PartitionExtractor(doc, catchSolid).ToElementIds());
        }
        public void GetPartitionAttachments(List<Document> docs)
        {
            if (Geom.Faces.Sides != null && PartitionIds != null && PartitionIds.Count > 0)
            {
                PartitionAttachments = new List<Attachment>();
                foreach (PlanarFace face in Geom.Faces.Sides)
                {
                    List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
                    Solid catchSolid = null;
                    try { catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, face.FaceNormal, 50 / 304.8); }
                    catch { }
                    ;
                    if (catchSolid != null)
                    {
                        List<Element> partitions = new List<Element>();
                        foreach (Document doc in docs) partitions.AddRange(new ExtractingTools.PartitionExtractor(doc, PartitionIds, catchSolid).ToElements());
                        foreach (Element elem in partitions)
                        {
                            PartitionCache pCache = new PartitionCache(elem);
                            List<XYZ> checkPoints = new List<XYZ>()
                            {
                                pCache.Geom.Origins.CenterStartBottom - pCache.Geom.Dirs.X * 20 / 304.8 + pCache.Geom.Dirs.Z * 20/304.8,
                                pCache.Geom.Origins.CenterEndBottom + pCache.Geom.Dirs.X * 20 / 304.8 + pCache.Geom.Dirs.Z * 20 / 304.8
                            };
                            foreach (XYZ point in checkPoints)
                            {
                                ElementFilter filter = new BoundingBoxContainsPointFilter(point);
                                if (filter.PassesFilter(Elem))
                                {
                                    IntersectionResult proj = face.Project(point);
                                    if (proj != null)
                                    {
                                        Attachment att = new Attachment()
                                        {
                                            Origin = new XYZ(proj.XYZPoint.X, proj.XYZPoint.Y, pCache.Geom.Dims.ZBot),
                                            Face = face,
                                            FaceH = (from line in cls.First().Cast<Line>()
                                                     where line.Direction.Normalize().IsAlmostEqualTo(pCache.Geom.Dirs.Z) || (-line.Direction.Normalize()).IsAlmostEqualTo(pCache.Geom.Dirs.Z)
                                                     select line.Length).ToList().Max(),
                                            PCache = pCache
                                        };
                                        att.LintelOrigins = GetLintelOriginsInPartitionAttachment(att);
                                        PartitionAttachments.Add(att);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        /*public void GetLintelAttachments(List<Document> docs)
        {
            List<ElementId> allPartitonVoidIds = new List<ElementId>();
            
            foreach (ElementId partitionId in PartitionIds)
            {
                Document doc = GetElementIdDocument(docs, partitionId);
                List<ElementId> partitionVoidIds = doc.GetElement(partitionId).GetDependentElements(OpeningFilters.Union).ToList();
                if (partitionVoidIds.Count > 0) allPartitonVoidIds.AddRange(partitionVoidIds);
            }

            if (allPartitonVoidIds.Count > 0)
            {
                LintelAttachments = new List<Attachment>();
                foreach (PlanarFace face in Geom.Faces.Sides)
                {
                    List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
                    Solid catchSolid = null;
                    try { catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, face.FaceNormal.Normalize(), 250 / 304.8); }
                    catch { };
                    if (catchSolid != null)
                    {
                        ElementFilter filter = new ElementIntersectsSolidFilter(catchSolid);
                        List<Element> partitionVoids = new List<Element>();
                        foreach (Document doc in docs) partitionVoids.AddRange(new FilteredElementCollector(doc, allPartitonVoidIds).WherePasses(filter).ToElements().ToList());
                        if (partitionVoids.Count > 0)
                        {
                            foreach (Element elem in partitionVoids)
                            {
                                FamilyInstance partionVoid = elem as FamilyInstance;
                                VoidCache vCache = new VoidCache(partionVoid);

                                //TempGraphicTools.CreateLine(doc, vCache.Geom.Origins.CenterMiddleBottom, vCache.Geom.Origins.CenterMiddleTop, vCache.Geom.Dirs.Y);

                                IntersectionResult proj = face.Project(vCache.Geom.Origins.CenterMiddleTop);
                                if (proj != null)
                                {
                                    Attachment att = new Attachment()
                                    {
                                        Origin = proj.XYZPoint - vCache.Geom.Dirs.Z * 60 / 304.8,
                                        Face = face,
                                        PCache = new PartitionCache(vCache.Host)
                                    };
                                    LintelAttachments.Add(att);
                                }
                            }
                        }
                    }
                }
            }
        }*/

        public List<XYZ> GetPartitionCIOrigins(Attachment att)
        {
            List<XYZ> origins = new List<XYZ>();
            att.PCache.Geom.GetSolidData(doc);

            if (att.PCache.Geom.Solid != null)
            {
                double startZ = (Math.Round(att.PCache.Geom.Dims.ZBot * 304.8) + ReinfSettings.Default.ci_BottomAlign) / 304.8;// + Geom.BasePoint.Position.Z;
                double endZ = att.PCache.Geom.Dims.ZBot + att.FaceH;
                for (double z = startZ; z <= endZ; z += ReinfSettings.Default.ci_Step / 304.8)
                {
                    XYZ p0 = new XYZ(att.Origin.X, att.Origin.Y, z);
                    XYZ p1 = p0 + att.Face.FaceNormal * (ReinfSettings.Default.ci_SmallWallAccuracy + 20) / 304.8;
                    Line cutline = Line.CreateBound(p0, p1);

                    List<Curve> spotlines = att.PCache.Geom.Solid.IntersectWithCurve(cutline, null).ToList();
                    if (spotlines != null && spotlines.Count > 0)
                    {
                        Line spotline = spotlines.First() as Line;
                        if (Math.Round(spotline.Length * 304.8) >= ReinfSettings.Default.ci_SmallWallAccuracy) origins.Add(p0);
                    }
                }
            }
            return origins;
        }

        private List<XYZ> GetLintelOriginsInPartitionAttachment(Attachment att)
        {
            List<XYZ> lintelOrigins = new List<XYZ>();

            /*List<CurveLoop> cls = att.Face.GetEdgesAsCurveLoops().ToList();
            Solid catchSolid = null;
            try { catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, att.Face.FaceNormal.Normalize(), 250 / 304.8); }
            catch { };
            if (catchSolid != null)
            {
                ElementFilter filter = new ElementIntersectsSolidFilter(catchSolid);
                List<FamilyInstance> voids = att.PCache.Voids.FindAll(elem => filter.PassesFilter(elem)).Cast<FamilyInstance>().ToList();
                
                foreach (Element partitionVoid in voids)
                {
                    VoidCache vCache = new VoidCache(partitionVoid);
                    IntersectionResult proj = att.Face.Project(vCache.Geom.Origins.CenterMiddleTop);
                    if (proj != null) lintelOrigins.Add(proj.XYZPoint - vCache.Geom.Dirs.Z * 60 / 304.8);
                }
            }*/

            foreach (Element elem in att.PCache.Voids)
            {
                VoidCache vCache = new VoidCache(elem as FamilyInstance);
                List<XYZ> points = new List<XYZ> { vCache.Geom.Origins.CenterStartTop, vCache.Geom.Origins.CenterEndTop };
                foreach (XYZ point in points)
                {
                    IntersectionResult proj = att.Face.Project(point);
                    if (proj != null && Math.Round(proj.Distance * 304.8) <= 250)
                    {
                        lintelOrigins.Add(proj.XYZPoint - vCache.Geom.Dirs.Z * 60 / 304.8);
                        break;
                    }
                }
            }

            return lintelOrigins;
        }

        /*private Document GetElementIdDocument(List<Document> docs, ElementId id)
        {
            foreach (Document doc in docs) if (doc.GetElement(id) != null) return doc;
            return null;
        }*/
    }
}
