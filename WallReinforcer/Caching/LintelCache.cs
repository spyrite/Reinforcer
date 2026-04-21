using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Resources;
using System;
using System.Collections.Generic;
using System.Linq;

using RevitOSA.WallReinforcer.Resources1P;
using RevitOSA.WallReinforcer.Tools;
using RevitOSA.WallReinforcer.Revit.Filters;

namespace RevitOSA.WallReinforcer.Caching
{
    public class LintelCache
    {
        //Поля
        private protected Document doc;
        private protected static List<RevitLinkInstance> links;
        private protected LintelElementData lintelElementData;

        public static FilteredElementCollector linkCollector;

        //Конструкторы

        public LintelCache(VoidCache voidCache)
        {
            doc = voidCache.Elem.Document;
            if (linkCollector == null)
            {
                linkCollector = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance));
                foreach (string sheetSetName in new List<string> { "КЖ" })
                {
                    links = (from link in linkCollector
                             where (link.Name.Contains(sheetSetName) && (link as RevitLinkInstance).GetLinkDocument() != null)
                             select link as RevitLinkInstance).ToList();
                    if (links != null && links.Count > 0) break;
                }
            }

            VoidCache = voidCache;
            WCache = new WallCache((VoidCache.Elem as FamilyInstance).Host);

            CornerElementsData = new LintelCornerElementsData
            {
                PlatesAlign = 5 / 304.8,
                PlatesStep = 400 / 304.8,
                PlatesN = (int)Math.Floor((VoidCache.Geom.Dims.B - 80 / 304.8) / (400 / 304.8)) + 1,
                PlateL = WCache.Geom.Dims.T - 10 / 304.8,
                PlateB = 80 / 304.8,
                PlateT = 4 / 304.8
            };

            ElementsPlacementData = new Dictionary<string, double>
            {
                { "Перемычка 1_Привязка снаружи", 0 },
                { "от 1й до 2й", 130/304.8 },
                { "от 2й до 3й", 130/304.8 },
                { "от 3й до 4й", 130/304.8 },
                { "от 4й до 5й", 130/304.8 },
                { "от 5й до 6й", 130/304.8 },
                { "от 6й до 7й", 130/304.8 },
                { "от 7й до 8й", 130/304.8 },
                { "от 8й до 9й", 130/304.8 },
                { "Перемычка 1_Смещение вниз", 0 }
            };

            Boardering = LintelBoardering.Nothing;
        }

        //Методы
        public void GetRequiredLintelData()
        {
            lintelElementData = new LintelElementData
            {
                H = 10000 / 304.8,
                MaxVoidWidth1 = 10000 / 304.8,
                LsupportLeft = 10000 / 304.8,
                LsupportRight = 10000 / 304.8,
            };
            GetLintelBoardering();

            string wallMaterial1Name = doc.GetElement(WCache.Layers.First().MaterialId).Name;
            string wallMaterial2Name = doc.GetElement(WCache.Layers.Last().MaterialId).Name;

            bool condition1 = wallMaterial1Name.Contains("Кирпич") && wallMaterial2Name.Contains("Кирпич");
            bool condition2 = wallMaterial1Name.Contains("Керамзитобетон") && wallMaterial2Name.Contains("Керамзитобетон");
            bool condition3 = Math.Round(VoidCache.Geom.Dims.B * 304.8) > 400;
            bool condition4 = Math.Round(VoidCache.Geom.Dims.B * 304.8) < 4000;
            bool condition5 = Boardering != LintelBoardering.Both || Math.Round(WCache.Geom.Dims.T * 304.8) == 250;
            bool condition6 = Math.Round(VoidCache.Geom.Dims.B * 304.8) <= 400;

            if ((condition1 || condition2) && condition3 && condition4 && condition5)
            {
                SectionType = CrossSectionType.Bar;
                FillLintelElementDataForBar();
            }
            else if (condition6)
            {
                SectionType = CrossSectionType.Rebar;
                FillLintelElementDataForRebar();
            }
            else
            {
                SectionType = CrossSectionType.Corner;
                FillLintelElementDataForCorner();
            }
            GetNLintelElements();
            UpdateElementsData();
        }

        private void GetNLintelElements()
        {
            switch (SectionType)
            {
                case CrossSectionType.Corner: NLintelElements = 2; break;
                case CrossSectionType.Bar:
                    int i = 1;
                    double T = ElementsPlacementData.Values.ToList()[i];
                    while (T <= WCache.Geom.Dims.T && i <= 9)
                    {
                        T += ElementsPlacementData.Values.ToList()[i];
                        i++;
                    }
                    NLintelElements = i;
                    break;
                case CrossSectionType.Rebar:
                    NLintelElements = 0;
                    foreach (CompoundStructureLayer layer in WCache.Layers)
                    {
                        if (AcceptMaterialCondition(layer))
                        {
                            int n = (int)Math.Round(layer.Width * 304.8 / 50) + 1;
                            if (n < 2) n = 2;
                            NLintelElements += n;
                        }
                    }
                    break;
            }
        }
        private void FillLintelElementDataForBar()
        {
            double RH = VoidCache.GetRemaningOfWallHeight();
            lintelElementData.Section = CrossSectionType.Bar;
            lintelElementData.L = VoidCache.Geom.Dims.B + lintelElementData.LsupportLeft + lintelElementData.LsupportRight;

            List<LintelElementData> allAvailableLintelData = new List<LintelElementData>();

            lintelElementData.MaterialName = doc.GetElement(WCache.Layers.First().MaterialId).Name;

            if (lintelElementData.MaterialName.Contains("Керамзитобетон"))
            {
                lintelElementData.T = VoidCache.HostCache.Geom.Dims.T;
                allAvailableLintelData.AddRange((from data in LintelUtils.GetAvailableBarData(doc)
                                                 where data.TypeName.Contains("ПБк")
                                                 select data).ToList());
            }
            else
            {
                lintelElementData.T = 120 / 304.8;
                allAvailableLintelData.AddRange(SerialElementsData.LintelElements);
                allAvailableLintelData.AddRange((from data in LintelUtils.GetAvailableBarData(doc)
                                                 where !data.TypeName.Contains("ПБк")
                                                 select data).ToList());
            }
            allAvailableLintelData = allAvailableLintelData.Distinct().ToList();

            foreach (LintelElementData allLintelData in allAvailableLintelData)
            {
                double currentH = Math.Round(allLintelData.H * 304.8);
                double remaningH = Math.Round(RH * 304.8);
                if (currentH < remaningH)
                {
                    if ((remaningH - currentH) < 20 / 304.8) { lintelElementData.H = allLintelData.H; break; }
                    if (currentH < Math.Round(lintelElementData.H * 304.8)) lintelElementData.H = allLintelData.H;
                }
            }

            if (Boardering != LintelBoardering.Both)
            {
                foreach (LintelElementData availableLintelData in allAvailableLintelData)
                {
                    switch (Boardering)
                    {
                        case LintelBoardering.Left:
                            double currentLsupportRight = availableLintelData.L - (lintelElementData.LsupportLeft + VoidCache.Geom.Dims.B);
                            if (Math.Round(availableLintelData.H * 304.8) == Math.Round(lintelElementData.H * 304.8)
                                && Math.Round(currentLsupportRight * 304.8) >= Math.Round(availableLintelData.LsupportMin * 304.8)
                                && Math.Round(currentLsupportRight) < Math.Round(lintelElementData.LsupportRight))
                            {
                                lintelElementData.L = availableLintelData.L;
                                lintelElementData.TypeName = availableLintelData.TypeName;
                                lintelElementData.ScheduleName = availableLintelData.TypeName;
                                lintelElementData.LsupportRight = currentLsupportRight;
                            }
                            break;

                        case LintelBoardering.Right:
                            double currentLsupportLeft = availableLintelData.L - (lintelElementData.LsupportRight + VoidCache.Geom.Dims.B);
                            if (Math.Round(availableLintelData.H * 304.8) == Math.Round(lintelElementData.H * 304.8)
                                && Math.Round(currentLsupportLeft) >= Math.Round(availableLintelData.LsupportMin * 304.8)
                                && Math.Round(currentLsupportLeft) < Math.Round(lintelElementData.LsupportLeft * 304.8))
                            {
                                lintelElementData.L = availableLintelData.L;
                                lintelElementData.TypeName = availableLintelData.TypeName;
                                lintelElementData.ScheduleName = availableLintelData.TypeName;
                                lintelElementData.LsupportLeft = currentLsupportLeft;
                            }
                            break;

                        case LintelBoardering.Nothing:
                            double currentLsupport = (availableLintelData.L - VoidCache.Geom.Dims.B) / 2;
                            if (Math.Round(availableLintelData.H * 304.8) == Math.Round(lintelElementData.H * 304.8)
                                && Math.Round(currentLsupport * 304.8) >= Math.Round(availableLintelData.LsupportMin * 304.8) && Math.Round(currentLsupport * 304.8) < Math.Round(lintelElementData.LsupportLeft * 304.8))
                            {
                                lintelElementData.L = availableLintelData.L;
                                lintelElementData.TypeName = availableLintelData.TypeName;
                                lintelElementData.ScheduleName = availableLintelData.TypeName;
                                lintelElementData.LsupportLeft = currentLsupport;
                                lintelElementData.LsupportRight = lintelElementData.LsupportLeft;
                            }
                            break;
                    }
                }

                if (lintelElementData.MaterialName.Contains("Керамзитобетон") && string.IsNullOrEmpty(lintelElementData.TypeName))
                {
                    lintelElementData.H = 190 / 304.8;
                    lintelElementData.L = VoidCache.Geom.Dims.B + 2 * 100 / 304.8;
                    string L = ((int)Math.Round(lintelElementData.L * 304.8)).ToString();
                    int key = (int)(Math.Round(lintelElementData.T * 304.8) * Math.Pow(10, 3) + Math.Round(lintelElementData.H * 304.8));

                    lintelElementData.TypeName = SerialElementsData.LintelSectionTypes[key][0] + "-" + L;
                    lintelElementData.ScheduleName = lintelElementData.TypeName;
                    lintelElementData.LsupportLeft = 100 / 304.8;
                    lintelElementData.LsupportRight = lintelElementData.LsupportLeft;
                }

            }
            else
            {
                if (lintelElementData.MaterialName.Contains("Керамзитобетон"))
                {
                    lintelElementData.H = 190 / 304.8;
                    string L = ((int)(Math.Round(lintelElementData.L * 304.8))).ToString();
                    int key = (int)(Math.Round(lintelElementData.T * 304.8) * Math.Pow(10, 3) + Math.Round(lintelElementData.H * 304.8));
                    lintelElementData.TypeName = SerialElementsData.LintelSectionTypes[key][0] + "-" + L;
                    lintelElementData.ScheduleName = lintelElementData.TypeName;
                }
                else
                {
                    double maxL = 10000 / 304.8;
                    string nearestSerialLintelTypeName = null;
                    foreach (LintelElementData availableLintelData in allAvailableLintelData)
                    {
                        double currentL = availableLintelData.L;
                        if (availableLintelData.H == lintelElementData.H && (currentL > lintelElementData.L & currentL < maxL))
                        {
                            maxL = currentL;
                            nearestSerialLintelTypeName = availableLintelData.TypeName;
                        }
                    }
                    if (nearestSerialLintelTypeName != null)
                    {
                        string L = ((int)(Math.Round(lintelElementData.L * 304.8))).ToString();
                        lintelElementData.TypeName = nearestSerialLintelTypeName + "(L=" + L + " мм)";
                        lintelElementData.ScheduleName = lintelElementData.TypeName;
                    }
                }
            }

            lintelElementData.Type = LintelUtils.GetOrCreateLintelElementType(doc, lintelElementData);
        }
        private void FillLintelElementDataForRebar()
        {
            lintelElementData.Section = CrossSectionType.Rebar;
            lintelElementData.H = 10 / 304.8;
            lintelElementData.T = 10 / 304.8;
            double lSupport = GetLSupport();

            switch (Boardering)
            {
                case LintelBoardering.Left:
                    lintelElementData.LsupportRight = lSupport * 2;
                    lintelElementData.LsupportLeft = 0;
                    break;

                case LintelBoardering.Right:
                    lintelElementData.LsupportLeft = lSupport * 2;
                    lintelElementData.LsupportRight = 0;
                    break;

                case LintelBoardering.Nothing:
                    lintelElementData.LsupportLeft = lSupport;
                    lintelElementData.LsupportRight = lSupport;
                    break;
            }

            lintelElementData.L = VoidCache.Geom.Dims.B + lintelElementData.LsupportLeft + lintelElementData.LsupportRight;

            string L = ((int)Math.Round(lintelElementData.L * 304.8)).ToString();
            string D = ((int)Math.Round(lintelElementData.T * 304.8)).ToString();
            lintelElementData.TypeName = D + "-А500С L=" + L;
            lintelElementData.ScheduleName = D + "-А500С";

            lintelElementData.Type = LintelUtils.GetOrCreateLintelElementType(doc, lintelElementData);
        }
        private void FillLintelElementDataForCorner()
        {
            lintelElementData.Section = CrossSectionType.Corner;
            if (WCache.Geom.Dims.T * 304.8 <= 100)
            {
                if (VoidCache.Geom.Dims.B * 304.8 <= 1500)
                {
                    lintelElementData.H = 32 / 304.8;
                    lintelElementData.T = 4 / 304.8;
                    lintelElementData.LsupportMin = 150 / 304.8;
                }
                else
                {
                    lintelElementData.H = 50 / 304.8;
                    lintelElementData.T = 5 / 304.8;
                    lintelElementData.LsupportMin = 150 / 304.8;
                }
                LintelCornerElementsData redefinedCornerElementsData = CornerElementsData;
                redefinedCornerElementsData.PlatesStep = 300 / 304.8;
                redefinedCornerElementsData.PlatesN = 3;
                redefinedCornerElementsData.PlateB = 60 / 304.8;
                CornerElementsData = redefinedCornerElementsData;
            }
            else
            {
                if (VoidCache.Geom.Dims.B * 304.8 <= 2000)
                {
                    lintelElementData.H = 50 / 304.8;
                    lintelElementData.T = 5 / 304.8;
                    lintelElementData.LsupportMin = 150 / 304.8;
                }
                else if (VoidCache.Geom.Dims.B * 304.8 <= 3000)
                {
                    lintelElementData.H = 63 / 304.8;
                    lintelElementData.T = 5 / 304.8;
                    lintelElementData.LsupportMin = 250 / 304.8;
                }
                else
                {
                    lintelElementData.H = 100 / 304.8;
                    lintelElementData.T = 8 / 304.8;
                    lintelElementData.LsupportMin = 250 / 304.8;
                }
            }

            switch (Boardering)
            {
                case LintelBoardering.Left:
                    lintelElementData.LsupportRight = lintelElementData.LsupportMin;
                    break;

                case LintelBoardering.Right:
                    lintelElementData.LsupportLeft = lintelElementData.LsupportMin;
                    break;

                case LintelBoardering.Nothing:
                    lintelElementData.LsupportLeft = lintelElementData.LsupportMin;
                    lintelElementData.LsupportRight = lintelElementData.LsupportMin;
                    break;
            }

            lintelElementData.L = VoidCache.Geom.Dims.B + lintelElementData.LsupportLeft + lintelElementData.LsupportRight;

            string L = ((int)Math.Round(lintelElementData.L * 304.8)).ToString();
            string H = ((int)Math.Round(lintelElementData.H * 304.8)).ToString();
            string T = ((int)Math.Round(lintelElementData.T * 304.8)).ToString();

            lintelElementData.TypeName = "L" + H + "x" + T + " L=" + L;
            lintelElementData.ScheduleName = H + "x" + T;

            lintelElementData.Type = LintelUtils.GetOrCreateLintelElementType(doc, lintelElementData);
        }
        private void UpdateElementsData()
        {
            ElementsData = new List<LintelElementData>();
            switch (SectionType)
            {
                case CrossSectionType.Bar:
                    for (int i = 0; i < NLintelElements; i++) ElementsData.Add(lintelElementData); break;
                case CrossSectionType.Rebar:
                    int startN = 0;
                    double startPosition = 10 / 304.8;
                    foreach (CompoundStructureLayer layer in WCache.Layers)
                    {
                        if (AcceptMaterialCondition(layer) && startN < NLintelElements)
                        {
                            int n = (int)Math.Round(layer.Width * 304.8 / 50);
                            if (n < 1) n = 1;
                            double step = (layer.Width - 2 * 10 / 304.8) / n;

                            for (int i = startN; i < NLintelElements; i++)
                            {
                                string key = FamilyStandardParameters.LintelElementPositionParameterNames[i];
                                if (i == 0) ElementsPlacementData[key] = -startPosition;
                                else if (i == startN) ElementsPlacementData[key] = startPosition;
                                else ElementsPlacementData[key] = step;
                                ElementsData.Add(lintelElementData);
                            }
                            startN += n + 1;
                            startPosition = 10 / 304.8;
                        }
                        else startPosition += layer.Width + 10 / 304.8;
                    }
                    ;

                    for (int i = NLintelElements; i < 10; i++)
                    {
                        string key = FamilyStandardParameters.LintelElementPositionParameterNames[i];
                        ElementsPlacementData[key] = 0;
                    }
                    break;


                case CrossSectionType.Corner:
                    LintelElementData mirroredLintelElementData = lintelElementData;
                    mirroredLintelElementData.Section = CrossSectionType.CornerMirrored;
                    mirroredLintelElementData.TypeName = lintelElementData.TypeName + " зеркально";
                    mirroredLintelElementData.Type = LintelUtils.GetOrCreateLintelElementType(doc, mirroredLintelElementData);
                    ElementsData.Add(mirroredLintelElementData);
                    ElementsData.Add(lintelElementData);
                    ElementsPlacementData["Перемычка 1_Привязка снаружи"] = lintelElementData.T;
                    ElementsPlacementData["от 1й до 2й"] = WCache.Geom.Dims.T + 2 * lintelElementData.T;
                    break;
            }
        }
        private void GetLintelBoardering()
        {
            VoidCache.Geom.GetWallVoidSolid(WCache);
            List<List<PlanarFace>> faceSets = new List<List<PlanarFace>>
            {
                VoidCache.Geom.Faces.Left,
                VoidCache.Geom.Faces.Right
            };

            Dictionary<LintelBoardering, double> result = new Dictionary<LintelBoardering, double>();

            for (int i = 0; i < faceSets.Count; i++)
            {
                List<PlanarFace> faces = faceSets[i];
                List<Element> concrete = new List<Element>();
                foreach (PlanarFace face in faces)
                {
                    List<CurveLoop> cls = face.GetEdgesAsCurveLoops().ToList();
                    Solid catchSolid = null;
                    try { catchSolid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, face.FaceNormal.Normalize(), 250 / 304.8); }
                    catch { }
                    ;
                    if (catchSolid != null)
                    {
                        foreach (RevitLinkInstance link in links)
                        {
                            Document linkDoc = link.GetLinkDocument();
                            concrete.AddRange(new ExtractingTools.ConcreteExtractor(linkDoc, catchSolid, StructureElementFilters.ColumnsOrWalls).ToElements());
                            if (concrete.Count > 0)
                            {
                                VoidCache.HostCache.Geom.GetSolidData();
                                Solid supportSolid = BooleanOperationsUtils.ExecuteBooleanOperation(VoidCache.HostCache.Geom.Solid, catchSolid, BooleanOperationsType.Intersect);
                                double lsup = supportSolid.Volume / (WCache.Geom.Dims.T * VoidCache.Geom.Dims.H) - 10 / 304.8;
                                result.Add((LintelBoardering)i, lsup);

                            }
                            break;
                        }
                    }
                }
            }
            switch (result.Count)
            {
                case 1:
                    Boardering = result.Keys.First();
                    switch (Boardering)
                    {
                        case LintelBoardering.Left:
                            lintelElementData.LsupportLeft = result.Values.First();
                            break;

                        case LintelBoardering.Right:
                            lintelElementData.LsupportRight = result.Values.First();
                            break;
                    }
                    break;
                case 2:
                    Boardering = LintelBoardering.Both;
                    lintelElementData.LsupportLeft = result[LintelBoardering.Left];
                    lintelElementData.LsupportRight = result[LintelBoardering.Right];
                    break;
            }
        }

        private double GetLSupport()
        {
            double lSupport = 0;
            double currentLSupport;

            foreach (CompoundStructureLayer layer in (VoidCache.HostCache as WallCache).Layers)
            {
                double t = Math.Round(layer.Width * 304.8);
                if (t <= 65) currentLSupport = 65 / 304.8;
                else currentLSupport = 120 / 304.8;

                if (lSupport < currentLSupport) lSupport = currentLSupport;
            }
            return lSupport;
        }

        private protected bool AcceptMaterialCondition(CompoundStructureLayer layer)
        {
            string materialName = doc.GetElement(layer.MaterialId).Name;
            bool condition1 = materialName.Contains("ирпич");
            bool condition2 = materialName.Contains("азобетон");
            bool condition3 = materialName.Contains("азогребн");
            bool condition4 = materialName.Contains("ерамзит");
            return condition1 || condition2 || condition3 || condition4;
        }

        //Структуры
        public struct LintelCornerElementsData
        {
            public double PlateL { get; set; }
            public double PlateB { get; set; }
            public double PlateT { get; set; }
            public int PlatesN { get; set; }
            public double PlatesStep { get; set; }
            public double PlatesAlign { get; set; }
        }

        //Свойства
        public FamilySymbol Type { get; set; }
        public VoidCache VoidCache { get; set; }
        public WallCache WCache { get; set; }
        public int NLintelElements { get; set; }
        public List<LintelElementData> ElementsData { get; set; }
        public LintelCornerElementsData CornerElementsData { get; set; }
        public Dictionary<string, double> ElementsPlacementData { get; set; }
        public LintelBoardering Boardering { get; set; }
        public CrossSectionType SectionType { get; set; }
    }
}
