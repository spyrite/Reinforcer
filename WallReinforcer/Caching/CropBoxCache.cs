using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace RevitOSA.WallReinforcer.Caching
{
    public class CropBoxCache : BoxCache
    {
        private readonly XYZ DiagonalVec;
        public Outline CentroidOutline { get; private set; }

        public CropBoxCache(BoundingBoxXYZ box, GeometryCache.Directions dirs)
        {
            Dirs = dirs;
            DiagonalVec = box.Transform.OfPoint(box.Max) - box.Transform.OfPoint(box.Min);
            preCalculations = new List<object>
            {
                box.Min.DistanceTo(box.Max),
                box.Transform.OfPoint(box.Min),
                Line.CreateBound(box.Transform.OfPoint(box.Min), box.Transform.OfPoint(box.Max)),
                box.Transform.OfPoint(box.Max)
            };

            Dims = new BoxDimensions
            {
                Diagonal = (double)preCalculations[0],
                L = (double)preCalculations[0] * Math.Cos(DiagonalVec.AngleTo(Dirs.X)),
                B = (double)preCalculations[0] * Math.Cos(DiagonalVec.AngleTo(Dirs.Y)),
                H = (double)preCalculations[0] * Math.Cos(DiagonalVec.AngleTo(Dirs.Z))
            };
            Points = new BoxPoints
            {
                Origin = (XYZ)preCalculations[1],
                Centroid = (preCalculations[2] as Line).Evaluate(0.5, true),
                Far = (XYZ)preCalculations[3]
            };
            Lines = new BoxLines
            {
                Diagonal = (Line)preCalculations[2],
                Bot = Line.CreateBound(Points.Origin, Points.Origin + Dirs.X * Dims.L),
                Left = Line.CreateBound(Points.Origin, Points.Origin + Dirs.Y * Dims.B),
                CentroidDepth = Line.CreateBound(Points.Centroid - Dirs.Z * Dims.H / 2, Points.Centroid + Dirs.Z * Dims.H / 2),
                CentroidDepthUnbound = Line.CreateUnbound(Points.Centroid, Dirs.Z)
            };
            Lines.Top = Lines.Bot.CreateTransformed(Transform.CreateTranslation(Dirs.Y * Dims.B)) as Line;
            Lines.Right = Lines.Left.CreateTransformed(Transform.CreateTranslation(Dirs.X * Dims.L)) as Line;

            Planes = new BoxPlanes
            {
                Front = Plane.CreateByOriginAndBasis(Points.Origin, Dirs.X, Dirs.Y),
                Center = Plane.CreateByOriginAndBasis(Points.Centroid, Dirs.X, Dirs.Y),
                Rear = Plane.CreateByOriginAndBasis(Points.Far, Dirs.X, Dirs.Y)
            };
            GetCentoridOutline();
        }

        private void GetCentoridOutline()
        {
            CentroidOutline = new Outline(Points.Centroid - Dirs.Z * Dims.H / 2, Points.Centroid + Dirs.Z * Dims.H / 2 + Dirs.Y * 10 / 304.8);
            if (CentroidOutline.IsEmpty) CentroidOutline = new Outline(Points.Centroid - Dirs.Z * Dims.H / 2, Points.Centroid + Dirs.Z * Dims.H / 2 - Dirs.Y * 10 / 304.8);
        }
    }
}
