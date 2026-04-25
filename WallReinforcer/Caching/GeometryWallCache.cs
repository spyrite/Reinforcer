using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryWallCache : GeometryCache
    {
        // Конструкторы
        public GeometryWallCache(Wall wall) : base(wall)
        {
            /*preCalculations = new List<object>
            {
                wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(),
                wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble()
            };
            LvlIds = new LevelIds
            {
                Bot = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId(),
                Top = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId(),
                Base = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()
            };
            Dirs = new Directions
            {
                X = ((Elem.Location as LocationCurve).Curve as Line).Direction,
                Y = wall.Orientation,
                Z = XYZ.BasisZ
            };
            Dims = new ControlDimensions
            {
                L = (Elem.Location as LocationCurve).Curve.Length,
                T = wall.Width,
                H = (double)preCalculations[0],
                OffsetBot = (double)preCalculations[1],
                OffsetTop = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble(),
                ZBot = (doc.GetElement(LvlIds.Bot) as Level).Elevation + (double)preCalculations[1],
                ZTop = (doc.GetElement(LvlIds.Bot) as Level).Elevation + (double)preCalculations[1] + (double)preCalculations[0]
            };

            preCalculations.Add((Elem.Location as LocationCurve).Curve.CreateTransformed(Transform.CreateTranslation(Dirs.Z * (Dims.OffsetBot - BasePoint.Position.Z))));
            Origins = new ControlPoints
            {
                CenterStartBottom = (preCalculations[2] as Curve).GetEndPoint(0),
                CenterMiddleBottom = (preCalculations[2] as Curve).Evaluate(0.5, true),
                CenterEndBottom = (preCalculations[2] as Curve).GetEndPoint(1),
                CenterMiddleMiddle = (preCalculations[2] as Curve).Evaluate(0.5, true) + Dirs.Z * Dims.H / 2,
                CenterStartTop = (preCalculations[2] as Curve).GetEndPoint(0) + Dirs.Z * Dims.H,
                CenterMiddleTop = (preCalculations[2] as Curve).Evaluate(0.5, true) + Dirs.Z * Dims.H,
                CenterEndTop = (preCalculations[2] as Curve).GetEndPoint(1) + Dirs.Z * Dims.H
            };
            Lines = new ControlLines
            {
                CenterBot = Line.CreateBound(Origins.CenterStartBottom, Origins.CenterEndBottom),
                CenterTop = Line.CreateBound(Origins.CenterStartTop, Origins.CenterEndTop)
            };
            Outline = new Outline(wall.get_BoundingBox(null).Min, wall.get_BoundingBox(null).Max);*/

            // 1. Получаем явные параметры и геометрию
            double height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            double baseOffset = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble();
            double topOffset = wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble();

            Line locationLine = (wall.Location as LocationCurve)?.Curve as Line;
            if (locationLine == null)
                throw new InvalidOperationException("Wall location is not a line.");

            XYZ direction = locationLine.Direction; // Local X
            XYZ orientation = wall.Orientation;     // Local Y

            // 2. Инициализация уровней
            LvlIds = new LevelIds
            {
                Bot = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId(),
                Top = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId(),
                Base = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT).AsElementId()
            };

            // 3. Инициализация направлений
            Dirs = new Directions
            {
                X = direction,
                Y = orientation,
                Z = XYZ.BasisZ // Для прямых стен вертикаль всегда глобальная Z
            };

            // 4. Расчет размеров и координат
            // Получаем уровень основания для расчета абсолютных высот
            Level baseLevel = doc.GetElement(LvlIds.Bot) as Level;
            if (baseLevel == null)
                throw new InvalidOperationException("Base level not found.");

            double baseLevelElevation = baseLevel.Elevation;
            double zBot = baseLevelElevation + baseOffset;
            double zTop = zBot + height;

            Dims = new ControlDimensions
            {
                L = locationLine.Length,
                T = wall.Width,
                H = height,
                OffsetBot = baseOffset,
                OffsetTop = topOffset,
                ZBot = zBot,
                ZTop = zTop
            };

            // 5. Расчет контрольных точек
            // Базовая линия стены с учетом смещения по Z относительно BasePoint
            // В оригинале была сложная трансформация. Здесь упрощаем:
            // StartPoint линии локации + смещение по Z

            XYZ startPtRaw = locationLine.GetEndPoint(0);
            // Корректируем Z точки старта, если BasePoint.Position.Z отличается от 0 или уровня проекта
            // Обычно в Revit координаты уже глобальные, но если используется BasePoint:
            double zCorrection = Dirs.Z.Z * (baseOffset - BasePoint.Position.Z);
            // Примечание: Если BasePoint.Position.Z == 0, то zCorrection = baseOffset.
            // Если логика оригинала подразумевала сдвиг всей линии на вектор, делаем так:

            XYZ shiftVector = Dirs.Z * (baseOffset - BasePoint.Position.Z);
            XYZ startPt = startPtRaw + shiftVector;
            XYZ endPt = locationLine.GetEndPoint(1) + shiftVector;

            // Создаем линию основания для удобства вычислений
            Line baseLine = Line.CreateBound(startPt, endPt);

            Origins = new ControlPoints
            {
                CenterStartBottom = baseLine.GetEndPoint(0),
                CenterMiddleBottom = baseLine.Evaluate(0.5, true),
                CenterEndBottom = baseLine.GetEndPoint(1),

                CenterMiddleMiddle = baseLine.Evaluate(0.5, true) + Dirs.Z * (height / 2.0),

                CenterStartTop = baseLine.GetEndPoint(0) + Dirs.Z * height,
                CenterMiddleTop = baseLine.Evaluate(0.5, true) + Dirs.Z * height,
                CenterEndTop = baseLine.GetEndPoint(1) + Dirs.Z * height
            };

            Lines = new ControlLines
            {
                CenterBot = baseLine,
                CenterTop = Line.CreateBound(Origins.CenterStartTop, Origins.CenterEndTop)
            };

            // 6. BoundingBox
            Outline = new Outline(wall.get_BoundingBox(null).Min, wall.get_BoundingBox(null).Max);
        }

        // Методы
        public override void GetSolidData()
        {
            base.GetSolidData();
            GetExtendedFacesData();
        }
        public override void GetSolidData(Document targetDoc)
        {
            base.GetSolidData(targetDoc);
            GetExtendedFacesData();
        }
        public Solid GetStartSolid()
        {
            XYZ p0 = Origins.CenterStartBottom - Dirs.Y * Dims.T / 2;
            XYZ p1 = p0 + Dirs.Z * Dims.H;
            XYZ p3 = Origins.CenterEndBottom - Dirs.Y * Dims.T / 2;
            XYZ p2 = p3 + Dirs.Z * Dims.H;

            List<Curve> lines = new List<Curve>
            {
                Line.CreateBound(p0, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p0)
            };
            Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { CurveLoop.Create(lines) }, Dirs.Y, Dims.T);
            return solid;
        }
        public int GetParity(ReferenceIntersector intersector)
        {
            List<XYZ> rayOrigins = new List<XYZ>
            {
                Origins.CenterStartBottom + Dirs.X * 10 / 304.8,
                Origins.CenterMiddleBottom,
                Origins.CenterEndBottom - Dirs.X * 10 / 304.8,
                Origins.CenterStartTop + Dirs.X * 10 / 304.8,
                Origins.CenterMiddleTop,
                Origins.CenterEndTop + Dirs.X * 10 / 304.8
            };
            int maxN = 0;
            int maxI = 0;

            List<List<ReferenceWithContext>> wallBranches = new List<List<ReferenceWithContext>>();
            List<ReferenceWithContext> wallBranch;

            for (int i = 0; i < rayOrigins.Count; i++)
            {
                XYZ origin = rayOrigins[i];
                wallBranch = new List<ReferenceWithContext>();

                wallBranch.AddRange(intersector.Find(origin, -Dirs.Z));
                wallBranch.AddRange(intersector.Find(origin, Dirs.Z));

                if (wallBranch.Count > 0) wallBranches.Add(wallBranch);

                if (maxN < wallBranches.Count)
                {
                    maxN = wallBranches.Count;
                    maxI = i;
                }
            }

            wallBranch = wallBranches[maxI];

            List<Level> allLevels = (from rwc in wallBranch
                                     select doc.GetElement(doc.GetElement(rwc.GetReference()).LevelId) as Level).ToList();

            allLevels = allLevels.Distinct().OrderBy(level => Math.Round(level.Elevation * 304.8)).ToList();
            int n = -1 + allLevels.IndexOf(doc.GetElement(LvlIds.Bot) as Level) % 2 * 2;
            return n;
        }


        // Подклассы
        public class RegionCache : GeometryCache
        {
            // Поля
            GeometryWallCache geomWallCache;

            // Конструкторы
            public RegionCache(GeometryWallCache geomWallCache, XYZ startPoint, XYZ endPoint)
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
        public class IntersectionCache : GeometryCache
        {
            // Конструкторы
            public IntersectionCache(GeometryWallCache geometryWallCache, List<GeometryCache> attachedGeometryCaches, XYZ origin)
            {
                DimSets = new List<ControlDimensions>();
                XYZ yDir = geometryWallCache.Dirs.Y;
                double l = geometryWallCache.Dims.T;

                if (attachedGeometryCaches != null && attachedGeometryCaches.Count > 0)
                {
                    foreach (GeometryCache attachedGeomCache in attachedGeometryCaches)
                    {
                        ControlDimensions dims = new ControlDimensions();
                        if (attachedGeomCache is GeometryWallCache || attachedGeomCache.Dirs.Y.IsAlmostEqualTo(GeometryTools.VecABS(geometryWallCache.Dirs.X)))
                            dims.T = attachedGeomCache.Dims.T;
                        else
                            dims.T = attachedGeomCache.Dims.L;
                        if (attachedGeomCache is GeometryColumnCache)
                            dims.L = attachedGeomCache.Dims.L;
                        DimSets.Add(dims);
                    }

                    if (GeometryTools.VecABS(Dirs.X).IsAlmostEqualTo(GeometryTools.VecABS(attachedGeometryCaches.First().Dirs.X)))
                    {
                        yDir = attachedGeometryCaches.First().Dirs.Y;
                        l = attachedGeometryCaches.First().Dims.L;
                    }
                    else
                    {
                        yDir = attachedGeometryCaches.First().Dirs.Y;
                        l = attachedGeometryCaches.First().Dims.T;
                    }
                }

                Dirs = new Directions
                {
                    X = geometryWallCache.Dirs.X,
                    Y = yDir,
                    Z = geometryWallCache.Dirs.Z
                };

                Dims = new ControlDimensions
                {
                    L = l,
                    H = geometryWallCache.Dims.H,
                    T = geometryWallCache.Dims.T
                };

                Origins = new ControlPoints()
                {
                    CenterStartBottom = origin - Dirs.X * Dims.L / 2,
                    CenterMiddleBottom = origin,
                    CenterEndBottom = origin + Dirs.X * Dims.L / 2
                };
            }

            // Методы
            public override void GetSolidData()
            {
                List<List<int>> tokenSets =
                [
                    [0, -1],
                    [1, -1],
                    [1, 1],
                    [0, 1]
                ];

                List<XYZ> points = [];
                foreach (List<int> tokens in tokenSets)
                {
                    XYZ point = Origins.CenterMiddleBottom + Dirs.X * (Dims.L / 2) * tokens[0] + Dirs.Y * (Dims.T / 2) * tokens[1];
                    points.Add(point);
                }

                List<Curve> lines =
                [
                    Line.CreateBound(points[0], points[1]),
                    Line.CreateBound(points[1], points[2]),
                    Line.CreateBound(points[2], points[3]),
                    Line.CreateBound(points[3], points[0])
                ];

                Solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { CurveLoop.Create(lines) }, Dirs.Z, Dims.H);
            }
            public void GetOutline()
            {
                XYZ p0 = Origins.CenterMiddleBottom - GeometryTools.VecABS(Dirs.X) * Dims.L / 2 - GeometryTools.VecABS(Dirs.Y) * Dims.T / 2;
                XYZ p1 = Origins.CenterMiddleBottom + GeometryTools.VecABS(Dirs.X) * Dims.L / 2 + GeometryTools.VecABS(Dirs.Y) * Dims.T / 2 + Dirs.Z * Dims.H;
                Outline = new Outline(p0, p1);
            }

            // Свойства
            List<ControlDimensions> DimSets { get; set; }
        }
        public class EndCache : GeometryCache
        {
            // Конструкторы
            public EndCache(GeometryWallCache geometryWallCache, XYZ origin, XYZ xDir)
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
}
