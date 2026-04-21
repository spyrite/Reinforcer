using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Revit.Filters
{
    public static class VoidFilters
    {
        public static readonly ElementFilter Windows = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_Windows));
        public static readonly ElementFilter Doors = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_Doors));
        public static readonly ElementFilter Union = new LogicalOrFilter(new List<ElementFilter> { Windows, Doors });
    }
}
