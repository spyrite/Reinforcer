using RevitOSA.WallReinforcer.Resources;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources1P
{
    public static class SerialElementsData
    {
        public static readonly Dictionary<int, List<object>> LintelSectionTypes = new Dictionary<int, List<object>>
        {
            { 140, new List<object>()  { "2ПБ", 100, 4 } },
            { 220, new List<object>()  { "3ПБ", 170, 8 } },
            { 290, new List<object>()  { "4ПБ", 210, 8 } },
            { 90190, new List<object>()  { "1ПБк", 100, null } },
            { 190190, new List<object>()  { "2ПБк", 100, null } }
        };

        public static readonly List<LintelElementData> LintelElements = new List<LintelElementData>
        {
            new LintelElementData
            {
                TypeName = "2ПБ10-1",
                L = 1030/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 830/304.8,
                MaxVoidWidth2 = 930/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ13-1",
                L = 1290/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 1090/304.8,
                MaxVoidWidth2 = 1190/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ16-2",
                L = 1550/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 1350/304.8,
                MaxVoidWidth2 = 1480/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ17-2",
                L = 1680/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 1480/304.8,
                MaxVoidWidth2 = 1580/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ19-3",
                L = 1940/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 1740/304.8,
                MaxVoidWidth2 = 1840/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ22-3",
                L = 2200/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 2000/304.8,
                MaxVoidWidth2 = 2100/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ25-3",
                L = 2460/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 2260/304.8,
                MaxVoidWidth2 = 2360/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ26-4",
                L = 2590/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 2390/304.8,
                MaxVoidWidth2 = 2490/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ29-4",
                L = 2850/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 2650/304.8,
                MaxVoidWidth2 = 2750/304.8,
            },
            new LintelElementData
            {
                TypeName = "2ПБ30-4",
                L = 2980/304.8,
                T = 120/304.8,
                H = 140/304.8,
                LsupportMin = 150/304.8,
                MaxVoidWidth1 = 2680/304.8,
                MaxVoidWidth2 = 2830/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ18-8",
                L = 1810/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 170/304.8,
                MaxVoidWidth1 = 1470/304.8,
                MaxVoidWidth2 = 1640/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ21-8",
                L = 2070/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 170/304.8,
                MaxVoidWidth1 = 1730/304.8,
                MaxVoidWidth2 = 1900/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ27-8",
                L = 2720/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 170/304.8,
                MaxVoidWidth1 = 2380/304.8,
                MaxVoidWidth2 = 2550/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ30-8",
                L = 2980/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 210/304.8,
                MaxVoidWidth1 = 2560/304.8,
                MaxVoidWidth2 = 2770/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ34-4",
                L = 3370/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 3170/304.8,
                MaxVoidWidth2 = 3270/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ36-4",
                L = 3630/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 3430/304.8,
                MaxVoidWidth2 = 3530/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ39-8",
                L = 3890/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 210/304.8,
                MaxVoidWidth1 = 3470/304.8,
                MaxVoidWidth2 = 3680/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ13-37",
                L = 3890/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 170/304.8,
                MaxVoidWidth1 = 950/304.8,
                MaxVoidWidth2 = 1120/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ16-37",
                L = 1550/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 170/304.8,
                MaxVoidWidth1 = 1210/304.8,
                MaxVoidWidth2 = 1380/304.8,
            },
            new LintelElementData
            {
                TypeName = "3ПБ18-37",
                L = 1810/304.8,
                T = 120/304.8,
                H = 220/304.8,
                LsupportMin = 200/304.8,
                MaxVoidWidth1 = 1410/304.8,
                MaxVoidWidth2 = 1610/304.8,
            },
            new LintelElementData
            {
                TypeName = "4ПБ30-4",
                L = 2980/304.8,
                T = 120/304.8,
                H = 290/304.8,
                LsupportMin = 100/304.8,
                MaxVoidWidth1 = 2780/304.8,
                MaxVoidWidth2 = 2880/304.8,
            },
            new LintelElementData
            {
                TypeName = "4ПБ44-8",
                L = 4410/304.8,
                T = 120/304.8,
                H = 290/304.8,
                LsupportMin = 210/304.8,
                MaxVoidWidth1 = 3990/304.8,
                MaxVoidWidth2 = 4200/304.8,
            },
            new LintelElementData
            {
                TypeName = "4ПБ48-8",
                L = 4800/304.8,
                T = 120/304.8,
                H = 290/304.8,
                LsupportMin = 210/304.8,
                MaxVoidWidth1 = 4380/304.8,
                MaxVoidWidth2 = 4590/304.8,
            },
            new LintelElementData
            {
                TypeName = "4ПБ60-8",
                L = 5960/304.8,
                T = 120/304.8,
                H = 290/304.8,
                LsupportMin = 250/304.8,
                MaxVoidWidth1 = 5460/304.8,
                MaxVoidWidth2 = 5710/304.8,
            }
    };
    }
}
