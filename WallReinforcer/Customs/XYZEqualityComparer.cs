using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Customs
{
    // Вспомогательный класс для сравнения XYZ с учетом погрешности
    public class XYZEqualityComparer : IEqualityComparer<XYZ>
    {
        private const double Tolerance = 1e-6; // ~0.0003 мм

        public bool Equals(XYZ x, XYZ y)
        {
            if (x == null || y == null) return false;
            return x.IsAlmostEqualTo(y, Tolerance);
        }

        public int GetHashCode(XYZ obj)
        {
            if (obj == null) return 0;
            // Округляем координаты для хеш-кода, чтобы близкие точки имели одинаковый хеш
            long ix = (long)Math.Round(obj.X / Tolerance);
            long iy = (long)Math.Round(obj.Y / Tolerance);
            long iz = (long)Math.Round(obj.Z / Tolerance);

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + ix.GetHashCode();
                hash = hash * 23 + iy.GetHashCode();
                hash = hash * 23 + iz.GetHashCode();
                return hash;
            }
        }
    }
}
