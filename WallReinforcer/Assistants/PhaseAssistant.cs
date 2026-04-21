using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Assistants
{
    public static class PhaseAssistant
    {
        private static Document doc;
        private static List<ElementId> allPhaseIds;
        private static List<double> phaseLevelZs;
        private static object elemZ;
        private static List<Parameter> phasePars;

        public static bool IsInitialized = false;

        public static void SetElemPhaseByLevel(Element elem)
        {
            if (!IsInitialized) Initialize(elem.Document);
            elemZ = GeometryTools.GetElementZValue(elem);

            if (elemZ != null)
            {
                GetPhaseParameters(elem);
                ElementId phaseId = null;
                if ((double)elemZ <= phaseLevelZs.First()) phaseId = allPhaseIds.First();
                else if ((double)elemZ > phaseLevelZs.Last()) phaseId = allPhaseIds.Last();
                else
                {
                    for (int i = 0; i < allPhaseIds.Count - 1; i++)
                    {
                        double botLevelZ = phaseLevelZs[i];
                        double topLevelZ = phaseLevelZs[i + 1];
                        if (i < allPhaseIds.Count() && (botLevelZ <= (double)elemZ & (double)elemZ < topLevelZ))
                        {
                            phaseId = allPhaseIds[i];
                            break;
                        }
                    }
                }
                foreach (Parameter phasePar in phasePars) if (phasePar != null && phaseId != null && !phasePar.IsReadOnly) phasePar.Set(phaseId);
            }
        }

        public static void Initialize(Document currentDoc)
        {
            doc = currentDoc;

            allPhaseIds = new FilteredElementCollector(doc).OfClass(typeof(Phase)).ToElementIds().ToList();
            allPhaseIds = allPhaseIds.OrderBy(id => (doc.GetElement(id) as Phase).get_Parameter(BuiltInParameter.PHASE_SEQUENCE_NUMBER).AsInteger()).ToList();
            List<string> allPhaseNames = (from id in allPhaseIds
                                          select (doc.GetElement(id) as Phase).Name).ToList();
            phaseLevelZs = (from lvl in new FilteredElementCollector(doc).OfClass(typeof(Level))
                            where allPhaseNames.Contains(lvl.Name)
                            select Math.Round((lvl as Level).Elevation * 304.8)).ToList();
            phaseLevelZs = phaseLevelZs.Distinct().ToList();
            phaseLevelZs.Sort();
            phaseLevelZs = (from z in phaseLevelZs
                            select z / 304.8).ToList();
            IsInitialized = true;
        }

        private static void GetPhaseParameters(Element elem)
        {
            phasePars = new List<Parameter>();
            if (elem is Group)
            {
                phasePars = (from id in (elem as Group).GetMemberIds()
                             where !doc.GetElement(id).get_Parameter(BuiltInParameter.PHASE_CREATED).IsReadOnly
                             select doc.GetElement(id).get_Parameter(BuiltInParameter.PHASE_CREATED)).ToList();
            }
            else if (elem is FamilyInstance)
            {
                FamilyInstance parentFI = elem as FamilyInstance;
                while (parentFI != null)
                {
                    parentFI = (elem as FamilyInstance).SuperComponent as FamilyInstance;
                    if (parentFI != null) elem = parentFI;
                }
            }
            if (phasePars.Count == 0 && elem != null) phasePars.Add(elem.get_Parameter(BuiltInParameter.PHASE_CREATED));
        }
    }
}
