using System;
using System.Collections.Generic;
using System.Linq;

namespace Wyrdrasil.Settlements.Tool;

public sealed class SettlementsDeletionReport
{
    public SettlementsDeletionReport(
        int? deletedZoneId = null,
        IEnumerable<int>? deletedSlotIds = null,
        IEnumerable<int>? deletedSeatIds = null,
        IEnumerable<int>? deletedBedIds = null,
        IEnumerable<int>? deletedCraftStationIds = null)
    {
        DeletedZoneId = deletedZoneId;
        DeletedSlotIds = (deletedSlotIds ?? Array.Empty<int>()).ToArray();
        DeletedSeatIds = (deletedSeatIds ?? Array.Empty<int>()).ToArray();
        DeletedBedIds = (deletedBedIds ?? Array.Empty<int>()).ToArray();
        DeletedCraftStationIds = (deletedCraftStationIds ?? Array.Empty<int>()).ToArray();
    }

    public int? DeletedZoneId { get; }
    public IReadOnlyList<int> DeletedSlotIds { get; }
    public IReadOnlyList<int> DeletedSeatIds { get; }
    public IReadOnlyList<int> DeletedBedIds { get; }
    public IReadOnlyList<int> DeletedCraftStationIds { get; }
}
