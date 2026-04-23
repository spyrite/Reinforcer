using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using FilterTreeControlWPF;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Resources1P;
using RevitOSA.WallReinforcer.Revit.Filters;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using DocSettings = RevitOSA.WallReinforcer.Properties.Docs;
using Document = Autodesk.Revit.DB.Document;
using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;

namespace RevitOSA.WallReinforcer.Revit
{
    public class PerformWallReinforce : IExternalCommand
    {
        public static UIApplication UIApp;
        public static UIDocument UIDoc;
        public static Autodesk.Revit.ApplicationServices.Application App;
        public static Document Doc;

        private List<ElementId> _selectedElementIds;
        private List<WallCache> _wCaches;

        private int _worksetIntId = -1;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Инициализация
            UIApp = commandData.Application;
            UIDoc = commandData.Application.ActiveUIDocument;
            App = commandData.Application.Application;
            Doc = commandData.Application.ActiveUIDocument.Document;
            _selectedElementIds = [.. UIDoc.Selection.GetElementIds()];
            _wCaches = [];

            //Выбор стен, сбор данных
            _wCaches = [.. _selectedElementIds.Select(id => Doc.GetElement(id)).Where(elem => elem is Wall).Select(elem => new WallCache(elem))];

            if (!_wCaches.Any())
                try
                {
                    _wCaches = [.. UIDoc.Selection.PickObjects(ObjectType.Element, new WallSelectionFilter(),
                        "Выберите армируемые стены").Select(r => new WallCache(Doc.GetElement(r)))];
                }
                catch { return Result.Cancelled; }

            if (Doc.IsWorkshared) _worksetIntId = WorksetAssistant.GetOrCreateUserWorksetIntId(Doc, DocSettings.Default.reinf_Walls_WorksetName);

            //Поиск арматурных выпусков
            _wCaches.ForEach(c => c.Reinf?.GetAnchors(c.Geom));

            //Анализ, деление на регионы, пересечения, определение концов
            foreach (WallCache wCache in _wCaches.Where(c => !c.Reinf.Anchors.Any())) wCache.AnalyzeForSubCaches();

            using (Transaction tx = new(Doc, "RevitOSA: Армирование стен"))
            {
                tx.Start();

                //Обновление защитного слоя
                _wCaches.ForEach(c => RebarCoverAssistant.SetRebarCoversToHost(c.Wall, Doc,
                    [0,0,0,ReinfSettings.Default.reinf_RebarCover_Edge,
                    ReinfSettings.Default.reinf_Walls_Y_Edge_CenterAlign - c.Reinf.DataX.D,
                    ReinfSettings.Default.reinf_Walls_Y_Edge_CenterAlign - c.Reinf.DataX.D]));
                
                //Армирование без выпусков



                //Армирование c выпусками



                tx.Commit();
            }




            return Result.Succeeded;
        }

        private void CreateVerticalRebarsOnIntersectionsWithColumns(WallCache wCache, double offset, double tolerance)
        {
            if (wCache.Geom.Dims.H >= 1000/304.8)
            {
                foreach (WallCache.IntersectionCache inter in wCache.Intersections)
                {
                    inter.AnalyzeForUpperElems();
                    double topAnc = inter.UpperSlabCaches.Max(c => c.Geom.Dims.T)
                        + (inter.UpperWallCaches.Cast<RebarHostCache>().Union(inter.UpperColumnCaches.Cast<RebarHostCache>()).Any()
                        ? ReinforcementTools.ComputeAorOVLength(wCache.Reinf.DataY.D, wCache.BClass, wCache.Reinf.RClass, AnchorMode.AnchorCompress)
                        : 0);

                    topAnc = topAnc - topAnc > 0 ? 0 : ReinfSettings.Default.reinf_RebarCover_Edge / 304.8;
                    double botOv = (Math.Floor(ReinforcementTools.ComputeAorOVLength(wCache.Reinf.DataY.D, wCache.BClass, wCache.Reinf.RClass, AnchorMode.OverlapCompress)) 
                        * 1.3 * 304.8 / 10) * 10 / 304.8;

                    List<List<int>> coeffs = [[-1, -1, 0], [-1, 1, 1], [1, 1, 0], [1, -1, 1]];
                    Element attachedElement = inter.GetAttachedRebarHosts().FirstOrDefault();
                    RebarHostCache attachmentCache = null;
                    switch (attachedElement)
                    {
                        case Wall:
                             attachmentCache = new WallCache(attachedElement as Wall);
                            break;
                        case FamilyInstance when attachedElement.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns:
                            attachmentCache = new ColumnCache(attachedElement as FamilyInstance);
                            break;
                        default:  continue;
                    }

                    List<Rebar> vRebars = [];
                    for (int i = 0; i < 4; i++)
                    {
                        XYZ startPoint = inter.Geom.Origins.CenterMiddleBottom + wCache.Geom.Dirs.X * (offset / 304.8) * coeffs[i][0] - wCache.Geom.Dirs.Y * (offset / 304.8) * coeffs[i][1];
                        if (!startPoint.IsPointNearHostEdge(wCache.Geom, attachmentCache.Geom, offset, tolerance))
                        {
                            XYZ p0 = startPoint + XYZ.BasisZ * botOv * coeffs[i][2];
                            XYZ p1 = startPoint + XYZ.BasisZ * (wCache.Geom.Dims.H + topAnc);
                            Line vLine = Line.CreateBound(p0, p1);

                            Rebar vRebar = Rebar.CreateFromCurves(Doc, RebarStyle.Standard, wCache.Reinf.DataY.BarType,
                                null, null, wCache.Elem, wCache.Geom.Dirs.Y, [vLine], RebarHookOrientation.Left, RebarHookOrientation.Left, true, false);
                            vRebar.SetVerticalRebarConstarints(wCache.Geom.Faces, attachmentCache.Geom.Faces, wCache.Geom.Dirs.X, attachmentCache.Geom.Dirs.X,
                                wCache.Reinf.DataX.D, wCache.Reinf.DataY.D, attachmentCache.Reinf.DataY.D);
                            SetParameters(vRebar, ReinforcementPartitionNames.reinfPartName_VertCorner, wCache.PhaseId);
                            vRebars.Add(vRebar);

                            if (vRebars.Count == 2 || vRebars.Count == 4)
                            {
                                Rebar stirrup = SetStirrup(vRebars[i - 1], vRebars[i], wCache);
                                stirrup.SetStirrupConstarints(new Tuple<Rebar, Rebar>(vRebars[i - 1], vRebars[i]), wCache.Geom.Dims.T);
                                SetParameters(stirrup, ReinforcementPartitionNames.reinfPartName_PStirrups, wCache.PhaseId);
                            }
                        }
                    }
                }
            }
        }

        private Rebar SetStirrup(Rebar rebar0, Rebar rebar1, RebarHostCache hostCache)
        {
            Rebar stirrup = null;
            XYZ p0 = rebar0.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0)[0].GetEndPoint(1);
            XYZ p1 = rebar1.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0)[0].GetEndPoint(1);

            double dst = rebar0.GetRebarConstraintsManager().GetCurrentConstraintOnHandle(new RebarHandles(rebar0).Top).GetDistanceToTargetHostFace();

            if (Math.Round(p0.Z * 304.8) == Math.Round(p1.Z * 304.8)
                    & dst < ReinforcementTools.ComputeBaseAnchorLength(hostCache.Reinf.DataY.D, hostCache.BClass, "A500"))
            {
                XYZ xDir = rebar0.GetShapeDrivenAccessor().Normal.Normalize();
                List<Curve> lines = 
                    [
                        Line.CreateBound(p0 - XYZ.BasisZ * (hostCache.Geom.Dims.T * 2 + hostCache.Reinf.DataY.D / 2) 
                        - xDir * hostCache.Reinf.DataY.D, p0 - XYZ.BasisZ * hostCache.Reinf.DataY.D / 2 - xDir * hostCache.Reinf.DataY.D),

                        Line.CreateBound(p0 - XYZ.BasisZ * hostCache.Reinf.DataY.D / 2 - xDir * hostCache.Reinf.DataY.D, p1 
                        - XYZ.BasisZ * hostCache.Reinf.DataY.D / 2 - xDir * hostCache.Reinf.DataY.D),

                        Line.CreateBound(p1 - XYZ.BasisZ * hostCache.Reinf.DataY.D / 2 - xDir * hostCache.Reinf.DataY.D, p1 
                        - XYZ.BasisZ * (hostCache.Geom.Dims.T * 2 + hostCache.Reinf.DataY.D / 2) - xDir * hostCache.Reinf.DataY.D)
                    ];

                stirrup = Rebar.CreateFromCurvesAndShape(Doc, GetRebarShape(ReinfSettings.Default.reinf_Shape_Stirrup_IntId), hostCache.Reinf.DataY.BarType, null, null,
                    hostCache.Elem, xDir, lines, RebarHookOrientation.Left, RebarHookOrientation.Left);
                stirrup.GetShapeDrivenAccessor().SetLayoutAsSingle();
            }
            return stirrup;
        }

        /// <summary>
        /// Вызывать только при открытой транзакции
        /// </summary>
        private void SetParameters(Element elem, string partitionName, ElementId phaseId)
        {
            if (Doc.IsWorkshared) elem.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM)?.Set(_worksetIntId);
            elem.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM).Set(partitionName);
            elem.get_Parameter(BuiltInParameter.PHASE_CREATED).Set(phaseId);
        }

        private static RebarShape GetRebarShape(int intId)
        {
            Element elem = Doc.GetElement(new ElementId(intId));
            return elem is RebarShape ? elem as RebarShape : null;
        }
    }
}
