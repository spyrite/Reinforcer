using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Resources
{
    internal static class RebarStandardData
    {
        internal static readonly Dictionary<double, double> StirrupDiameters = new Dictionary<double, double>
        {
            {16, 6/304.8},
            {18, 6/304.8},
            {20, 6/304.8},
            {22, 6/304.8},
            {25, 8/304.8},
            {28, 8/304.8},
            {32, 8/304.8},
            {36, 10/304.8}
        };
    }
}
