using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryAnchorPolicyService
{
    public bool RequiresZone(ZoneSlotType slotType)
    {
        return slotType switch
        {
            ZoneSlotType.Innkeeper => true,
            _ => true
        };
    }

    public bool IsZoneTypeAllowed(ZoneSlotType slotType, ZoneType zoneType)
    {
        return slotType switch
        {
            ZoneSlotType.Innkeeper => zoneType == ZoneType.Tavern,
            ZoneSlotType.Seat => true,
            _ => false
        };
    }

    public bool RequiresZone(SeatUsageType usageType)
    {
        return true;
    }

    public bool IsZoneTypeAllowed(SeatUsageType usageType, ZoneType zoneType)
    {
        return usageType switch
        {
            SeatUsageType.Public => zoneType is ZoneType.Tavern or ZoneType.Courtyard or ZoneType.Kitchen,
            SeatUsageType.Reserved => true,
            _ => false
        };
    }

    public bool RequiresZoneForBed()
    {
        return true;
    }

    public bool IsZoneTypeAllowedForBed(ZoneType zoneType)
    {
        return zoneType is ZoneType.Bedroom or ZoneType.Barracks;
    }
}
