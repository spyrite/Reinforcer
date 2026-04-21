using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

using AnnSettings = RevitOSA.WallReinforcer.Properties.Annotations;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GridCache
    {
        private readonly Document doc;
        private readonly Options opt = new Options();

        public struct View2DData
        {
            public View View;
            public List<Leader> Leaders;
            public List<Curve> Curves;
            public List<bool> VisibleBubbles;
            public string Name;
        }

        public GridCache(Grid grid)
        {
            doc = grid.Document;
            Grid = grid;
            Names = new List<string> { Grid.Name };
            Lines = new List<Line> { Grid.Curve as Line, null };
            Centers = new List<XYZ> { Lines[0].Evaluate(0.5, true), null };
            Origin = Lines[0].Origin;
            Dirs = new GeometryCache.Directions
            {
                X = Lines[0].Direction,
                Y = Lines[0].Direction.CrossProduct(XYZ.BasisZ),
                Z = XYZ.BasisZ
            };
            CenterPlane = Plane.CreateByOriginAndBasis(Origin, Dirs.X, Dirs.Z);
        }

        public Grid Grid { get; }
        public View View { get; set; }
        public List<string> Names { get; set; }
        public List<Line> Lines { get; set; }
        public List<XYZ> Centers { get; set; }
        public XYZ Origin { get; }
        public GeometryCache.Directions Dirs { get; }
        public Plane CenterPlane { get; }
        public List<bool> HasBubbles { get; set; }
        public XYZ RayWhereGridHasBeenCatchedDir { get; set; }

        public Line GetViewGridLine(ViewCache vc)
        {
            Solid solid = vc.CropBox.CreateBoxSolid(vc.CropBox.Dims.H);
            List<Curve> spotlines = solid.IntersectWithCurve(Lines[1], null).ToList();
            if (spotlines.Count > 0)
            {
                Line line = spotlines.First() as Line;
                XYZ dir = line.Direction;
                XYZ p0 = line.GetEndPoint(0) - AnnSettings.Default.gridTail_l0 / 304.8 * dir;
                XYZ p1 = line.GetEndPoint(1) + AnnSettings.Default.gridTail_l0 / 304.8 * dir;
                line = Line.CreateBound(p0, p1);
                return line;
            }
            return null;
        }
        public void UpdateViewGeometrics(View view)
        {
            doc.Regenerate();
            View = view;
            opt.View = View;
            Lines[1] = Grid.get_Geometry(opt).First() as Line;
            Centers[1] = Lines[1].Evaluate(0.5, true);
            HasBubbles = new List<bool>
            {
                Grid.IsBubbleVisibleInView(DatumEnds.End0, View),
                Grid.IsBubbleVisibleInView(DatumEnds.End1, View)
            };
        }
        public static View2DData GetGridView2DData(Grid grid, View view)
        {
            View2DData data2D = new View2DData
            {
                View = view,
                VisibleBubbles = new List<bool> { grid.IsBubbleVisibleInView(DatumEnds.End0, view), grid.IsBubbleVisibleInView(DatumEnds.End1, view) },
                Leaders = new List<Leader> { grid.GetLeader(DatumEnds.End0, view), grid.GetLeader(DatumEnds.End1, view) },
                Curves = grid.GetCurvesInView(DatumExtentType.ViewSpecific, view).ToList(),
                Name = grid.Name
            };

            return data2D;
        }
    }
}
