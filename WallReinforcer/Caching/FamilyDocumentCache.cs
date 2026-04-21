using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using RevitOSA.WallReinforcer.Resources1P;
using System.Collections.Generic;
using System.Linq;

using DocsSettings = RevitOSA.WallReinforcer.Properties.Docs;

namespace RevitOSA.WallReinforcer.Caching
{
    public class FamilyDocumentCache
    {
        public Document Doc { get; set; }
        public List<Document> OwnerFamilyDocs { get; set; }
        public List<FamilyDocumentCache> SubComponentFamilyDocsCache { get; set; }
        public List<FamilyParameterData> SharedFamilyParameterDatas { get; set; }
        public Dictionary<string, string> ResharedFamilyParameterNames { get; set; }
        public FamilyStructureClassification StructureClassification { get; set; }

        public FamilyDocumentCache(Document doc)
        {
            if (doc.IsFamilyDocument)
            {
                Doc = doc;
                GetOwnerFamilyDocs();
                GetSharedFamilyParameterDatas();

                if (DocsSettings.Default.families_stuctClass_DeterminateAuto) GetFamilyStructureClassification();
                else StructureClassification = (FamilyStructureClassification)DocsSettings.Default.families_stuctClass_AddStandardParametersFor;
            }
        }

        public void GetSubComponentFamilyDocsCache()
        {
            List<Document> familyDocs = new List<Document>() { Doc };
            List<Family> families;
            Document fDoc;
            int i = 0;
            while (i < familyDocs.Count)
            {
                fDoc = familyDocs[i];
                families = (from fam in new FilteredElementCollector(fDoc).OfClass(typeof(Family)).Cast<Family>()
                            where fam.IsEditable
                            select fam).ToList();
                familyDocs.AddRange((from fam in families
                                     select fDoc.EditFamily(fam)).ToList());
                i++;
            }
            SubComponentFamilyDocsCache = new List<FamilyDocumentCache>();
            for (i = 1; i < familyDocs.Count; i++)
            {
                FamilyDocumentCache fDocCache = new FamilyDocumentCache(familyDocs[i]);
                SubComponentFamilyDocsCache.Add(fDocCache);
            }
        }

        private void GetOwnerFamilyDocs()
        {
            OwnerFamilyDocs = new List<Document>();

        }

        private void GetSharedFamilyParameterDatas()
        {
            SharedFamilyParameterDatas = new List<FamilyParameterData>();
            FamilyManager fm = Doc.FamilyManager;
            foreach (FamilyParameter par in fm.Parameters)
                if (par.IsShared)
                {
                    FamilyParameterData familyParameterData = new FamilyParameterData
                    {
                        Parameter = par,
                        SourceName = par.Definition.Name,
                        GuidAsString = par.GUID.ToString(),
#if REVIT2023 || REVIT2024||REVIT2025
                        GroupTypeId = par.Definition.GetGroupTypeId(),
#else
                        GroupTypeId = par.Definition.ParameterGroup,
#endif
                        IsInstance = par.IsInstance,
                        AssociatedParameters = par.AssociatedParameters.Cast<Parameter>().ToList()
                    };
                    SharedFamilyParameterDatas.Add(familyParameterData);
                }
        }

        private void GetFamilyStructureClassification()
        {
            StructureClassification = FamilyStructureClassification.Unknow;
            string prefix = Doc.Title.Split('_').FirstOrDefault();
            if (prefix != null)
            {
                if (FamilyNamePrefixes.structure_RebarShape.Contains(prefix)) StructureClassification = FamilyStructureClassification.RebarShape;
                else if (FamilyNamePrefixes.structure_SteelDetail.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelDetail;
                else if (FamilyNamePrefixes.structure_SteelUnit.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelUnit;
                else if (FamilyNamePrefixes.structure_Fabric.Contains(prefix)) StructureClassification = FamilyStructureClassification.Fabric;
                //else if (FamilyNamePrefixes.structure_CI.Contains(prefix)) StructureClassification = FamilyStructureClassification.ConcreteInsert;
                else if (FamilyNamePrefixes.structure_Void.Contains(prefix)) StructureClassification = FamilyStructureClassification.Void;

#if COMPANY_OLP
                else if (FamilyNamePrefixes.structure_ConcreteDetail.Contains(prefix)) StructureClassification = FamilyStructureClassification.ConcreteDetail;
                else if (FamilyNamePrefixes.structure_RebarIFC.Contains(prefix)) StructureClassification = FamilyStructureClassification.RebarIFC;
                else if (FamilyNamePrefixes.structure_SteelBeam.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelBeam;
                else if (FamilyNamePrefixes.structure_SteelColumn.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelColumn;
                else if (FamilyNamePrefixes.structure_SteelConnection.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelConnection;
                else if (FamilyNamePrefixes.structure_SteelConnector.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelConnector;
                else if (FamilyNamePrefixes.structure_SteelDetail_Child.Contains(prefix)) StructureClassification = FamilyStructureClassification.SteelDetail_Child;
#endif
            }
        }
    }
}
