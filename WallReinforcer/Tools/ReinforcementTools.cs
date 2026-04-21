using RevitOSA.WallReinforcer.Resources;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Tools
{
    public static class ReinforcementTools
    {
        private static readonly Dictionary<string, double> RbtValues = new Dictionary<string, double>
        {
            { "B15", 0.75 },
            { "B20", 0.9 },
            { "B25", 1.05 },
            { "B30", 1.15 },
            { "B35", 1.3 },
            { "B40", 1.4 },
            { "B45", 1.5 }
        };
        private static readonly Dictionary<string, double> RsValues = new Dictionary<string, double>
        {
            { "A240", 210 },
            { "A400", 340 },
            { "A500", 435 }
        };
        private static readonly Dictionary<AnchorMode, double> alpha12Values = new Dictionary<AnchorMode, double>
        {
            { AnchorMode.AnchorCompress, 0.75 },
            { AnchorMode.AnchorTense, 1 },
            { AnchorMode.OverlapCompress, 0.9 },
            { AnchorMode.OverlapTense, 1.2 }
        };

        public static double ComputeBendDiameter(double d, string rClass)
        {
            double db;
            if (rClass == "А240") { if (Math.Round(d * 304.8) < 20) db = 2.5 * d; else db = 4 * d; }
            else { if (Math.Round(d * 304.8) < 20) db = 5 * d; else db = 8 * d; }
            ;
            return db;
        }

        public static double ComputeAorOVLength(double d, string bClass, string rClass, AnchorMode ancMode)
        {
            double l0an = ComputeBaseAnchorLength(d, bClass, rClass);
            double lan = alpha12Values[ancMode] * l0an;
            List<double> values = new List<double> { 0.3 * l0an, 15 * d, 200 / 304.8, lan };
            double roundedLan = Math.Ceiling(values.Max() * 304.8 / 10) * 10 / 304.8;
            return roundedLan;
        }

        public static double ComputeBaseAnchorLength(double d, string bClass, string rClass)
        {
            double Rbt = RbtValues[bClass];
            double Rs = RsValues[rClass];
            double Rbond = Rbt * 2.5 * 1;
            double As = Math.PI * Math.Pow(d, 2) / 4;
            double us = Math.PI * d;
            double l0an = Rs * As / (Rbond * us);
            return l0an;
        }
    }
}
