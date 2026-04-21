using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Caching
{
    public class SlabCache : RebarHostCache
    {
        // Конструкторы
        public SlabCache(Element elem) : base(elem)
        {
            Slab = elem as Floor;
            Geom = new GeometrySlabCache(Slab);
            SupportLines = new SupportLinesData
            {
                WallsBottom = new List<Curve>(),
                WallsTop = new List<Curve>(),
                ColumnsBottom = new List<Curve>(),
                ColumnsTop = new List<Curve>()
            };
        }

        // Методы
        public void GetSupportLines(GeometryCache geomCache, Side side)
        {
            Transform transform1;
            Transform transform2 = null;
            Line cutLine = null;
            switch (side)
            {
                case Side.Bottom:
                    transform1 = Transform.CreateTranslation(-geomCache.Dirs.Z * 10 / 304.8);
                    transform2 = Transform.CreateTranslation(geomCache.Dirs.Z * 10 / 304.8);
                    cutLine = geomCache.Lines.CenterBot.CreateTransformed(transform1) as Line;
                    break;
                case Side.Top:
                    transform1 = Transform.CreateTranslation(geomCache.Dirs.Z * 10 / 304.8);
                    transform2 = Transform.CreateTranslation(-geomCache.Dirs.Z * 10 / 304.8);
                    cutLine = geomCache.Lines.CenterTop.CreateTransformed(transform1) as Line;
                    break;
            }
            if (cutLine != null && transform2 == null)
            {
                if (Geom.Solid != null) Geom.GetSolidData();
                List<Curve> spotLines = Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                foreach (Curve spotLine in spotLines)
                {
                    Curve supportLine = spotLine.CreateTransformed(transform2);
                    switch (side)
                    {
                        case Side.Bottom: SupportLines.AllBottom.Add(supportLine); break;
                        case Side.Top: SupportLines.AllTop.Add(supportLine); break;
                    }
                    if (geomCache is GeometryWallCache)
                        switch (side)
                        {
                            case Side.Bottom: SupportLines.WallsBottom.Add(supportLine); break;
                            case Side.Top: SupportLines.WallsTop.Add(supportLine); break;
                        }
                    else if (geomCache is GeometryColumnCache)
                        switch (side)
                        {
                            case Side.Bottom: SupportLines.ColumnsBottom.Add(supportLine); break;
                            case Side.Top: SupportLines.ColumnsTop.Add(supportLine); break;
                        }
                }
            }
        }

        // Структуры
        public struct SupportLinesData
        {
            public List<Curve> WallsBottom;
            public List<Curve> WallsTop;
            public List<Curve> ColumnsBottom;
            public List<Curve> ColumnsTop;
            public List<Curve> AllBottom;
            public List<Curve> AllTop;
        }

        // Свойства
        public Floor Slab { get; set; }
        public SupportLinesData SupportLines { get; set; }
    }
}
