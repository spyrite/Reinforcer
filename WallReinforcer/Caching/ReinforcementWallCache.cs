using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Resources;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RevitOSA.WallReinforcer.Assistants;

using Line = Autodesk.Revit.DB.Line;
using Transform = Autodesk.Revit.DB.Transform;
using View = Autodesk.Revit.DB.View;
using Parameter = Autodesk.Revit.DB.Parameter;
using AnnSettings = RevitOSA.WallReinforcer.Properties.Annotations;
using ReinfSettings = RevitOSA.WallReinforcer.Properties.Reinforcement;
using ModelSettings = RevitOSA.WallReinforcer.Properties.Modelling;

using ReinforcementData = RevitOSA.WallReinforcer.Resources.ReinforcementData;



#if REVIT2023 || REVIT2024 || REVIT2025
using static Autodesk.Revit.DB.SpecTypeId;
#endif

using Autodesk.Revit.DB.Structure;

using RevitOSA.WallReinforcer.Resources1P;
using static RevitOSA.WallReinforcer.Resources1P.RevitParameters;
using RevitOSA.WallReinforcer.Tools;



namespace RevitOSA.WallReinforcer.Caching
{
    public partial class ReinforcementWallCache : ReinforcementCache
    {
        // Конструкторы
        public ReinforcementWallCache(Wall wall) : base(wall)
        {
            Elem = wall;
            DataX = GetPrimaryDataX();
        }

        // Методы
        private ReinforcementData GetPrimaryDataX()
        {
            ReinforcementData reinfData = new ReinforcementData()
            {
                RClass = ReinfSettings.Default.reinf_RClass,
            };

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_X_D) && Elem.GetParameters(pp_Reinf_X_D).First().AsDouble() > 0)
            {
                reinfData.D = Elem.GetParameters(pp_Reinf_X_D).First().AsDouble();
                reinfData.Bend = ReinforcementTools.ComputeBendDiameter(reinfData.D, reinfData.RClass);
                reinfData.BarType = RebarBarTypeAssistant.GetOrCreateRebarBarType(doc, reinfData.D, 0, reinfData.RClass, false);
            }
            else if (ReinfSettings.Default.reinf_Walls_X_Type_IntId != -1)
            {
#if REVIT2023
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif REVIT2024 || REVIT2025
                reinfData.BarType = doc.GetElement(new ElementId((long)ReinfSettings.Default.reinf_Walls_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#else
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarDiameter;
#endif
                reinfData.Bend = reinfData.BarType.StandardBendDiameter;
            }

            if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_X_Step) && Elem.GetParameters(pp_Reinf_X_Step).First().AsDouble() > 0)
                reinfData.Step = Elem.GetParameters(pp_Reinf_X_Step).First().AsDouble();
            else reinfData.Step = ReinfSettings.Default.reinf_Walls_X_Step / 304.8;

            return reinfData;
        }
    }
}
