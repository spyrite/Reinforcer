using Autodesk.Revit.DB;

namespace RevitOSA.WallReinforcer.Resources
{
    public struct LintelElementData
    {
        public string ScheduleName { get; set; }
        public string TypeName { get; set; }
        public FamilySymbol Type { get; set; }
        public double L { get; set; }
        public double LsupportMin { get; set; }
        public double LsupportLeft { get; set; }
        public double LsupportRight { get; set; }
        public double H { get; set; }
        public double T { get; set; }
        public double MaxVoidWidth1 { get; set; }
        public double MaxVoidWidth2 { get; set; }
        public CrossSectionType Section { get; set; }
        public string MaterialName { get; set; }
    }
}
