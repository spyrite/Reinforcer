using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Assistants
{
    public static class RebarCoverAssistant
    {
        private static readonly List<BuiltInParameter> parameterNames = new List<BuiltInParameter>()
            {
                BuiltInParameter.CLEAR_COVER,
                BuiltInParameter.CLEAR_COVER_BOTTOM,
                BuiltInParameter.CLEAR_COVER_TOP,
                BuiltInParameter.CLEAR_COVER_OTHER,
                BuiltInParameter.CLEAR_COVER_INTERIOR,
                BuiltInParameter.CLEAR_COVER_EXTERIOR
            };

        public static ElementId GetOrCreateRebarCoverIdByDistance(Document doc, double dst)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(RebarCoverType));
            foreach (RebarCoverType cover in collector.Cast<RebarCoverType>())
            {
                if (Math.Round(cover.CoverDistance * 304.8) == dst) return cover.Id;
            }
            RebarCoverType newCover = null;
            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Создание нового типа защитного слоя");
                newCover = RebarCoverType.Create(doc, ((int)Math.Round(dst)).ToString(), dst / 304.8);
                tx.Commit();
            }
            return newCover.Id;
        }
        public static List<ElementId> GetHostRebarCoverIds(Element elem)
        {
            List<ElementId> coverIds = new List<ElementId>();
            foreach (BuiltInParameter parameterName in parameterNames)
            {
                Parameter coverParameter = elem.get_Parameter(parameterName);
                if (coverParameter != null && !coverParameter.IsReadOnly) coverIds.Add(coverParameter.AsElementId());
                else coverIds.Add(null);
            }
            return coverIds;
        }
        public static void SetRebarCoversToHost(Element elem, List<ElementId> coverIds)
        {
            for (int i = 0; i < parameterNames.Count; i++)
            {
                Parameter coverParameter = elem.get_Parameter(parameterNames[i]);
                if (coverParameter != null && !coverParameter.IsReadOnly && coverIds[i] != null) coverParameter.Set(coverIds[i]);
            }
        }
        public static void SetRebarCoversToHost(Element elem, Document doc, List<double> dsts)
        {
            for (int i = 0; i < parameterNames.Count; i++)
            {
                Parameter coverParameter = elem.get_Parameter(parameterNames[i]);
                if (coverParameter != null && !coverParameter.IsReadOnly)
                {
                    ElementId coverId = GetOrCreateRebarCoverIdByDistance(doc, dsts[i]);
                    coverParameter.Set(coverId);
                }
            }
        }
        public static void SetRebarCoversToHost(Element elem, Document doc, double dst)
        {
            for (int i = 0; i < parameterNames.Count; i++)
            {
                Parameter coverParameter = elem.get_Parameter(parameterNames[i]);
                if (coverParameter != null && !coverParameter.IsReadOnly)
                {
                    ElementId coverId = GetOrCreateRebarCoverIdByDistance(doc, dst);
                    coverParameter.Set(coverId);
                }
            }
        }
        public static void RebarCoversToZero(Element elem)
        {
            if (RebarHostData.IsValidHost(elem))
            {
                Document doc = elem.Document;
                RebarHostData hostData = RebarHostData.GetRebarHostData(elem);
                RebarCoverType rebarCoverType = doc.GetElement(GetOrCreateRebarCoverIdByDistance(doc, 0)) as RebarCoverType;
                hostData.SetCommonCoverType(rebarCoverType);
            }
        }

    }
}
