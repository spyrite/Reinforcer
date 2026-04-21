using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Resources;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Caching
{
    public class RebarAnchorCache : RebarCache
    {
        // Конструкторы
        public RebarAnchorCache(Rebar rebar, RebarHostCache anchoredHostCache) : base(rebar)
        {
            AnchoredHostCache = anchoredHostCache;
            GetAnchorPartsData();
        }

        public RebarAnchorCache(Document doc, RebarContainerItem rci, RebarHostCache anchoredHostCache) : base(doc, rci)
        {
            AnchoredHostCache = anchoredHostCache;
            GetAnchorPartsData();
        }

        // Методы
        private void GetAnchorPartsData()
        {
            if (AnchoredHostCache != null)
            {
                if (AnchoredHostCache.Geom.Solid == null) AnchoredHostCache.Geom.GetSolidData();

                AnchorPartDatas = new List<AnchorPartData>();
                foreach (List<Curve> lines in CenterLineSets)
                {
                    AnchorPartData data = new AnchorPartData();
                    data.Lines = new List<Curve>();
                    foreach (Curve cutLine in PrimaryData.CenterLines)
                    {
                        List<Curve> spotLines = AnchoredHostCache.Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                        if (spotLines.Count > 0) data.Lines.AddRange(spotLines);
                    }
                    data.Length = data.Lines.Sum(line => line.Length);

                    List<XYZ> points = (from line in data.Lines
                                        select line.GetEndPoint(0)).ToList();
                    points.Add((from line in data.Lines
                                select line.GetEndPoint(1)).Last());
                    points = points.OrderBy(point => point, new Sorting.XYZCoordsComparer()).ToList();
                    data.BottomPoint = points.First();
                    data.TopPoint = points.Last();
                    AnchorPartDatas.Add(data);
                }
            }
        }

        // Свойства
        public RebarHostCache AnchoredHostCache { get; set; }
        public List<AnchorPartData> AnchorPartDatas { get; set; }

    }
}
