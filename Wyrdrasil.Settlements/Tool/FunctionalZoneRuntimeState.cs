using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Wyrdrasil.Settlements.Tool;

public sealed class FunctionalZoneRuntimeState
{
    private readonly Dictionary<ZoneInterpretationKind, IReadOnlyList<int>> _interpretedDesignationIdsByKind;
    private readonly List<ZoneValidationDiagnostic> _diagnostics;

    public int ZoneId { get; }
    public int BuildingId { get; }
    public ZoneType ZoneType { get; }
    public ZoneFunctionalState State { get; }
    public ZoneCapability Capabilities { get; }
    public int PublicSeatCount { get; }
    public int ReservedSeatCount { get; }
    public int BedCount { get; }
    public int CraftStationCount { get; }
    public int InnkeeperSlotCount { get; }
    public int AssignedInnkeeperCount { get; }
    public IReadOnlyDictionary<ZoneInterpretationKind, IReadOnlyList<int>> InterpretedDesignationIdsByKind => new ReadOnlyDictionary<ZoneInterpretationKind, IReadOnlyList<int>>(_interpretedDesignationIdsByKind);
    public IReadOnlyList<ZoneValidationDiagnostic> Diagnostics => _diagnostics;

    public FunctionalZoneRuntimeState(
        int zoneId,
        int buildingId,
        ZoneType zoneType,
        ZoneFunctionalState state,
        ZoneCapability capabilities,
        int publicSeatCount,
        int reservedSeatCount,
        int bedCount,
        int craftStationCount,
        int innkeeperSlotCount,
        int assignedInnkeeperCount,
        IDictionary<ZoneInterpretationKind, IReadOnlyList<int>> interpretedDesignationIdsByKind,
        IEnumerable<ZoneValidationDiagnostic> diagnostics)
    {
        ZoneId = zoneId;
        BuildingId = buildingId;
        ZoneType = zoneType;
        State = state;
        Capabilities = capabilities;
        PublicSeatCount = publicSeatCount;
        ReservedSeatCount = reservedSeatCount;
        BedCount = bedCount;
        CraftStationCount = craftStationCount;
        InnkeeperSlotCount = innkeeperSlotCount;
        AssignedInnkeeperCount = assignedInnkeeperCount;
        _interpretedDesignationIdsByKind = interpretedDesignationIdsByKind?.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<int>)pair.Value.ToArray()) ?? new Dictionary<ZoneInterpretationKind, IReadOnlyList<int>>();
        _diagnostics = diagnostics?.ToList() ?? new List<ZoneValidationDiagnostic>();
    }

    public bool HasCapability(ZoneCapability capability)
    {
        return (Capabilities & capability) == capability;
    }

    public bool HasInterpretation(ZoneInterpretationKind interpretationKind, int designationId)
    {
        return _interpretedDesignationIdsByKind.TryGetValue(interpretationKind, out var ids) && ids.Contains(designationId);
    }
}
