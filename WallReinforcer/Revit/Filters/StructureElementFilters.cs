using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Revit.Filters
{
    public static class StructureElementFilters
    {
        public static readonly ElementFilter Floors = new LogicalAndFilter(new LogicalOrFilter(new ElementClassFilter(typeof(Floor)), new ElementClassFilter(typeof(FamilyInstance))), new ElementCategoryFilter(BuiltInCategory.OST_Floors));
        public static readonly ElementFilter Columns = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns));
        public static readonly ElementFilter Walls = new ElementClassFilter(typeof(Wall));
        public static readonly ElementFilter ColumnsOrWalls = new LogicalOrFilter(Columns, Walls);
        public static readonly ElementFilter Beams = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming));
        public static readonly ElementFilter Stairs = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_Stairs));
        public static readonly ElementFilter Foundations = new LogicalAndFilter(new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation), new LogicalOrFilter(new ElementClassFilter(typeof(Floor)), new ElementClassFilter(typeof(FamilyInstance))));
        public static readonly ElementFilter GMs = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_GenericModel));
        public static readonly ElementFilter Railings = new LogicalAndFilter(new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_StairsRailing));
        public static readonly ElementFilter Union = new LogicalOrFilter(new List<ElementFilter> { Floors, Columns, Walls, Beams, Stairs, Foundations, Railings, GMs });
    }
}
