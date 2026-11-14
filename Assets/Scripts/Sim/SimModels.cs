using System;
using System.Collections.Generic;

namespace Outpost.Sim
{
    public enum CrewRole
    {
        Medic,
        Engineer,
        Pilot,
        Scientist
    }

    public enum CrewActionType
    {
        CleanPanels,
        RepairSystem,
        ReinforceShielding,
        RestMeal,
        TreatCrew,
        RationSupplies
    }

    public enum SystemType
    {
        Scrubber,
        Recycler,
        Crops
    }

    public enum MissionStatus
    {
        InProgress,
        Won,
        Lost
    }

    public enum MissionGrade
    {
        S,
        A,
        B,
        C,
        F
    }

    public enum CrisisType
    {
        PowerBlackout,
        OxygenDepleted,
        WaterDepleted,
        FoodDepleted,
        RadiationExposure,
        FreezingCold,
        EquipmentFailure,
        DustStorm,
        SolarFlare,
        ShieldingBreach
    }

    [Serializable]
    public class CrewMember
    {
        public int Id;
        public string NameKey;
        public CrewRole Role;
        public float Health;
        public float Morale;
        public float CumulativeDoseMsv;
        public bool IsFallenCommander;

        public bool IsAlive => Health > 0f;
        public bool CanWork => IsAlive && !IsFallenCommander;
    }

    [Serializable]
    public struct PowerAllocation
    {
        public float LifeSupport;
        public float Greenhouse;
        public float Heating;
        public float Comms;

        public static PowerAllocation Default => new PowerAllocation
        {
            LifeSupport = 0.85f,
            Greenhouse = 0.75f,
            Heating = 0.75f,
            Comms = 0.50f
        };

        public PowerAllocation Clamped()
        {
            return new PowerAllocation
            {
                LifeSupport = SimMath.Clamp01(LifeSupport),
                Greenhouse = SimMath.Clamp01(Greenhouse),
                Heating = SimMath.Clamp01(Heating),
                Comms = SimMath.Clamp01(Comms)
            };
        }
    }

    [Serializable]
    public class SolForecast
    {
        public float PredictedGeneration;
        public float PredictedDemand;
        public float NetPowerDelta;
        public float PredictedBatteryAfter;
        public bool WillBlackout;
        public float NetOxygenDelta;
        public float NetWaterDelta;
        public float NetFoodDelta;
        public float PredictedDoseMsv;
        public bool IsHeatingCold;
        public string UrgentAlertKey;
        public int UrgentSeverity; // 0 = nominal, 1 = caution, 2 = critical
    }

    [Serializable]
    public class NasaSource
    {
        public string Id;
        public string TitleKey;
        public string AgencyKey;
        public string UrlText;
        public string FactKey;
    }

    [Serializable]
    public class DebriefCard
    {
        public int Sol;
        public string EventId;
        public string TitleKey;
        public string ChoiceLabelKey;
        public string OutcomeKey;
        public string FactKey;
        public string SourceId;
    }

    public static class SimMath
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float Clamp100(float value) => Clamp(value, 0f, 100f);
    }
}
