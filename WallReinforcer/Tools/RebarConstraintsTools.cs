using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using RevitOSA.WallReinforcer.Caching;
using RevitOSA.WallReinforcer.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static RevitOSA.WallReinforcer.Caching.GeometryCache;



namespace RevitOSA.WallReinforcer.Tools
{
    public static class RebarConstraintsTools
    {
        private static RebarConstraintsManager conMan;
        private static List<Tuple<RebarConstrainedHandle, RebarConstraint>> constraints;
        private static RebarHandles handles;
        private static RebarCache rebarCache;

        public static void SetVerticalRebarConstarints(this Rebar rebar, ElemFaces hostFaces, ElemFaces attachFaces, XYZ xDir1, XYZ xDir2, double DX, double DY1, double DY2)
        {
            Initialize(rebar);
            rebarCache = new RebarCache(rebar);

            if (handles.Top != null) constraints.Add(new(handles.Top,
                    conMan.GetConstraintCandidatesForHandle(handles.Top, rebarCache.GetNearestFace(hostFaces.Top, 1).Reference)
                    .FirstOrDefault(c => c.IsFixedDistanceToHostFace())));
            if (handles.Bottom != null) constraints.Add(new(handles.Bottom,
                    conMan.GetConstraintCandidatesForHandle(handles.Bottom, rebarCache.GetNearestFace(hostFaces.Bottom, 0).Reference)
                    .FirstOrDefault(c => c.IsFixedDistanceToHostFace())));
            if (handles.Plane != null)
            {
                constraints.Add(new(handles.Plane,
                    conMan.GetConstraintCandidatesForHandle(handles.Plane, rebarCache.GetNearestFace(hostFaces.SideLong, 0).Reference)
                    .FirstOrDefault(c => c.IsToCover())));
                constraints.Last().Item2.SetDistanceToTargetCover(-DX);
            }
            if (handles.S1 != null)
            {
                constraints.Add(new(handles.S1,
                    conMan.GetConstraintCandidatesForHandle(handles.S1, rebarCache.GetNearestFace(xDir1.IsAlmostEqualTo(xDir2)
                    ? attachFaces.SideShort
                    : attachFaces.SideLong, 0).Reference)
                    .FirstOrDefault(c => c.IsToCover())));
                constraints.Last().Item2.SetDistanceToTargetCover(-(50 / 304.8 - DY2 / 2 + DY1 / 2));
            }
            constraints.ForEach(c => conMan.SetPreferredConstraintForHandle(c.Item1, c.Item2));
        }

        public static void SetStirrupConstarints(this Rebar rebar, Tuple<Rebar, Rebar> vRebarPair, double thickness)
        {
            Initialize(rebar);

            if (handles.Top != null)
            {
                constraints.Add(new(handles.Top,
                    conMan.GetConstraintCandidatesForHandle(handles.Top, vRebarPair.Item1.Id)
                    .FirstOrDefault(c => c.IsToOtherRebar())));
                constraints.Last().Item2.SetDistanceToTargetCover(thickness*2);
            }
            if (handles.Plane != null)
            {
                constraints.Add(new(handles.Plane,
                    conMan.GetConstraintCandidatesForHandle(handles.Plane, vRebarPair.Item1.Id)
                    .FirstOrDefault(c => c.IsToOtherRebar())));
                constraints.Last().Item2.SetToUseClearBarSpacing(true);
                constraints.Last().Item2.SetDistanceToTargetCover(0);
            }
            if (handles.S1 != null)
            {
                constraints.Add(new(handles.S1,
                    conMan.GetConstraintCandidatesForHandle(handles.S1, vRebarPair.Item1.Id)
                    .FirstOrDefault(c => c.IsToOtherRebar())));
                constraints.Last().Item2.SetToUseClearBarSpacing(false);
                constraints.Last().Item2.SetDistanceToTargetCover(0);
            }
            if (handles.S2 != null)
            {
                constraints.Add(new(handles.S2,
                    conMan.GetConstraintCandidatesForHandle(handles.S2, vRebarPair.Item1.Id)
                    .FirstOrDefault(c => c.IsToOtherRebar())));
                constraints.Last().Item2.SetToUseClearBarSpacing(true);
                constraints.Last().Item2.SetDistanceToTargetCover(0);
            }
            if (handles.S3 != null)
            {
                constraints.Add(new(handles.S3,
                    conMan.GetConstraintCandidatesForHandle(handles.S3, vRebarPair.Item2.Id)
                    .FirstOrDefault(c => c.IsToOtherRebar())));
                constraints.Last().Item2.SetToUseClearBarSpacing(false);
                constraints.Last().Item2.SetDistanceToTargetCover(0);
            }

            constraints.ForEach(c => conMan.SetPreferredConstraintForHandle(c.Item1, c.Item2));
        }


        private static void Initialize(Rebar rebar)
        {
            conMan = rebar.GetRebarConstraintsManager();
            constraints = [];
            handles = new RebarHandles(rebar);
        }
    }
}
