using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct FamilyParameterData
    {
        public FamilyParameter Parameter { get; set; }
        public string NewName { get; set; }
        public bool IsInstance { get; set; }
        public ExternalDefinition ExternalDefinition { get; set; }
        public string SourceName;
        public Guid Guid;
        public string GuidAsString;
#if REVIT2023 || REVIT2024||REVIT2025
        public ForgeTypeId GroupTypeId;
        public ForgeTypeId SpecTypeId;
#else
        public BuiltInParameterGroup GroupTypeId;
        public ParameterType SpecTypeId;
#endif
        public List<Parameter> AssociatedParameters;
    }
}
