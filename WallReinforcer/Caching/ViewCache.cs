using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Resources1P;
using RevitOSA.WallReinforcer.Revit.Filters;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;

namespace RevitOSA.WallReinforcer.Caching
{
    public class ViewCache
    {
        private readonly Document doc;
        private string[] nameParts;

        public View View { get; }
        public string Name { get; }
        public string ElemMark { get; }
        public GeometryCache.Directions Dirs { get; }
        public CropBoxCache CropBox { get; }
        public int Scale { get; }
        public ViewCollectors Collectors { get; set; }
        public string ViewportSheetName { get; set; }
        public Element MainElement { get; set; }
        public ViewFunctionalType FunctionalType { get; private set; }
        public List<ElementId> AnnotationIds { get; set; }

        public ViewCache(View view)
        {
            View = view;
            doc = View.Document;
            Name = View.Name;
            ElemMark = GetMainElementMark();
            Dirs = new GeometryCache.Directions
            {
                X = view.RightDirection,
                Y = view.UpDirection,
                Z = view.ViewDirection
            };
            CropBox = new CropBoxCache(View.CropBox, Dirs);
            Scale = View.Scale;
            //UpdateViewCollectors();
            UpdateViewportSheetName();
            IdentifyViewFunctionalType();
        }

        private string GetMainElementMark()
        {
            ResourceSet emDescriptions = ElemMarkDescriptions.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);
            foreach (string namePart in Name.Split(new string[] { "_", " ", ", " }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (DictionaryEntry emDescription in emDescriptions)
                {
                    string prefix = emDescription.Key.ToString();
                    if (namePart.Split('-').First() == prefix) return namePart;
                }
            }
            return "";
        }
        private void SetMainElementByMark()
        {
            if (Collectors == null) UpdateViewCollectors();
            foreach (Element elem in Collectors.Union)
            {
                string mark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString();
                if (mark == ElemMark) MainElement = elem;
            }
        }
        private void IdentifyViewFunctionalType()
        {
            FunctionalType = ViewFunctionalType.Unknow;
            nameParts = Name.Split('_');
            foreach (string namePart in nameParts)
            {
                TrySetViewFunctionalType(namePart);
                if (FunctionalType != ViewFunctionalType.Unknow) break;
                else
                {
                    foreach (string subNamePart in namePart.Split(' '))
                    {
                        TrySetViewFunctionalType(subNamePart);
                        if (FunctionalType != ViewFunctionalType.Unknow) break;
                    }
                    if (FunctionalType != ViewFunctionalType.Unknow) break;
                }
            }
        }
        private void TrySetViewFunctionalType(string sourceString)
        {
            if (ViewNameStrings.GeneralArrangements.Contains(sourceString)
                && !ViewNameStrings.Sections.Contains(sourceString)
                && !ViewNameStrings.Details.Contains(sourceString)) FunctionalType = ViewFunctionalType.GeneralArrangement;
            else if (ViewNameStrings.GeneralArrangements.Contains(sourceString)
                && ViewNameStrings.Sections.Contains(sourceString)) FunctionalType = ViewFunctionalType.GeneralArrangementSection;
            else if (ViewNameStrings.GeneralArrangements.Contains(sourceString)
                && ViewNameStrings.Details.Contains(sourceString)) FunctionalType = ViewFunctionalType.GeneralArrangementDetail;

            else if (ViewNameStrings.Formworks.Contains(sourceString)
                && !ViewNameStrings.Sections.Contains(sourceString)
                && !ViewNameStrings.Details.Contains(sourceString)) FunctionalType = ViewFunctionalType.Formwork;
            else if (ViewNameStrings.Formworks.Contains(sourceString)
                && ViewNameStrings.Sections.Contains(sourceString)) FunctionalType = ViewFunctionalType.FormworkSection;
            else if (ViewNameStrings.Formworks.Contains(sourceString)
                && ViewNameStrings.Details.Contains(sourceString)) FunctionalType = ViewFunctionalType.FormworkDetail;

            else if (ViewNameStrings.Reinforcement.Contains(sourceString)
                && !ViewNameStrings.Sections.Contains(sourceString)
                && !ViewNameStrings.Details.Contains(sourceString)) FunctionalType = ViewFunctionalType.Reinforcement;
            else if (ViewNameStrings.Reinforcement.Contains(sourceString)
                && ViewNameStrings.Sections.Contains(sourceString)) FunctionalType = ViewFunctionalType.ReinforcementSection;
            else if (ViewNameStrings.Reinforcement.Contains(sourceString)
                && ViewNameStrings.Details.Contains(sourceString)) FunctionalType = ViewFunctionalType.ReinforcementDetail;
        }

        public bool ShouldHideGridBubbles()
        {
            bool condition1 = !Dirs.Z.IsAlmostEqualTo(XYZ.BasisZ) && !Dirs.Z.IsAlmostEqualTo(-XYZ.BasisZ);
            bool condition2 = !Name.Contains("рмирован");
            if (condition1 && condition2) return true;
            else return false;
        }
        public void UpdateViewportSheetName()
        {
            Parameter vpSheetNameParameter = View.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NAME);
            if (vpSheetNameParameter.HasValue) ViewportSheetName = vpSheetNameParameter.AsString();
        }
        public void UpdateViewCollectors() { Collectors = new ViewCollectors(View); }
        public void SetMainElementFromView()
        {
            SetMainElementByMark();
            if (MainElement == null)
            {
                Options optForElems = new Options();
                List<List<double>> linearParameterPairs = new List<List<double>>()
                {
                new List<double> { 0, 0 },
                new List<double> { -0.25, 0 },
                new List<double> { 0.25, 0 },
                new List<double> { 0, -0.25 },
                new List<double> { 0, 0.25 }
                };
                foreach (Element elem in Collectors.Union)
                {
                    Solid solid = GeometryTools.GetSolid(elem, true);
                    List<Element> voids = (from elemId in elem.GetDependentElements(VoidFilters.Union) select doc.GetElement(elemId)).ToList();

                    if (solid != null || voids.Count > 0)
                    {
                        foreach (List<double> i in linearParameterPairs)
                        {
                            Transform transform = Transform.CreateTranslation(Dirs.X * CropBox.Dims.L * i.First() + Dirs.Y * CropBox.Dims.H * i.Last());
                            Curve line = CropBox.Lines.CentroidDepth.CreateTransformed(transform);

                            List<ElementFilter> filters = new List<ElementFilter>();
                            for (int j = 0; j < 10; j++) filters.Add(new BoundingBoxContainsPointFilter(line.Evaluate(j * 0.1, true)));
                            ElementFilter filter = new LogicalOrFilter(filters);

                            if (solid.IntersectWithCurve(line, null).Count() > 0
                                || (from voi in voids where filter.PassesFilter(voi) select voi).ToList().Count() > 0) MainElement = elem;
                        }
                    }
                }
            }

        }
        public void GetAnnotationIds()
        {
            AnnotationIds = View.GetDependentElements(AnnotationFilters.AllForView).ToList();
        }

        public class ViewCollectors
        {
            private readonly Document doc;
            internal FilteredElementCollector Floors { get; }
            internal FilteredElementCollector Columns { get; }
            internal FilteredElementCollector Walls { get; }
            internal FilteredElementCollector Beams { get; }
            internal FilteredElementCollector Stairs { get; }
            internal FilteredElementCollector Foundation { get; }
            internal FilteredElementCollector GenericModel { get; }
            internal FilteredElementCollector Union { get; }
            internal FilteredElementCollector Rebars { get; }
            internal FilteredElementCollector Containers { get; }
            internal FilteredElementCollector Grids { get; }

            internal ViewCollectors(View view)
            {
                doc = view.Document;
                Floors = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Floors);
                Columns = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Columns);
                Walls = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Walls);
                Beams = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Beams);
                Stairs = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Stairs);
                Foundation = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Foundations);
                GenericModel = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.GMs);
                Union = new FilteredElementCollector(doc, view.Id).WherePasses(StructureElementFilters.Union);
                Rebars = new FilteredElementCollector(doc, view.Id).WherePasses(RebarFilters.RebarsExtended);
                Containers = new FilteredElementCollector(doc, view.Id).WherePasses(RebarFilters.Containers);
                Grids = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Grid));
            }
        }

        private static class ViewNameStrings
        {
            internal static readonly List<string> GeneralArrangements = new List<string>
                {
                    "План",
                    "Схема расположения",
                    "схема",
                    "план",
                    "схема расположения",
                    "схема"
                };
            internal static readonly List<string> Sections = new List<string>
                {
                    "Разрезы",
                    "Разрез",
                    "Сечения",
                    "Сечение",
                    "Виды",
                    "Вид",
                    "разрезы",
                    "разрез",
                    "сечения",
                    "сечение",
                    "виды",
                    "вид"
                };
            internal static readonly List<string> Details = new List<string>
                {
                    "Узлы",
                    "Узел",
                    "Фрагменты",
                    "фрагменты",
                    "Обрамление",
                    "обрамление"
                };
            internal static readonly List<string> Formworks = new List<string>
                {
                    "Опалубочные чертежи",
                    "Опалубочный чертеж",
                    "Опалубка",
                    "опалубочные чертежи",
                    "опалубочный чертеж",
                    "опалубка"
                };
            internal static readonly List<string> Reinforcement = new List<string>
                {
                    "Схемы армирования",
                    "Схема армирования",
                    "Армирование",
                    "схемы армирования",
                    "схема армирования",
                    "армирование"
                };
        };
    }
}
