using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using System.Collections.Generic;
using System.Linq;

using DocSettings = RevitOSA.WallReinforcer.Properties.Docs;

namespace RevitOSA.WallReinforcer.Assistants
{
    public static class WorksetAssistant
    {
        public static readonly Dictionary<WorksetStructureSuffix, string> StructureSuffixes = new Dictionary<WorksetStructureSuffix, string>
        {
            {WorksetStructureSuffix.reinf, "Армирование" },
            {WorksetStructureSuffix.cis, "Закладные" },
            {WorksetStructureSuffix.ais, "Сборки" },
            {WorksetStructureSuffix.anchors, "Выпуски" },
            {WorksetStructureSuffix.steel, "Металл" },
            {WorksetStructureSuffix.piles, "Сваи" },
            {WorksetStructureSuffix.concrete, "Бетон" }
        };

        public static int GetOrCreateUserWorksetBySurnameIntId(Document doc, string name)
        {
            if (!string.IsNullOrEmpty(DocSettings.Default.user_Surname))
            {
                Workset userWorkset;
                FilteredWorksetCollector collector = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset);
                userWorkset = (from workset in collector where workset.Name == name select workset).FirstOrDefault();
                if (userWorkset == null) userWorkset = Workset.Create(doc, name);
                return userWorkset.Id.IntegerValue;
            }
            else return -1;
        }

        public static int GetOrCreateUserWorksetIntId(Document doc, string name)
        {
            Workset userWorkset = (from workset in new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset)
                                   where workset.Name == name
                                   select workset).FirstOrDefault();
            if (userWorkset == null) userWorkset = Workset.Create(doc, name);
            return userWorkset.Id.IntegerValue;
        }

        public static void DeterminateWorksetForElement(Document doc, Element elem)
        {

        }
    }
}
