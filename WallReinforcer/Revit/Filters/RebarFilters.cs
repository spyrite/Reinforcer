using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Revit.Filters
{
    public static class RebarFilters
    {
        public static readonly ElementFilter All = new ElementMulticlassFilter(new List<Type> { typeof(Rebar), typeof(RebarContainer), typeof(AreaReinforcement) });
        public static readonly ElementFilter ARs = new ElementClassFilter(typeof(AreaReinforcement));
        public static readonly ElementFilter Containers = new ElementClassFilter(typeof(RebarContainer));
        public static readonly ElementFilter Rebars = new ElementClassFilter(typeof(Rebar));
        public static readonly ElementFilter RebarsInSystem = new ElementClassFilter(typeof(RebarInSystem));
        public static readonly ElementFilter RebarsExtended = new ElementMulticlassFilter(new List<Type> { typeof(Rebar), typeof(RebarInSystem) });
    }
}
