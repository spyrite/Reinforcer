using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using RevitOSA.WallReinforcer.Tools;
using RevitOSA.WallReinforcer.Resources;

using ReinforcementData = RevitOSA.WallReinforcer.Resources.ReinforcementData;

#if REVIT2023 || REVIT2024 || REVIT2025
using static Autodesk.Revit.DB.SpecTypeId;
#endif

using Autodesk.Revit.DB.Structure;


namespace RevitOSA.WallReinforcer.Caching
{
    public class ReinforcementCache
    {
        //Поля
        private protected static Document doc;
        private protected List<object> preCalculations;

        //Конструкторы
        public ReinforcementCache(Element elem)
        {
            doc = elem.Document;
            Elem = elem;
            RClass = ReinfSettings.Default.reinf_RClass;
        }

        //Методы
        public void GetAnchors(GeometryCache geomCache)
        {
            // Инициализация
            Anchors = [];
            if (geomCache.Solid == null) geomCache.GetSolidData();
            double deep = 500 / 304.8;
            ElementFilter vFilter = new BoundingBoxIntersectsFilter(geomCache.Outline);
            List<RebarHostCache> analyzedBottomHostCaches = new List<RebarHostCache>();

            while (Anchors.Count == 0)
            {
                // Поиск нижележащих хостов с армированием
                List<RebarHostCache> bottomHostCaches = ExtractingTools.GetNearestHostCaches(geomCache, geomCache.Faces.Bottom, Side.Bottom, deep);
                bottomHostCaches = (from hostCache in bottomHostCaches
                                    where RebarHostTools.IsHostIsReinforced(hostCache.Elem)
                                    && !analyzedBottomHostCaches.Contains(hostCache)
                                    select hostCache).ToList();
                if (bottomHostCaches.Count == 0) break;

                // Поиск анкеров
                List<ElementId> rebarIds = new List<ElementId>();
                foreach (RebarHostCache hostCache in bottomHostCaches)
                {
                    if (!RebarHostData.IsValidHost(hostCache.Elem)) continue;
                    RebarHostData hostData = RebarHostData.GetRebarHostData(hostCache.Elem);

                    // Кэширование отдельных арматурных стержней
                    List<RebarCache> rebarCaches = (from rebar in hostData.GetRebarsInHost()
                                                    where vFilter.PassesFilter(hostCache.Elem)
                                                    select new RebarCache(rebar)).ToList();

                    // Кэширование стержней арматурных контейнеров
                    List<RebarContainerCache> containerCaches = (from container in hostData.GetRebarContainersInHost()
                                                                 where vFilter.PassesFilter(hostCache.Elem)
                                                                 select new RebarContainerCache(container)).ToList();
                    foreach (RebarContainerCache containerCache in containerCaches)
                        rebarCaches.AddRange(containerCache.RCICs);

                    // Фильтрация и сбор данных
                    foreach (RebarCache rebarCache in rebarCaches)
                    {
                        RebarCache.LineSetData data = rebarCache.GetLineSetsData().First();
                        List<ElementFilter> filters = new List<ElementFilter>
                        {
                            new BoundingBoxContainsPointFilter(data.Points.First()),
                            new BoundingBoxContainsPointFilter(data.Points.Last()),
                        };
                        ElementFilter filter = new LogicalOrFilter(filters);
                        if (filter.PassesFilter(hostCache.Elem))
                        {
                            RebarAnchorCache anchorCache = rebarCache as RebarAnchorCache;
                            anchorCache.AnchoredHostCache = hostCache;
                            Anchors.Add(anchorCache);
                        }
                    }
                }
                if (Anchors.Count > 0) break;

                // Продолжение цикла
                List<double> heights = (from hostCache in bottomHostCaches
                                        where hostCache.Geom.Dims.H > 0
                                        select hostCache.Geom.Dims.H).ToList();
                if (heights.Count > 0)
                {
                    deep += heights.Max();
                    analyzedBottomHostCaches.AddRange(bottomHostCaches);
                }
                else break;
            }
        }

        //Свойства
        public Element Elem { get; set; }
        public RebarHostCache HostCache { get; set; }
        public string ReinforcementFor { get; set; }
        public string RClass { get; set; }
        public List<ReinforcementData> DataPStirrupSet { get; set; }
        public List<RebarAnchorCache> Anchors { get; set; }
        protected CoverData CovData { get; set; }

        //Структуры, классы-структуры
        protected class CoverData
        {
            protected Item All { get; set; }
            protected Item Bot { get; set; }
            protected Item Top { get; set; }
            protected Item Oth { get; set; }
            protected Item Int { get; set; }
            protected Item Ext { get; set; }

            protected class Item
            {
                public double Dst { get; set; }
                public RebarCoverType CoverType { get; set; }
            }

        }

        //Свойства
        public ReinforcementData DataX { get; set; }
        public ReinforcementData DataY { get; set; }
    }
}
