namespace Wyrdrasil.Settlements.Tool;

[System.Flags]
public enum ZoneCapability
{
    None = 0,
    SocialGathering = 1 << 0,
    PublicSeating = 1 << 1,
    HospitalityService = 1 << 2,
    PrivateRest = 1 << 3,
    CraftWork = 1 << 4,
    FoodPreparation = 1 << 5,
    FoodStorage = 1 << 6,
    GuardAssembly = 1 << 7
}
