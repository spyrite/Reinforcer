using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryWallRegionCache : GeometryCache
    {
        // Поля
        GeometryWallCache geomWallCache;

        // Конструкторы
        public GeometryWallRegionCache(GeometryWallCache geomWallCache, XYZ startPoint, XYZ endPoint)
        {
            this.geomWallCache = geomWallCache;
            Lines = new ControlLines
            {
                CenterBot = Line.CreateBound(startPoint, endPoint)
            };

            Origins = new ControlPoints
            {
                CenterStartBottom = startPoint,
                CenterMiddleBottom = Lines.CenterBot.Evaluate(0.5, true),
                CenterEndBottom = endPoint
            };

            Dims = new ControlDimensions
            {
                L = Lines.CenterBot.Length,
                H = geomWallCache.Dims.H,
                T = geomWallCache.Dims.T,
                ZBot = Origins.CenterMiddleBottom.Z
            };

            Dirs = new Directions
            {
                X = (Lines.CenterBot as Line).Direction.Normalize(),
                Z = geomWallCache.Dirs.Z
            };

            HasPointsCoincidentWithWall();
        }

        // Методы
        public override void GetSolidData()
        {
            if (geomWallCache.Solid == null) geomWallCache.GetSolidData();
            UnboundFaces = new UnboundElemFaces
            {
                Left = new List<Plane> { Plane.CreateByNormalAndOrigin(geomWallCache.Dirs.X, Origins.CenterStartBottom) },
                Right = new List<Plane> { Plane.CreateByNormalAndOrigin(geomWallCache.Dirs.X, Origins.CenterEndBottom) }
            };

            Solid = BooleanOperationsUtils.CutWithHalfSpace(geomWallCache.Solid, UnboundFaces.Left.First());
            BooleanOperationsUtils.CutWithHalfSpaceModifyingOriginalSolid(Solid, UnboundFaces.Right.First());
        }
        public void GetAccurateHeight()
        {
            List<double> hs = new List<double>();
            for (int i = 0; i < 10; i++)
            {
                XYZ midPoint = Lines.CenterBot.Evaluate(i / 10, true);
                Line cutLine = Line.CreateBound(midPoint, midPoint + Dirs.Z * Dims.H);
                if (Solid == null) GetSolidData();
                List<Curve> spotLines = Solid.IntersectWithCurve(cutLine, null).ToList();
                if (spotLines.Count == 1) hs.Add(spotLines.First().Length);
            }
            if (hs.Count > 0)
            {
                ControlDimensions dims = Dims;
                dims.H = hs.Max();
                Dims = dims;
            }
        }
        public void GetOutLine()
        {
            XYZ p0 = Origins.CenterMiddleBottom - GeometryTools.VecABS(Dirs.X) * Dims.L / 2 - GeometryTools.VecABS(Dirs.Y) * Dims.T / 2;
            XYZ p1 = Origins.CenterMiddleBottom + GeometryTools.VecABS(Dirs.X) * Dims.L / 2 + GeometryTools.VecABS(Dirs.Y) * Dims.T / 2 + Dirs.Z * Dims.H;
            Outline = new Outline(p0, p1);
        }
        private void HasPointsCoincidentWithWall()
        {
            EndPointsCoincidentWithWallEndPoints = new List<bool> { false, false };
            List<XYZ> checkPoints = new List<XYZ>
            {
                Origins.CenterStartBottom,
                Origins.CenterEndBottom,
                geomWallCache.Origins.CenterStartBottom,
                geomWallCache.Origins.CenterEndBottom,
            };
            for (int i = 0; i < 2; i++)
                if (checkPoints[i].IsAlmostEqualTo(checkPoints[i + 2]))
                    EndPointsCoincidentWithWallEndPoints[i] = true;
        }

        // Свойства
        public List<bool> EndPointsCoincidentWithWallEndPoints { get; set; }
    }
}
