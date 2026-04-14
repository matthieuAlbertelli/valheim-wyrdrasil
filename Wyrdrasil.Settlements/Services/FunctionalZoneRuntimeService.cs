using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class FunctionalZoneRuntimeService
{
    private readonly ZoneDefinitionCatalog _definitionCatalog;
    private readonly FunctionalZoneService _zoneService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;

    public FunctionalZoneRuntimeService(
        ZoneDefinitionCatalog definitionCatalog,
        FunctionalZoneService zoneService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService)
    {
        _definitionCatalog = definitionCatalog;
        _zoneService = zoneService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
    }

    public bool TryGetRuntimeState(int zoneId, out FunctionalZoneRuntimeState runtimeState)
    {
        var zone = _zoneService.Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        if (zone == null)
        {
            runtimeState = null!;
            return false;
        }

        runtimeState = Evaluate(zone);
        return true;
    }

    public FunctionalZoneRuntimeState Evaluate(FunctionalZoneData zone)
    {
        if (!_definitionCatalog.TryGetDefinition(zone.ZoneType, out var definition))
        {
            return new FunctionalZoneRuntimeState(
                zone.Id,
                zone.BuildingId,
                zone.ZoneType,
                ZoneFunctionalState.Invalid,
                ZoneCapability.None,
                0,
                0,
                0,
                0,
                0,
                0,
                new Dictionary<ZoneInterpretationKind, IReadOnlyList<int>>(),
                new[]
                {
                    new ZoneValidationDiagnostic(
                        ZoneValidationDiagnosticCode.UnsupportedZoneType,
                        $"Zone type '{zone.ZoneType}' has no registered functional definition.")
                });
        }

        var publicSeatIds = _seatService.Seats
            .Where(candidate => candidate.ZoneId == zone.Id && candidate.UsageType == SeatUsageType.Public)
            .Select(candidate => candidate.Id)
            .ToArray();

        var reservedSeatIds = _seatService.Seats
            .Where(candidate => candidate.ZoneId == zone.Id && candidate.UsageType == SeatUsageType.Reserved)
            .Select(candidate => candidate.Id)
            .ToArray();

        var bedIds = _bedService.Beds
            .Where(candidate => candidate.ZoneId == zone.Id)
            .Select(candidate => candidate.Id)
            .ToArray();

        var craftStationIds = _craftStationService.CraftStations
            .Where(candidate => candidate.ZoneId == zone.Id)
            .Select(candidate => candidate.Id)
            .ToArray();

        var innkeeperSlots = _slotService.Slots
            .Where(candidate => candidate.ZoneId == zone.Id && candidate.SlotType == ZoneSlotType.Innkeeper)
            .ToArray();

        var interpretedDesignationIdsByKind = BuildInterpretations(
            definition,
            publicSeatIds,
            reservedSeatIds,
            bedIds,
            craftStationIds,
            innkeeperSlots.Select(candidate => candidate.Id).ToArray());

        var configuredDiagnostics = new List<ZoneValidationDiagnostic>();
        var configuredSatisfied = ValidateRequirements(
            definition.ConfiguredRequirements,
            publicSeatIds.Length,
            reservedSeatIds.Length,
            bedIds.Length,
            craftStationIds.Length,
            innkeeperSlots.Length,
            innkeeperSlots.Count(candidate => candidate.AssignedRegisteredNpcId.HasValue),
            configuredDiagnostics);

        ZoneFunctionalState state;
        var diagnostics = new List<ZoneValidationDiagnostic>();

        if (!configuredSatisfied)
        {
            state = ZoneFunctionalState.Draft;
            diagnostics.AddRange(configuredDiagnostics);
        }
        else
        {
            var staffedDiagnostics = new List<ZoneValidationDiagnostic>();
            var staffedSatisfied = ValidateRequirements(
                definition.StaffedRequirements,
                publicSeatIds.Length,
                reservedSeatIds.Length,
                bedIds.Length,
                craftStationIds.Length,
                innkeeperSlots.Length,
                innkeeperSlots.Count(candidate => candidate.AssignedRegisteredNpcId.HasValue),
                staffedDiagnostics);

            if (!staffedSatisfied)
            {
                state = ZoneFunctionalState.Configured;
                diagnostics.AddRange(staffedDiagnostics);
            }
            else
            {
                var operationalDiagnostics = new List<ZoneValidationDiagnostic>();
                var operationalSatisfied = ValidateRequirements(
                    definition.OperationalRequirements,
                    publicSeatIds.Length,
                    reservedSeatIds.Length,
                    bedIds.Length,
                    craftStationIds.Length,
                    innkeeperSlots.Length,
                    innkeeperSlots.Count(candidate => candidate.AssignedRegisteredNpcId.HasValue),
                    operationalDiagnostics);

                if (!operationalSatisfied)
                {
                    state = ZoneFunctionalState.Staffed;
                    diagnostics.AddRange(operationalDiagnostics);
                }
                else
                {
                    state = definition.StaffedRequirements.IsEmpty ? ZoneFunctionalState.Operational : ZoneFunctionalState.Operational;
                }
            }
        }

        return new FunctionalZoneRuntimeState(
            zone.Id,
            zone.BuildingId,
            zone.ZoneType,
            state,
            definition.Capabilities,
            publicSeatIds.Length,
            reservedSeatIds.Length,
            bedIds.Length,
            craftStationIds.Length,
            innkeeperSlots.Length,
            innkeeperSlots.Count(candidate => candidate.AssignedRegisteredNpcId.HasValue),
            interpretedDesignationIdsByKind,
            diagnostics);
    }

    public bool IsSeatEligibleForPublicSocialUse(RegisteredSeatData seat)
    {
        if (seat.UsageType != SeatUsageType.Public || !seat.ZoneId.HasValue)
        {
            return false;
        }

        if (!TryGetRuntimeState(seat.ZoneId.Value, out var runtimeState))
        {
            return false;
        }

        return runtimeState.State >= ZoneFunctionalState.Configured &&
               runtimeState.HasCapability(ZoneCapability.PublicSeating) &&
               runtimeState.HasInterpretation(ZoneInterpretationKind.PublicSocialSeating, seat.Id);
    }

    private static Dictionary<ZoneInterpretationKind, IReadOnlyList<int>> BuildInterpretations(
        ZoneTypeDefinition definition,
        IReadOnlyList<int> publicSeatIds,
        IReadOnlyList<int> reservedSeatIds,
        IReadOnlyList<int> bedIds,
        IReadOnlyList<int> craftStationIds,
        IReadOnlyList<int> innkeeperSlotIds)
    {
        var interpretedIdsByKind = new Dictionary<ZoneInterpretationKind, HashSet<int>>();
        foreach (var rule in definition.DefaultInterpretationRules)
        {
            IReadOnlyList<int> sourceIds = rule.SourceDesignationKind switch
            {
                ZoneDesignationKind.PublicSeat => publicSeatIds,
                ZoneDesignationKind.ReservedSeat => reservedSeatIds,
                ZoneDesignationKind.Bed => bedIds,
                ZoneDesignationKind.CraftStation => craftStationIds,
                ZoneDesignationKind.InnkeeperSlot => innkeeperSlotIds,
                _ => System.Array.Empty<int>()
            };

            if (!interpretedIdsByKind.TryGetValue(rule.InterpretationKind, out var set))
            {
                set = new HashSet<int>();
                interpretedIdsByKind[rule.InterpretationKind] = set;
            }

            foreach (var sourceId in sourceIds)
            {
                set.Add(sourceId);
            }
        }

        return interpretedIdsByKind.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<int>)pair.Value.OrderBy(id => id).ToArray());
    }

    private static bool ValidateRequirements(
        ZoneRequirementProfile requirements,
        int publicSeatCount,
        int reservedSeatCount,
        int bedCount,
        int craftStationCount,
        int innkeeperSlotCount,
        int assignedInnkeeperCount,
        ICollection<ZoneValidationDiagnostic> diagnostics)
    {
        var valid = true;

        if (publicSeatCount < requirements.MinimumPublicSeatCount)
        {
            valid = false;
            diagnostics.Add(new ZoneValidationDiagnostic(
                ZoneValidationDiagnosticCode.MissingPublicSeats,
                $"Missing public seats: requires {requirements.MinimumPublicSeatCount}, found {publicSeatCount}."));
        }

        if (reservedSeatCount < requirements.MinimumReservedSeatCount)
        {
            valid = false;
            diagnostics.Add(new ZoneValidationDiagnostic(
                ZoneValidationDiagnosticCode.MissingReservedSeats,
                $"Missing reserved seats: requires {requirements.MinimumReservedSeatCount}, found {reservedSeatCount}."));
        }

        if (bedCount < requirements.MinimumBedCount)
        {
            valid = false;
            diagnostics.Add(new ZoneValidationDiagnostic(
                ZoneValidationDiagnosticCode.MissingBeds,
                $"Missing beds: requires {requirements.MinimumBedCount}, found {bedCount}."));
        }

        if (craftStationCount < requirements.MinimumCraftStationCount)
        {
            valid = false;
            diagnostics.Add(new ZoneValidationDiagnostic(
                ZoneValidationDiagnosticCode.MissingCraftStations,
                $"Missing craft stations: requires {requirements.MinimumCraftStationCount}, found {craftStationCount}."));
        }

        if (innkeeperSlotCount < requirements.MinimumInnkeeperSlotCount)
        {
            valid = false;
            diagnostics.Add(new ZoneValidationDiagnostic(
                ZoneValidationDiagnosticCode.MissingInnkeeperSlot,
                $"Missing innkeeper slot: requires {requirements.MinimumInnkeeperSlotCount}, found {innkeeperSlotCount}."));
        }

        if (assignedInnkeeperCount < requirements.MinimumAssignedInnkeeperCount)
        {
            valid = false;
            diagnostics.Add(new ZoneValidationDiagnostic(
                ZoneValidationDiagnosticCode.MissingAssignedInnkeeper,
                $"Missing assigned innkeeper: requires {requirements.MinimumAssignedInnkeeperCount}, found {assignedInnkeeperCount}."));
        }

        return valid;
    }
}
