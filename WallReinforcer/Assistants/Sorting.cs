using Autodesk.Revit.DB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RevitOSA.WallReinforcer.Assistants
{
    public static class Sorting
    {
        /*public static double ElemMarkNs(ViewCache vc)
        {
            double ns = 0;
            if (vc.ElemMark != null
                && vc.ElemMark.Split('-').Count() == 2)
            {
                string[] markNumberParts = vc.ElemMark.Split('-').Last().Split('.');
                for (int i = 0; i < markNumberParts.Count(); i++)
                    if (int.TryParse(markNumberParts[i], out int N)) ns += N * Math.Pow(0.01, i);
            }
            return ns;
        }*/
        public static double ElemMarkNs(string elemMark)
        {
            double ns = 0;
            if (elemMark.Split('-').Count() == 2)
            {
                string[] markNumberParts = elemMark.Split('-').Last().Split('.');
                for (int i = 0; i < markNumberParts.Count(); i++)
                    if (int.TryParse(markNumberParts[i], out int N)) ns += N * Math.Pow(0.01, i);
            }
            return ns;
        }

        #region Comparers
        public class SemiNumericComparer : IComparer<string>
        {
            public int Compare(string s1, string s2)
            {
                if (double.TryParse(s1, out var i1)
                    && double.TryParse(s2, out var i2))
                {
                    if (i1 > i2)
                    {
                        return 1;
                    }

                    if (i1 < i2)
                    {
                        return -1;
                    }

                    if (i1 == i2)
                    {
                        return 0;
                    }
                }

                var text1 = SplitCharsAndNums(s1);
                var text2 = SplitCharsAndNums(s2);

                if (text1.Length > 1 && text2.Length > 1)
                {
                    for (var i = 0; i < Math.Max(text1.Length, text2.Length); i++)
                    {
                        if (text1[i] != null && text2[i] != null)
                        {
                            var pos = Compare(text1[i], text2[i]);
                            if (pos != 0)
                            {
                                return pos;
                            }
                        }
                        else
                        {
                            //text1[i] is null there for the string is shorter and comes before a longer string.
                            if (text1[i] == null)
                            {
                                return -1;
                            }
                            if (text2[i] == null)
                            {
                                return 1;
                            }
                        }
                    }
                }

                int acceptCase = string.Compare(s1, s2, false);
                int ignoreCase = string.Compare(s1, s2, true);
                if (acceptCase != ignoreCase) acceptCase *= -1;
                return acceptCase;
            }

            private string[] SplitCharsAndNums(string text)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    var sb = new StringBuilder();
                    for (var i = 0; i < text.Length - 1; i++)
                    {
                        if (!char.IsDigit(text[i]) && char.IsDigit(text[i + 1]) ||
                            char.IsDigit(text[i]) && !char.IsDigit(text[i + 1]))
                        {
                            sb.Append(text[i]);
                            sb.Append(" ");
                        }
                        else
                        {
                            sb.Append(text[i]);
                        }
                    }

                    sb.Append(text[text.Length - 1]);

                    return sb.ToString().Split(' ');
                }
                else return new string[] { };
            }
        }
        public class StrCmpLogicalComparer : Comparer<string>
        {
            [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string x, string y);

            public override int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }
        public class AlphaNumericComparer : IComparer
        {
            public int Compare(object x, object y)
            {
                if (!(x is string s1))
                {
                    return 0;
                }
                if (!(y is string s2))
                {
                    return 0;
                }

                int len1 = s1.Length;
                int len2 = s2.Length;
                int marker1 = 0;
                int marker2 = 0;

                // Walk through two the strings with two markers.
                while (marker1 < len1 && marker2 < len2)
                {
                    char ch1 = s1[marker1];
                    char ch2 = s2[marker2];

                    // Some buffers we can build up characters in for each chunk.
                    char[] space1 = new char[len1];
                    int loc1 = 0;
                    char[] space2 = new char[len2];
                    int loc2 = 0;

                    // Walk through all following characters that are digits or
                    // characters in BOTH strings starting at the appropriate marker.
                    // Collect char arrays.
                    do
                    {
                        space1[loc1++] = ch1;
                        marker1++;

                        if (marker1 < len1)
                        {
                            ch1 = s1[marker1];
                        }
                        else
                        {
                            break;
                        }
                    } while (char.IsDigit(ch1) == char.IsDigit(space1[0]));

                    do
                    {
                        space2[loc2++] = ch2;
                        marker2++;

                        if (marker2 < len2)
                        {
                            ch2 = s2[marker2];
                        }
                        else
                        {
                            break;
                        }
                    } while (char.IsDigit(ch2) == char.IsDigit(space2[0]));

                    // If we have collected numbers, compare them numerically.
                    // Otherwise, if we have strings, compare them alphabetically.
                    string str1 = new string(space1);
                    string str2 = new string(space2);

                    int result;

                    if (char.IsDigit(space1[0]) && char.IsDigit(space2[0]))
                    {
                        int thisNumericChunk = int.Parse(str1);
                        int thatNumericChunk = int.Parse(str2);
                        result = thisNumericChunk.CompareTo(thatNumericChunk);
                    }
                    else
                    {
                        result = str1.CompareTo(str2);
                    }

                    if (result != 0)
                    {
                        return result;
                    }
                }
                return len1 - len2;
            }
        }
        public class SemiNumericComparer2 : IComparer<string>
        {
            public int Compare(string s1, string s2)
            {
                if (IsNumeric(s1) && IsNumeric(s2))
                    return Convert.ToInt32(s1) - Convert.ToInt32(s2);

                if (IsNumeric(s1) && !IsNumeric(s2))
                    return -1;

                if (!IsNumeric(s1) && IsNumeric(s2))
                    return 1;

                return string.Compare(s1, s2, true);
            }

            public static bool IsNumeric(object value)
            {
                return int.TryParse(value.ToString(), out _);
            }
        }

        public class XYZCoordsComparer : IComparer<XYZ>
        {
            public int Compare(XYZ p0, XYZ p1)
            {
                bool condition1 = Math.Round(p0.X * 304.8) <= Math.Round(p1.X * 304.8);
                bool condition2 = Math.Round(p0.Y * 304.8) <= Math.Round(p1.Y * 304.8);
                bool condition3 = Math.Round(p0.Z * 304.8) <= Math.Round(p1.Z * 304.8);

                if (condition1 && condition2 && condition3) return -1;
                else if (p0.IsAlmostEqualTo(p1)) return 0;
                else return 1;
            }
        }

        public class XYZEqualityComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ p0, XYZ p1)
            {
                /*bool condition1 = Math.Round(p0.X * 304.8) == Math.Round(p1.X * 304.8);
                  bool condition2 = Math.Round(p0.Y * 304.8) == Math.Round(p1.Y * 304.8);
                  bool condition3 = Math.Round(p0.Z * 304.8) == Math.Round(p1.Z * 304.8);

                  return condition1 & condition2 & condition3;*/

                return p0.IsAlmostEqualTo(p1);
            }

            public int GetHashCode(XYZ obj)
            {
                return 0;
            }
        }
        #endregion
    }
}
