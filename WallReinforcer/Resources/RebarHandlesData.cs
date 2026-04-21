using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitOSA.WallReinforcer.Resources
{
    public class RebarHandles
    {
        private readonly Rebar _rebar;
        private readonly List<RebarConstrainedHandle> _handles;

        public RebarHandles (Rebar rebar)
        {
            _rebar = rebar;
            _handles = [.. _rebar.GetRebarConstraintsManager().GetAllHandles()];
            Bottom = _handles.Find(h => h.GetHandleType() == RebarHandleType.StartOfBar);
            Top = _handles.Find(h => h.GetHandleType() == RebarHandleType.EndOfBar);
            Plane = _handles.Find(h => h.GetHandleType() == RebarHandleType.RebarPlane);
            S1 = _handles.Find(h => h.GetHandleName() == "Сегмент стержня 1");
            S2 = _handles.Find(h => h.GetHandleName() == "Сегмент стержня 2");
            S2 = _handles.Find(h => h.GetHandleName() == "Сегмент стержня 3");
        }

        //Свойства
        public RebarConstrainedHandle Bottom { get; private set; }
        public RebarConstrainedHandle Top { get; private set; }
        public RebarConstrainedHandle Plane { get; private set; }
        public RebarConstrainedHandle S1 { get; private set; }
        public RebarConstrainedHandle S2 { get; private set; }
        public RebarConstrainedHandle S3 { get; private set; }
    }
}
