using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Revit.Filters
{
    public static class AnnotationFilters
    {
        public static readonly ElementFilter AllForView = new ElementMulticlassFilter(
        [
            typeof(FamilyInstance), 
            typeof(Dimension), 
            typeof(TextNote), 
            typeof(IndependentTag) 
        ]
        );
    }
}
