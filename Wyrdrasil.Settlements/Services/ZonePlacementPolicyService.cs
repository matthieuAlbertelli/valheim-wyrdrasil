using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class ZonePlacementPolicyService
{
    private readonly ZoneDefinitionCatalog _definitionCatalog;

    public ZonePlacementPolicyService(ZoneDefinitionCatalog definitionCatalog)
    {
        _definitionCatalog = definitionCatalog;
    }

    public bool RequiresZone(ZoneSlotType slotType)
    {
        return !CanExistWithoutZone(ToDesignationKind(slotType));
    }

    public bool ShouldAssociateWithZone(ZoneSlotType slotType, ZoneType zoneType)
    {
        return CanAssociateWithZone(ToDesignationKind(slotType), zoneType);
    }

    public bool CanDesignateStandalone(SeatUsageType usageType)
    {
        return CanExistWithoutZone(ToDesignationKind(usageType));
    }

    public bool ShouldAssociateSeatWithZone(SeatUsageType usageType, ZoneType zoneType)
    {
        return CanAssociateWithZone(ToDesignationKind(usageType), zoneType);
    }

    public bool CanDesignateBedStandalone()
    {
        return CanExistWithoutZone(ZoneDesignationKind.Bed);
    }

    public bool ShouldAssociateBedWithZone(ZoneType zoneType)
    {
        return CanAssociateWithZone(ZoneDesignationKind.Bed, zoneType);
    }

    public bool CanDesignateCraftStationStandalone()
    {
        return CanExistWithoutZone(ZoneDesignationKind.CraftStation);
    }

    public bool ShouldAssociateCraftStationWithZone(ZoneType zoneType)
    {
        return CanAssociateWithZone(ZoneDesignationKind.CraftStation, zoneType);
    }

    private bool CanExistWithoutZone(ZoneDesignationKind designationKind)
    {
        return designationKind != ZoneDesignationKind.InnkeeperSlot;
    }

    private bool CanAssociateWithZone(ZoneDesignationKind designationKind, ZoneType zoneType)
    {
        return _definitionCatalog.TryGetDefinition(zoneType, out var definition) && definition.SupportsDesignation(designationKind);
    }

    private static ZoneDesignationKind ToDesignationKind(ZoneSlotType slotType)
    {
        return slotType switch
        {
            ZoneSlotType.Innkeeper => ZoneDesignationKind.InnkeeperSlot,
            ZoneSlotType.Seat => ZoneDesignationKind.PublicSeat,
            _ => ZoneDesignationKind.PublicSeat
        };
    }

    private static ZoneDesignationKind ToDesignationKind(SeatUsageType usageType)
    {
        return usageType switch
        {
            SeatUsageType.Public => ZoneDesignationKind.PublicSeat,
            SeatUsageType.Reserved => ZoneDesignationKind.ReservedSeat,
            _ => ZoneDesignationKind.PublicSeat
        };
    }
}
