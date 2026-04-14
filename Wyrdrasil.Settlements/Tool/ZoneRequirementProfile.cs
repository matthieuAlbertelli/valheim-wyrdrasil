namespace Wyrdrasil.Settlements.Tool;

public sealed class ZoneRequirementProfile
{
    public int MinimumPublicSeatCount { get; }
    public int MinimumReservedSeatCount { get; }
    public int MinimumBedCount { get; }
    public int MinimumCraftStationCount { get; }
    public int MinimumInnkeeperSlotCount { get; }
    public int MinimumAssignedInnkeeperCount { get; }

    public bool IsEmpty =>
        MinimumPublicSeatCount <= 0 &&
        MinimumReservedSeatCount <= 0 &&
        MinimumBedCount <= 0 &&
        MinimumCraftStationCount <= 0 &&
        MinimumInnkeeperSlotCount <= 0 &&
        MinimumAssignedInnkeeperCount <= 0;

    public ZoneRequirementProfile(
        int minimumPublicSeatCount = 0,
        int minimumReservedSeatCount = 0,
        int minimumBedCount = 0,
        int minimumCraftStationCount = 0,
        int minimumInnkeeperSlotCount = 0,
        int minimumAssignedInnkeeperCount = 0)
    {
        MinimumPublicSeatCount = minimumPublicSeatCount < 0 ? 0 : minimumPublicSeatCount;
        MinimumReservedSeatCount = minimumReservedSeatCount < 0 ? 0 : minimumReservedSeatCount;
        MinimumBedCount = minimumBedCount < 0 ? 0 : minimumBedCount;
        MinimumCraftStationCount = minimumCraftStationCount < 0 ? 0 : minimumCraftStationCount;
        MinimumInnkeeperSlotCount = minimumInnkeeperSlotCount < 0 ? 0 : minimumInnkeeperSlotCount;
        MinimumAssignedInnkeeperCount = minimumAssignedInnkeeperCount < 0 ? 0 : minimumAssignedInnkeeperCount;
    }
}
