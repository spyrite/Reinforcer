using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Resources1P;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;

namespace RevitOSA.WallReinforcer.Caching
{
    public class ViewSheetCache
    {
        private readonly Document doc;
        private static readonly ResourceSet elemMarkDesrictions = ElemMarkDescriptions.ResourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

        public ViewSheet Sheet { get; }
        public string SheetSetMark { get; set; }
        public string Name { get; set; }
        public string SheetNumber { get; set; }
        public Dimensions Dims { get; set; }
        public List<Viewport> Viewports { get; set; }
        public ViewCacheLists VCs { get; set; }
        public List<ScheduleSheetInstance> SSIs { get; set; }
        public List<ScheduleCache> SCs { get; set; }
        public AssemblyInstance AI { get; set; }
        public List<string> ElemMarks { get; set; }
        public string ElemMarksString { get; set; }

        public struct Dimensions
        {
            public double W;
            public double H;
        }
        public struct ViewCacheLists
        {
            public List<ViewCache> All;
            public List<ViewCache> Formworks;
        }

        public ViewSheetCache(ViewSheet sheet)
        {
            doc = sheet.Document;
            Sheet = sheet;
            if (Sheet.get_Parameter(new System.Guid(p_TitleBlock_SheetSet)) != null)
                SheetSetMark = Sheet.get_Parameter(new System.Guid(p_TitleBlock_SheetSet)).AsString();
            else
                SheetSetMark = null;
            Name = Sheet.Name;
            UpdateOwnedViews();
            UpdateOwnedSchedules();
            ElemMarks = (from vc in VCs.All select vc.ElemMark).Distinct().ToList();

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Sheet, p_TitleBlock_SheetNumber))
                SheetNumber = Sheet.get_Parameter(new Guid(p_TitleBlock_SheetNumber)).AsString();
            else
                SheetNumber = Sheet.SheetNumber.Split('-').Last();
        }

        public void UpdateOwnedViews()
        {
            Viewports = (from id in Sheet.GetAllViewports()
                         select doc.GetElement(id) as Viewport).ToList();

            List<ViewCache> vcs = (from viewport in Viewports select new ViewCache(doc.GetElement(viewport.ViewId) as View)).ToList();
            VCs = new ViewCacheLists
            {
                All = vcs,
                Formworks = (from vc in vcs
                             where vc.FunctionalType == ViewFunctionalType.Formwork
                             select vc).OrderBy(ElemMarkNs).ToList()
            };

        }
        public void UpdateSheetName()
        {
            ElemMarks = (from vc in VCs.Formworks select vc.ElemMark).ToList();
            if (ElemMarks.Count() > 0)
            {
                ElemMarksString = string.Join(", ", ElemMarks);
                string[] prefixes = elemMarkDesrictions.GetString(ElemMarks[0].Split('-')[0]).Split(';');
                if (ElemMarks.Count() == 1) Name = prefixes[2] + " " + ElemMarks[0];
                else Name = prefixes[3] + " " + ElemMarksString;

                if (SheetNameAllowed(Name)) Sheet.Name = Name;
                else Name = Sheet.Name;
            }
        }
        public void UpdateOwnedSchedules()
        {
            /**SSIs = (from elem in new FilteredElementCollector(doc, Sheet.Id).OfClass(typeof(ScheduleSheetInstance))
                                                where !(elem as ScheduleSheetInstance).IsTitleblockRevisionSchedule
                                                select elem as ScheduleSheetInstance).ToList();*/
            SSIs = (from id in Sheet.GetDependentElements(new ElementClassFilter(typeof(ScheduleSheetInstance)))
                    where !(doc.GetElement(id) as ScheduleSheetInstance).IsTitleblockRevisionSchedule
                    select doc.GetElement(id) as ScheduleSheetInstance).ToList();

            SCs = (from ssi in SSIs
                   select new ScheduleCache(doc.GetElement(ssi.ScheduleId) as ViewSchedule)).ToList();
            SCs = SCs.Distinct().ToList();
        }
        public void UpdateDims()
        {
            Dims = new Dimensions
            {
                W = Sheet.Outline.Max.U - Sheet.Outline.Min.U,
                H = Sheet.Outline.Max.V - Sheet.Outline.Min.V
            };
        }

        private double ElemMarkNs(ViewCache vc)
        {
            double ns = 0;
            if (vc.ElemMark != null
                && vc.ElemMark.Split('-').Count() == 2)
            {
                string[] markNumberParts = vc.ElemMark.Split('-').Last().Split('.');
                for (int i = 0; i < markNumberParts.Count(); i++)
                    if (int.TryParse(markNumberParts[i], out int N)) ns += N * Math.Pow(0.01, i);
            }
            return ns;
        }
        private bool SheetNameAllowed(string name)
        {
            List<string> existSheetNames = (from sheet in new FilteredElementCollector(doc).OfClass(typeof(ViewSheet))
                                            select sheet.Name).ToList();
            if (existSheetNames.Contains(name)) return false;
            else return true;
        }
    }
}
