using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.Linq;

using RevitOSA.WallReinforcer.Assistants;
using RevitOSA.WallReinforcer.Resources;

using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;
using ReinforcementData = RevitOSA.WallReinforcer.Resources.ReinforcementData;
using RevitOSA.WallReinforcer.Tools;

namespace RevitOSA.WallReinforcer.Caching
{
    public class ReinforcementVoidCache : ReinforcementCache
    {
        //Конструкторы
        public ReinforcementVoidCache(FamilyInstance voidInst) : base(voidInst)
        {
            Void = voidInst;
        }

        //Методы
        public void GetDataXSet(GeometryFamilyInstanceCache geom)
        {
            if (geom.Solid == null)
            {
                if (Void.Host is Wall) geom.GetWallVoidSolid(HostCache as WallCache);
            }
            if (geom.Solid != null)
            {
                List<ReinforcementData> reinfDataXSet = new List<ReinforcementData>();
                List<PlanarFace> faces = new List<PlanarFace>();
                faces.AddRange(geom.Faces.Top);
                faces.AddRange(geom.Faces.Bottom);
                foreach (PlanarFace face in faces)
                {
                    ReinforcementData reinfData = GetPrimaryDataX();
                    reinfData.Dir = face.FaceNormal.Normalize();
                    reinfData.StartPoint = face.Project(geom.Origins.CenterMiddleMiddle).XYZPoint + reinfData.Dir * 50 / 304.8;
                    reinfDataXSet.Add(reinfData);
                }
                DataXSet = reinfDataXSet;
            }
        }
        public void GetDataYSet(GeometryFamilyInstanceCache geom)
        {
            if (geom.Solid == null)
            {
                if (Void.Host is Wall) geom.GetWallVoidSolid(HostCache as WallCache);
            }
            if (geom.Solid != null)
            {
                List<ReinforcementData> reinfDataYSet = new List<ReinforcementData>();
                List<PlanarFace> faces = new List<PlanarFace>();
                faces.AddRange(geom.Faces.Left);
                faces.AddRange(geom.Faces.Right);
                foreach (PlanarFace face in faces)
                {
                    ReinforcementData reinfData = GetPrimaryDataY();
                    reinfData.Dir = face.FaceNormal.Normalize();
                    reinfData.StartPoint = face.Project(geom.Origins.CenterMiddleMiddle).XYZPoint + reinfData.Dir * 50 / 304.8;
                    reinfDataYSet.Add(reinfData);
                }
                DataYSet = reinfDataYSet;
            }
        }

        private ReinforcementData GetPrimaryDataX()
        {
            ReinforcementData reinfData = new ReinforcementData()
            {
                RClass = ReinfSettings.Default.reinf_RClass,
                InRunningMeters = false
            };

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Void, pp_Reinf_X_D) && Void.GetParameters(pp_Reinf_X_D).First().AsDouble() > 0)
            {
                reinfData.D = Void.GetParameters(pp_Reinf_X_D).First().AsDouble();
                reinfData.Bend = ReinforcementTools.ComputeBendDiameter(reinfData.D, reinfData.RClass);
                reinfData.BarType = RebarBarTypeAssistant.GetOrCreateRebarBarType(doc, reinfData.D, 0, reinfData.RClass, false);
            }
            else if (ReinfSettings.Default.reinf_Voids_X_Type_IntId != -1)


            {
#if REVIT2023
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Voids_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif REVIT2024 || REVIT2025
                reinfData.BarType = doc.GetElement(new ElementId((long)ReinfSettings.Default.reinf_Voids_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#else
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Voids_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarDiameter;
#endif
                reinfData.Bend = reinfData.BarType.StandardBendDiameter;
            }

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Void, pp_Reinf_X_Step) && Void.GetParameters(pp_Reinf_X_Step).First().AsDouble() > 0)
                reinfData.Step = Void.GetParameters(pp_Reinf_X_Step).First().AsDouble();
            else reinfData.Step = ReinfSettings.Default.reinf_Voids_X_Step / 304.8;

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Void, pp_Reinf_X_N) && Void.GetParameters(pp_Reinf_X_N).First().AsInteger() > 0)
                reinfData.N = Void.GetParameters(pp_Reinf_X_N).First().AsInteger();
            else reinfData.N = ReinfSettings.Default.reinf_Voids_X_N;

            return reinfData;
        }
        private ReinforcementData GetPrimaryDataY()
        {
            ReinforcementData reinfData = new ReinforcementData()
            {
                RClass = ReinfSettings.Default.reinf_RClass,
                InRunningMeters = false
            };

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Void, pp_Reinf_Y_D) && Void.GetParameters(pp_Reinf_Y_D).First().AsDouble() > 0)
            {
                reinfData.D = Void.GetParameters(pp_Reinf_Y_D).First().AsDouble();
                reinfData.Bend = ReinforcementTools.ComputeBendDiameter(reinfData.D, reinfData.RClass);
                reinfData.BarType = RebarBarTypeAssistant.GetOrCreateRebarBarType(doc, reinfData.D, 0, reinfData.RClass, false);
            }
            else if (ReinfSettings.Default.reinf_Voids_Y_Type_IntId != -1)
            {
#if REVIT2023
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Voids_Y_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif REVIT2024 || REVIT2025
                reinfData.BarType = doc.GetElement(new ElementId((long)ReinfSettings.Default.reinf_Voids_Y_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#else
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Voids_Y_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarDiameter;
#endif
                reinfData.Bend = reinfData.BarType.StandardBendDiameter;
            }

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Void, pp_Reinf_Y_Step) && Void.GetParameters(pp_Reinf_Y_Step).First().AsDouble() > 0)
                reinfData.Step = Void.GetParameters(pp_Reinf_Y_Step).First().AsDouble();
            else reinfData.Step = ReinfSettings.Default.reinf_Voids_Y_Step / 304.8;

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Void, pp_Reinf_Y_N) && Void.GetParameters(pp_Reinf_Y_N).First().AsInteger() > 0)
                reinfData.N = Void.GetParameters(pp_Reinf_Y_N).First().AsInteger();
            else reinfData.N = ReinfSettings.Default.reinf_Voids_Y_N;

            return reinfData;
        }

        //Свойства
        public FamilyInstance Void { get; set; }
        public List<ReinforcementData> DataXSet { get; set; }
        public List<ReinforcementData> DataYSet { get; set; }
    }
}
