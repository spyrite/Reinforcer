using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Tools;
using System.Collections.Generic;

using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryWallEndCache : GeometryCache
    {
        // Конструкторы
        public GeometryWallEndCache(GeometryWallCache geometryWallCache, XYZ origin, XYZ xDir)
        {
            Dirs = new Directions
            {
                X = xDir,
                Y = geometryWallCache.Dirs.Y,
                Z = geometryWallCache.Dirs.Y
            };

            Dims = new ControlDimensions
            {
                L = ReinfSettings.Default.reinf_Walls_Y_Edge_CenterAlign / 304.8
                + ReinfSettings.Default.reinf_Walls_Y_Edge_Step / 304.8
                + ReinfSettings.Default.reinf_Walls_Y_Step / 2 / 304.8,
                T = geometryWallCache.Dims.T,
                H = geometryWallCache.Dims.H,
            };

            Origins = new ControlPoints
            {
                CenterStartBottom = origin,
                CenterMiddleBottom = origin + Dirs.X * Dims.L / 2,
                CenterEndBottom = origin + Dirs.X * Dims.L
            };
        }

        // Методы
        public override void GetSolidData()
        {
            List<List<int>> tokenSets = new List<List<int>>
            {
                new List<int> {0, -1},
                new List<int> {1, -1},
                new List<int> {1, 1},
                new List<int> {0, 1}
            };

            List<XYZ> points = new List<XYZ>();
            foreach (List<int> tokens in tokenSets)
            {
                XYZ point = Origins.CenterMiddleBottom + Dirs.X * Dims.T * tokens[0] + Dirs.Y * (Dims.T / 2) * tokens[1];
                points.Add(point);
            }

            List<Curve> lines = new List<Curve>
            {
                Line.CreateBound(points[0], points[1]),
                Line.CreateBound(points[1], points[2]),
                Line.CreateBound(points[2], points[3]),
                Line.CreateBound(points[3], points[0])
            };

            Solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { CurveLoop.Create(lines) }, Dirs.Z, Dims.H);
        }
        public void GetOutline()
        {
            XYZ p0 = Origins.CenterMiddleBottom - GeometryTools.VecABS(Dirs.X) * Dims.T - GeometryTools.VecABS(Dirs.Y) * Dims.T / 2;
            XYZ p1 = Origins.CenterMiddleBottom + GeometryTools.VecABS(Dirs.X) * Dims.T + GeometryTools.VecABS(Dirs.Y) * Dims.T / 2 + Dirs.Z * Dims.H;
            Outline = new Outline(p0, p1);
        }
    }
}
