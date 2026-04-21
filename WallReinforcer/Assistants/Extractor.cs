using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Revit.Filters;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Assistants
{
    public class Extractor
    {
        private protected Document doc;
        private protected List<Element> elems;
        private protected List<ElementId> elemIds;
        private protected FilteredElementCollector collector;
        private protected ElementFilter defaultFilter;

        public Extractor() { }
        public Extractor(Document doc)
        {
            Initialize(doc);
            collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
        }
        public Extractor(Document doc, ElementId viewId)
        {
            Initialize(doc);
            collector = new FilteredElementCollector(doc, viewId).WhereElementIsNotElementType();
        }
        public Extractor(Document doc, List<ElementId> elemSetIds)
        {
            Initialize(doc);
            collector = new FilteredElementCollector(doc, elemSetIds).WhereElementIsNotElementType();
        }
        public Extractor(Document doc, AssemblyInstance ai)
        {
            Initialize(doc);
            collector = new FilteredElementCollector(doc, ai.GetMemberIds()).WhereElementIsNotElementType();
        }
        public Extractor(Document doc, Solid solid)
        {
            Initialize(doc);
            ElementFilter filter = new ElementIntersectsSolidFilter(solid);
            collector = new FilteredElementCollector(doc).WherePasses(filter).WhereElementIsNotElementType();
        }
        public Extractor(Document doc, List<ElementId> elemSetIds, Solid solid)
        {
            Initialize(doc);
            ElementFilter filter = new ElementIntersectsSolidFilter(solid);
            collector = new FilteredElementCollector(doc, elemSetIds).WherePasses(filter).WhereElementIsNotElementType();
        }

        private protected void Initialize(Document doc)
        {
            this.doc = doc;
            defaultFilter = StructureElementFilters.Union;
            elems = new List<Element>();
            elemIds = new List<ElementId>();
        }
        private protected void SetDefaultFilter(ElementFilter filter)
        {
            defaultFilter = filter;
            collector.WherePasses(defaultFilter);
        }
        private protected List<string> GetMaterialNames(Element elem)
        {
            List<string> materialNames = new List<string>();
            if (elem is HostObject)
            {
                HostObjAttributes elemType = doc.GetElement(elem.GetTypeId()) as HostObjAttributes;
                if (elemType is WallType && (elemType as WallType).Kind != WallKind.Curtain || elemType is FloorType)
                {
                    CompoundStructure cs = elemType.GetCompoundStructure();
                    foreach (CompoundStructureLayer layer in cs.GetLayers())
                    {
                        Material material = doc.GetElement(layer.MaterialId) as Material;
                        materialNames.Add(material.Name);
                    }
                }
            }
            else if (elem is FamilyInstance)
            {
                Parameter materialParameter3 = null;
                if ((elem as FamilyInstance).Symbol.GetParameters("1П_Материал").Count > 0) materialParameter3 = (elem as FamilyInstance).Symbol.GetParameters("1П_Материал")[0];
                Parameter materialParameter4 = null;
                if (elem.GetParameters("1П_Материал").Count > 0) materialParameter4 = elem.GetParameters("1П_Материал")[0];
                List<Parameter> materialParameters = new List<Parameter>()
                    {
                        (elem as FamilyInstance).Symbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM),
                        (elem as FamilyInstance).get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM),
                        materialParameter3,
                        materialParameter4
                    };
                foreach (Parameter materialParameter in materialParameters)
                {
                    if (materialParameter != null && materialParameter.AsElementId() != ElementId.InvalidElementId)
                    {
                        ElementId materialId = materialParameter.AsElementId();
                        Material material = doc.GetElement(materialId) as Material;
                        materialNames.Add(material.Name);
                        break;
                    }
                }
            }
            return materialNames;
        }

        public List<Element> ToElements() { return elems; }
        public List<ElementId> ToElementIds() { return elemIds; }
    }
}
