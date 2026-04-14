using Autodesk.Revit.DB;
using RevitOSA.CoreMain.FB;
using RevitOSA.CoreMain.Assistants;
using System;
using System.Resources;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Line = Autodesk.Revit.DB.Line;
using Transform = Autodesk.Revit.DB.Transform;
using View = Autodesk.Revit.DB.View;
using Parameter = Autodesk.Revit.DB.Parameter;
using AnnSettings = RevitOSA.CoreSettings.Properties.Annotations;
using ReinfSettings = RevitOSA.CoreSettings.Properties.Reinforcement;
using ModelSettings = RevitOSA.CoreSettings.Properties.Modelling;
using Autodesk.Revit.DB.Architecture;

#if R2023 || R2024 || R2025
using static Autodesk.Revit.DB.SpecTypeId;
#endif

using Autodesk.Revit.DB.Structure;

#if COMPANY_FP
using RevitOSA.CoreSettings.ResourcesFP;
using static RevitOSA.CoreSettings.ResourcesFP.RevitParameters;

#elif COMPANY_OLP
using static RevitOSA.CoreSettings.ResourcesOLP.RevitParameters;
using RevitOSA.CoreSettings.ResourcesOLP;

#else
#endif

namespace RevitOSA.CoreMain.Caching
{
    public class ReinforcementWallCache : ReinforcementCache
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
#if R2023
                reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_X_Type_IntId)) as RebarBarType;
                reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif R2024 || R2025
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

        // Подклассы
        public class RegionCache : ReinforcementCache
        {
            // Конструкторы
            public RegionCache(Wall wall) : base(wall)
            {
                Elem = wall;
                DataY = GetPrimaryDataY();
            }

            // Методы
            private ReinforcementData GetPrimaryDataY()
            {
                ReinforcementData reinfData = new ReinforcementData()
                {
                    RClass = ReinfSettings.Default.reinf_RClass,
                };

                if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_Y_D) && Elem.GetParameters(pp_Reinf_Y_D).First().AsDouble() > 0)
                {
                    reinfData.D = Elem.GetParameters(pp_Reinf_Y_D).First().AsDouble();
                    reinfData.Bend = ReinforcementTools.ComputeBendDiameter(reinfData.D, reinfData.RClass);
                    reinfData.BarType = RebarBarTypeAssistant.GetOrCreateRebarBarType(doc, reinfData.D, 0, reinfData.RClass, false);
                }
                else if (ReinfSettings.Default.reinf_Walls_Y_Type_IntId != -1)
                {
#if R2023
                    reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif R2024 || R2025
                    reinfData.BarType = doc.GetElement(new ElementId((long)ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarNominalDiameter;
#else
                    reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarDiameter;
#endif
                    reinfData.Bend = reinfData.BarType.StandardBendDiameter;
                }

                if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_Y_Step) && Elem.GetParameters(pp_Reinf_Y_Step).First().AsDouble() > 0)
                    reinfData.Step = Elem.GetParameters(pp_Reinf_Y_Step).First().AsDouble();
                else reinfData.Step = ReinfSettings.Default.reinf_Walls_Y_Step / 304.8;

                return reinfData;
            }

            // Свойства
        }
        public class IntersectionCache : ReinforcementCache
        {
            //Конструкторы
            public IntersectionCache(Wall wall) : base(wall)
            {
                Elem = wall;
                DataY = GetPrimaryDataY();
                PlaceRebars = false;
            }

            //Методы
            private ReinforcementData GetPrimaryDataY()
            {
                ReinforcementData reinfData = new ReinforcementData()
                {
                    RClass = ReinfSettings.Default.reinf_RClass
                };

                if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_Edge_D) && Elem.GetParameters(pp_Reinf_Edge_D).First().AsDouble() > 0)
                {
                    reinfData.D = Elem.GetParameters(pp_Reinf_Edge_D).First().AsDouble();
                    reinfData.Bend = ReinforcementTools.ComputeBendDiameter(reinfData.D, reinfData.RClass);
                    reinfData.BarType = RebarBarTypeAssistant.GetOrCreateRebarBarType(doc, reinfData.D, 0, reinfData.RClass, false);
                }
                else if (ReinfSettings.Default.reinf_Walls_Y_Type_IntId != -1)
                {
#if R2023
                    reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif R2024 || R2025
                    reinfData.BarType = doc.GetElement(new ElementId((long)ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarNominalDiameter;
#else
                    reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_X_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarDiameter;
#endif
                    reinfData.Bend = reinfData.BarType.StandardBendDiameter;
                }

                if (ReinfSettings.Default.reinf_Walls_Y_Edge_Step > 0)
                    reinfData.Step = ReinfSettings.Default.reinf_Walls_Y_Edge_Step / 304.8;

                else if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_X_Step) && Elem.GetParameters(pp_Reinf_X_Step).First().AsDouble() > 0)
                    reinfData.Step = Elem.GetParameters(pp_Reinf_X_Step).First().AsDouble();

                else
                    reinfData.Step = 50 / 304.8;

                return reinfData;
            }

            //Свойства
            public bool PlaceRebars { get; set; }
        }
        public class EndCache : ReinforcementCache
        {
            //Конструкторы
            public EndCache(Wall wall) : base(wall)
            {
                Elem = wall;
                DataY = GetPrimaryDataY();
            }

            //Методы
            private ReinforcementData GetPrimaryDataY()
            {
                ReinforcementData reinfData = new ReinforcementData()
                {
                    RClass = ReinfSettings.Default.reinf_RClass
                };

                if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_Edge_D) && Elem.GetParameters(pp_Reinf_Edge_D).First().AsDouble() > 0)
                {
                    reinfData.D = Elem.GetParameters(pp_Reinf_Edge_D).First().AsDouble();
                    reinfData.Bend = ReinforcementTools.ComputeBendDiameter(reinfData.D, reinfData.RClass);
                    reinfData.BarType = RebarBarTypeAssistant.GetOrCreateRebarBarType(doc, reinfData.D, 0, reinfData.RClass, false);
                }
                else if (ReinfSettings.Default.reinf_Walls_Y_Type_IntId != -1)
                {
#if R2023
                    reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarNominalDiameter;
#elif R2024 || R2025
                    reinfData.BarType = doc.GetElement(new ElementId((long)ReinfSettings.Default.reinf_Walls_Y_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarNominalDiameter;
                
#else
                    reinfData.BarType = doc.GetElement(new ElementId(ReinfSettings.Default.reinf_Walls_X_Type_IntId)) as RebarBarType;
                    reinfData.D = reinfData.BarType.BarDiameter;
#endif

                    reinfData.Bend = reinfData.BarType.StandardBendDiameter;
                }

                if (ReinfSettings.Default.reinf_Walls_Y_Edge_Step > 0)
                    reinfData.Step = ReinfSettings.Default.reinf_Walls_Y_Edge_Step / 304.8;

                else if (ElementParametersAssistant.IsParameterExistAndHasValue(Elem, pp_Reinf_X_Step) && Elem.GetParameters(pp_Reinf_X_Step).First().AsDouble() > 0)
                    reinfData.Step = Elem.GetParameters(pp_Reinf_X_Step).First().AsDouble();

                else
                    reinfData.Step = 50 / 304.8;

                return reinfData;
            }
        }

    }
}
