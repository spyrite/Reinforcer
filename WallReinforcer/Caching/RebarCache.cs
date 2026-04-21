using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using RevitOSA.WallReinforcer.Resources;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;
using ReinforcementData = RevitOSA.WallReinforcer.Resources.ReinforcementData;

namespace RevitOSA.WallReinforcer.Caching
{
    public class RebarCache
    {
        // Поля
        private protected Document doc;

        // Конструкторы
        public RebarCache(Rebar rebar)
        {
            this.doc = rebar.Document;
            Rebar = rebar;
            Host = doc.GetElement(rebar.GetHostId());
            GetPrimaryData(rebar);

            // Данные по параметрам
            Partition = rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM).AsString();
            PhaseId = Host.get_Parameter(BuiltInParameter.PHASE_CREATED).AsElementId();

            Normal = rebar.GetShapeDrivenAccessor().Normal;
            Rule = rebar.LayoutRule;
            HookTypes = new List<RebarHookType> { (RebarHookType)doc.GetElement(rebar.GetHookTypeId(0)), (RebarHookType)doc.GetElement(rebar.GetHookTypeId(1)) };
            HookOrients = new List<RebarHookOrientation> { rebar.GetHookOrientation(0), rebar.GetHookOrientation(1) };
#if REVIT2023 || REVIT2024 || REVIT2025
            HookAngles = new List<double> { rebar.GetHookRotationAngle(0), rebar.GetHookRotationAngle(1) };
#else
            HookAngles = new List<double>
            {
                rebar.get_Parameter(BuiltInParameter.REBAR_HOOK_ANGLE).AsDouble(),
                rebar.get_Parameter(BuiltInParameter.REBAR_HOOK_ANGLE).AsDouble()
            };
#endif

            Shape = (RebarShape)doc.GetElement(rebar.GetShapeId());
            if (Shape.SimpleLine && HookTypes[0] == null && HookTypes[1] == null) IsSimple = true;
            else IsSimple = false;
            Style = Shape.RebarStyle;

            // Данные по сборкам
            if (rebar.AssemblyInstanceId != ElementId.InvalidElementId)
            {
                AIName = doc.GetElement(rebar.AssemblyInstanceId).Name;
                ParentId = rebar.AssemblyInstanceId;
                ATMark = doc.GetElement(doc.GetElement(rebar.AssemblyInstanceId).GetTypeId()).get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
            }
            else
            {
                AIName = null;
                ParentId = Host.Id;
                ATMark = null;
            }

            CenterLineSets = new List<List<Curve>>();
#if REVIT2024 || REVIT2025
            for (int i = 0; i < rebar.Quantity; i++) CenterLineSets.Add(
                rebar.GetTransformedCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, i).ToList());
#else
            for (int i = 0; i < rebar.Quantity; i++) CenterLineSets.Add(
                rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, i).ToList());
#endif
        }
        public RebarCache(Document doc, RebarContainerItem rci)
        {
            this.doc = doc;
            RCI = rci;
            GetPrimaryData(rci);

            Normal = rci.Normal;
            Rule = rci.LayoutRule;
            HookTypes = new List<RebarHookType> { (RebarHookType)doc.GetElement(rci.GetHookTypeId(0)), (RebarHookType)doc.GetElement(rci.GetHookTypeId(1)) };
            HookOrients = new List<RebarHookOrientation> { rci.GetHookOrientation(0), rci.GetHookOrientation(1) };
            Shape = (RebarShape)doc.GetElement(rci.RebarShapeId);
            Style = Shape.RebarStyle;

            Ltot = PrimaryData.CenterLines.Sum(line => line.Length);
            Volume = rci.Volume;
        }

        // Методы
        public void MatchLayoutRuleFromRebar(RebarContainerItem targetRCI, int n)
        {
            if (Rebar != null)
            {
                if (Rule == RebarLayoutRule.Single | n == 1) targetRCI.SetLayoutAsSingle();
                else
                {
                    double step = Rebar.MaxSpacing;
                    double L = (n - 1) * step;
                    bool side = Rebar.GetShapeDrivenAccessor().BarsOnNormalSide;
                    List<bool> includings = new List<bool> { Rebar.IncludeFirstBar, Rebar.IncludeLastBar };
                    switch (Rule)
                    {
                        case RebarLayoutRule.FixedNumber:
                            targetRCI.SetLayoutAsFixedNumber(n, L, side, includings[0], includings[1]);
                            break;
                        case RebarLayoutRule.MaximumSpacing:
                            targetRCI.SetLayoutAsMaximumSpacing(step, L, side, includings[0], includings[1]);
                            break;
                        case RebarLayoutRule.NumberWithSpacing:
                            targetRCI.SetLayoutAsNumberWithSpacing(n, step, side, includings[0], includings[1]);
                            break;
                    }
                }
                ;
            }
        }
        public void MatchLayoutRuleFromRCI(Rebar targetRebar)
        {
            if (RCI != null)
            {
                if (Rule == RebarLayoutRule.Single) targetRebar.GetShapeDrivenAccessor().SetLayoutAsSingle();
                else
                {
                    int n = RCI.Quantity;
                    double L = RCI.ArrayLength;
                    double step = RCI.MaxSpacing;
                    bool side = RCI.BarsOnNormalSide;
                    List<bool> includings = new List<bool> { RCI.IncludeFirstBar, RCI.IncludeLastBar };

                    if (Rule == RebarLayoutRule.FixedNumber || n == 1)
                        targetRebar.GetShapeDrivenAccessor().SetLayoutAsFixedNumber(n, L, side, includings[0], includings[1]);
                    else if (Rule == RebarLayoutRule.MaximumSpacing)
                        targetRebar.GetShapeDrivenAccessor().SetLayoutAsMaximumSpacing(step, L, side, includings[0], includings[1]);
                    else if (Rule == RebarLayoutRule.NumberWithSpacing && 1 <= PrimaryData.N && PrimaryData.N <= 1002)
                        targetRebar.GetShapeDrivenAccessor().SetLayoutAsNumberWithSpacing(n, step, side, includings[0], includings[1]);
                }
            }
        }
        public void MatchLayoutRuleFromRCI(RebarContainerItem targetRCI)
        {
            if (RCI != null)
            {
                if (Rule == RebarLayoutRule.Single) targetRCI.SetLayoutAsSingle();
                else
                {
                    int n = RCI.Quantity;
                    double L = RCI.ArrayLength;
                    double step = RCI.MaxSpacing;
                    bool side = RCI.BarsOnNormalSide;
                    List<bool> includings = new List<bool> { RCI.IncludeFirstBar, RCI.IncludeLastBar };

                    switch (Rule)
                    {
                        case RebarLayoutRule.FixedNumber:
                            targetRCI.SetLayoutAsFixedNumber(n, L, side, includings[0], includings[1]);
                            break;
                        case RebarLayoutRule.MaximumSpacing:
                            targetRCI.SetLayoutAsMaximumSpacing(step, L, side, includings[0], includings[1]);
                            break;
                        case RebarLayoutRule.NumberWithSpacing:
                            targetRCI.SetLayoutAsNumberWithSpacing(n, step, side, includings[0], includings[1]);
                            break;
                    }
                }
            }
        }

        public List<LineSetData> GetLineSetsData()
        {
            Rebar rebar = Rebar;
#if REVIT2024 || REVIT2025
            XYZ startOrigin = rebar.GetMovedBarTransform(0).Origin;
            IList<Curve> startLines = rebar.GetTransformedCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
#else
            IList<Curve> startLines = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0);
            XYZ startOrigin = startLines.First().GetEndPoint(0);
#endif
            List<LineSetData> data = new List<LineSetData> { new LineSetData(startLines, startOrigin) };
            Transform negativeStepTransform = Transform.CreateTranslation(-PrimaryData.Dir * rebar.MaxSpacing);


            if (rebar.Quantity > 1)
            {
                for (int i = 1; i < rebar.Quantity; i++)
                {
                    if (rebar.DoesBarExistAtPosition(i))
                    {
#if REVIT2024 || REVIT2025
                        XYZ currentOrigin = rebar.GetMovedBarTransform(i).Origin;
#else
                        XYZ currentOrigin = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, i).First().GetEndPoint(0);
#endif
                        bool condition1 = currentOrigin.IsAlmostEqualTo(data[data.Count() - 1].Origin);
                        bool condition2 = currentOrigin.IsAlmostEqualTo(data[data.Count() - 1].Origin + PrimaryData.Dir * rebar.MaxSpacing * data[data.Count() - 1].N);
                        bool condition3 = currentOrigin.IsAlmostEqualTo(data[data.Count() - 1].Origin - PrimaryData.Dir * rebar.MaxSpacing * 2 * data[data.Count() - 1].N);
                        if (condition1 | condition2 | condition3)
                        {
                            data[data.Count - 1].N += 1;
                            if (condition3)
                            {
                                IList<Curve> lines = new List<Curve>();
                                foreach (Curve line in data[data.Count - 1].Curves)
                                {
                                    line.CreateTransformed(negativeStepTransform);
                                    lines.Add(line);
                                }
                                data[data.Count - 1].Curves = lines;
                            }
                        }
#if REVIT2024 || REVIT2025
                        else data.Add(new LineSetData(rebar.GetTransformedCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, i), currentOrigin));
#else
                        else data.Add(new LineSetData(rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, i), currentOrigin));
#endif
                    }
                }
            }
            return data;
        }
        public bool HasMovedBar()
        {
            Rebar rebar = Rebar;
            for (int i = 0; i < rebar.Quantity; i++)
            {
#if REVIT2024 || REVIT2025
                XYZ barOrigin = rebar.GetMovedBarTransform(i).Origin;
#else
                XYZ barOrigin = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, i).First().GetEndPoint(0);
#endif
                bool check = rebar.DoesBarExistAtPosition(i);
                if (!barOrigin.IsAlmostEqualTo(new XYZ()) | !check) return true;
            }
            return false;
        }

        public Face GetNearestFace(List<PlanarFace> faces, int i)
        {
            XYZ dir = faces.First().FaceNormal.Normalize();
            Curve line = PrimaryData.CenterLines.First();
            List<double> dsts = [.. faces.Select(f => Math.Abs(line.GetEndPoint(i).DotProduct(dir) - f.Origin.DotProduct(dir)))];
            return dsts.Any() ? faces[dsts.IndexOf(dsts.Min())] : null;
        }

        private void GetPrimaryData(Rebar rebar)
        {
            ReinforcementData data = new ReinforcementData
            {
                BarType = (RebarBarType)doc.GetElement(rebar.GetTypeId()),
                Dir = rebar.GetShapeDrivenAccessor().GetDistributionPath().Direction,
#if REVIT2024 || REVIT2025
                CenterLines = rebar.GetTransformedCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0).ToList(),
#else
                CenterLines = rebar.GetCenterlineCurves(false, true, true, MultiplanarOption.IncludeOnlyPlanarCurves, 0).ToList(),
#endif
            };

#if REVIT2023 || REVIT2024 || REVIT2025
            data.D = data.BarType.BarNominalDiameter;
#else
            data.D = data.BarType.BarDiameter;
#endif

            if (data.BarType.get_Parameter(Guid.Parse(p_Dims_InRunningMeters)) != null
                && data.BarType.get_Parameter(Guid.Parse(p_Dims_InRunningMeters)).AsInteger() == 1)
                data.InRunningMeters = true;
            else
                data.InRunningMeters = false;
            PrimaryData = data;
        }
        private void GetPrimaryData(RebarContainerItem rci)
        {
            ReinforcementData data = new ReinforcementData
            {
                BarType = (RebarBarType)doc.GetElement(rci.BarTypeId),
                Dir = rci.GetDistributionPath().Direction,
                CenterLines = rci.GetCenterlineCurves(false, true, true).ToList(),
                N = rci.Quantity,
            };

#if REVIT2023 || REVIT2024 || REVIT2025
            data.D = data.BarType.BarNominalDiameter;
#else
            data.D = data.BarType.BarDiameter;
#endif

            if (data.BarType.get_Parameter(Guid.Parse(p_Dims_InRunningMeters)) != null
                && data.BarType.get_Parameter(Guid.Parse(p_Dims_InRunningMeters)).AsInteger() == 1)
                data.InRunningMeters = true;
            else
                data.InRunningMeters = false;

            if (Rule != RebarLayoutRule.Single) data.Step = rci.MaxSpacing;
            else data.Step = 0;
            PrimaryData = data;
        }

        // Свойства
        public Rebar Rebar { get; private set; }
        public RebarContainerItem RCI { get; private set; }
        public string Partition { get; private set; }
        public XYZ Normal { get; private set; }
        public RebarLayoutRule Rule { get; private set; }
        public List<RebarHookType> HookTypes { get; private set; }
        public List<RebarHookOrientation> HookOrients { get; private set; }
        public List<double> HookAngles { get; private set; }
        public RebarShape Shape { get; private set; }
        public bool IsSimple { get; private set; }
        public RebarStyle Style { get; private set; }
        public Element Host { get; private set; }
        public ElementId PhaseId { get; private set; }
        public string AIName { get; private set; }
        public ElementId ParentId { get; private set; }
        public string ATMark { get; private set; }
        public List<List<Curve>> CenterLineSets { get; private set; }
        public ReinforcementData PrimaryData { get; private set; }
        public double Ltot { get; }
        public double Volume { get; }

        // Подклассы
        public class LineSetData
        {
            public LineSetData(IList<Curve> curves, XYZ origin)
            {
                Curves = curves;
                N = 1;
                Origin = origin;

                Points = (from curve in curves
                          where curve is Line
                          select (curve as Line).GetEndPoint(0)).ToList();
                Points.Add((from curve in curves
                            where curve is Line
                            select (curve as Line).GetEndPoint(1)).Last());
            }
            public IList<Curve> Curves { get; set; }
            public int N { get; set; }
            public XYZ Origin { get; set; }
            public List<XYZ> Points { get; set; }
        }
    }
}
