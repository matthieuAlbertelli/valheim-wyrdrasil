using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySoulsPersistenceParticipant : IWorldPersistenceParticipant
{
    private readonly RegistryResidentService _residentService;

    public RegistrySoulsPersistenceParticipant(RegistryResidentService residentService)
    {
        _residentService = residentService;
    }

    public string ModuleId => "souls";
    public int SchemaVersion => 3;

    public void ResetForWorldChange()
    {
    }

    public string CapturePayload()
    {
        _residentService.PrepareResidentPresenceSnapshotsForSave();

        var saveData = new SoulsModuleSaveData
        {
            NextResidentId = _residentService.NextRegisteredNpcId
        };

        foreach (var resident in _residentService.RegisteredNpcs)
        {
            if (resident == null || resident.Identity == null || resident.Identity.Appearance == null || resident.Identity.Equipment == null)
            {
                continue;
            }

            saveData.Residents.Add(FromRegisteredNpcData(resident));
        }

        return WorldPersistenceCoordinator.SerializePayload(saveData);
    }

    public void RestorePayload(string payloadXml)
    {
        var saveData = WorldPersistenceCoordinator.DeserializePayload<SoulsModuleSaveData>(payloadXml);
        if (saveData == null)
        {
            _residentService.LoadResidents(System.Array.Empty<RegisteredNpcData>(), 1);
            return;
        }

        _residentService.LoadResidents(saveData.Residents.Select(ToRegisteredNpcData), saveData.NextResidentId);
    }

    public bool RetryDeferredResolutions() => false;

    private static RegisteredNpcSaveData FromRegisteredNpcData(RegisteredNpcData data)
    {
        return new RegisteredNpcSaveData
        {
            Id = data.Id,
            DisplayName = data.DisplayName,
            Role = data.Role,
            Assignments = data.Assignments.Select(FromAssignmentData).ToList(),
            Identity = FromIdentityData(data.Identity),
            PresenceSnapshot = FromPresenceSnapshotData(data.PresenceSnapshot),
            ScheduleEntries = data.ScheduleEntries.Select(FromScheduleEntryData).ToList()
        };
    }

    private static RegisteredNpcData ToRegisteredNpcData(RegisteredNpcSaveData data)
    {
        var resident = new RegisteredNpcData(data.Id, data.DisplayName, ToIdentityData(data.Identity));
        resident.SetRole(data.Role);
        resident.SetAssignments(ToResidentAssignments(data));
        resident.SetScheduleEntries(data.ScheduleEntries.Select(ToScheduleEntryData));
        ApplyPresenceSnapshotSaveData(resident.PresenceSnapshot, data.PresenceSnapshot);
        return resident;
    }

    private static IEnumerable<ResidentAssignmentData> ToResidentAssignments(RegisteredNpcSaveData data)
    {
        if (data.Assignments != null && data.Assignments.Count > 0)
        {
            foreach (var assignment in data.Assignments)
            {
                yield return new ResidentAssignmentData(
                    assignment.Purpose,
                    new OccupationTargetRef(assignment.TargetKind, assignment.TargetId));
            }

            yield break;
        }

        if (data.AssignedSlotId.HasValue)
        {
            yield return new ResidentAssignmentData(
                ResidentAssignmentPurpose.Work,
                new OccupationTargetRef(OccupationTargetKind.Slot, data.AssignedSlotId.Value));
        }

        if (data.AssignedSeatId.HasValue)
        {
            yield return new ResidentAssignmentData(
                ResidentAssignmentPurpose.Meal,
                new OccupationTargetRef(OccupationTargetKind.Seat, data.AssignedSeatId.Value));
        }

        if (data.AssignedBedId.HasValue)
        {
            yield return new ResidentAssignmentData(
                ResidentAssignmentPurpose.Sleep,
                new OccupationTargetRef(OccupationTargetKind.Bed, data.AssignedBedId.Value));
        }
    }

    private static ResidentAssignmentSaveData FromAssignmentData(ResidentAssignmentData data)
    {
        return new ResidentAssignmentSaveData
        {
            Purpose = data.Purpose,
            TargetKind = data.Target.TargetKind,
            TargetId = data.Target.TargetId
        };
    }

    private static ResidentScheduleEntrySaveData FromScheduleEntryData(ResidentScheduleEntryData data)
    {
        return new ResidentScheduleEntrySaveData
        {
            ActivityType = data.ActivityType,
            StartMinuteOfDay = data.StartMinuteOfDay,
            EndMinuteOfDay = data.EndMinuteOfDay,
            Priority = data.Priority
        };
    }

    private static ResidentScheduleEntryData ToScheduleEntryData(ResidentScheduleEntrySaveData data)
    {
        return new ResidentScheduleEntryData(data.ActivityType, data.StartMinuteOfDay, data.EndMinuteOfDay, data.Priority);
    }

    private static ResidentPresenceSnapshotSaveData FromPresenceSnapshotData(ResidentPresenceSnapshotData data)
    {
        return new ResidentPresenceSnapshotSaveData
        {
            RestoreMode = data.RestoreMode,
            WorldPosition = Float3SaveData.FromVector3(data.WorldPosition),
            WorldYawDegrees = data.WorldYawDegrees,
            HasAssignedPurpose = data.AssignedPurpose.HasValue,
            AssignedPurpose = data.AssignedPurpose ?? default
        };
    }

    private static void ApplyPresenceSnapshotSaveData(ResidentPresenceSnapshotData target, ResidentPresenceSnapshotSaveData saveData)
    {
        switch (saveData.RestoreMode)
        {
            case ResidentRestoreMode.WorldPosition:
                target.SetWorldPosition(saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                break;
            case ResidentRestoreMode.AssignedTargetAnchor:
                if (saveData.HasAssignedPurpose)
                {
                    target.SetAssignedTargetAnchor(saveData.AssignedPurpose, saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                }
                else
                {
                    target.Clear();
                }

                break;
            default:
                if (TryMapLegacyRestoreMode(saveData.RestoreMode, out var legacyPurpose))
                {
                    target.SetAssignedTargetAnchor(legacyPurpose, saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                }
                else
                {
                    target.Clear();
                }

                break;
        }
    }

    private static bool TryMapLegacyRestoreMode(ResidentRestoreMode restoreMode, out ResidentAssignmentPurpose purpose)
    {
        switch (restoreMode)
        {
            case (ResidentRestoreMode)2:
                purpose = ResidentAssignmentPurpose.Work;
                return true;
            case (ResidentRestoreMode)3:
                purpose = ResidentAssignmentPurpose.Meal;
                return true;
            case (ResidentRestoreMode)4:
                purpose = ResidentAssignmentPurpose.Sleep;
                return true;
            default:
                purpose = default;
                return false;
        }
    }

    private static VikingIdentitySaveData FromIdentityData(VikingIdentityData data)
    {
        return new VikingIdentitySaveData
        {
            GenerationSeed = data.GenerationSeed,
            Role = data.Role,
            Appearance = new VikingAppearanceSaveData
            {
                ModelIndex = data.Appearance.ModelIndex,
                HairItem = data.Appearance.HairItem,
                BeardItem = data.Appearance.BeardItem ?? string.Empty,
                HasBeardItem = data.Appearance.BeardItem != null,
                SkinColor = ColorSaveData.FromColor(data.Appearance.SkinColor),
                HairColor = ColorSaveData.FromColor(data.Appearance.HairColor)
            },
            Equipment = new VikingEquipmentSaveData
            {
                HelmetItem = data.Equipment.HelmetItem ?? string.Empty,
                HasHelmetItem = data.Equipment.HelmetItem != null,
                ChestItem = data.Equipment.ChestItem ?? string.Empty,
                HasChestItem = data.Equipment.ChestItem != null,
                LegItem = data.Equipment.LegItem ?? string.Empty,
                HasLegItem = data.Equipment.LegItem != null,
                ShoulderItem = data.Equipment.ShoulderItem ?? string.Empty,
                HasShoulderItem = data.Equipment.ShoulderItem != null,
                RightHandItem = data.Equipment.RightHandItem ?? string.Empty,
                HasRightHandItem = data.Equipment.RightHandItem != null,
                LeftHandItem = data.Equipment.LeftHandItem ?? string.Empty,
                HasLeftHandItem = data.Equipment.LeftHandItem != null
            }
        };
    }

    private static VikingIdentityData ToIdentityData(VikingIdentitySaveData data)
    {
        var appearance = new VikingAppearanceData(
            data.Appearance.ModelIndex,
            data.Appearance.HairItem,
            data.Appearance.HasBeardItem ? data.Appearance.BeardItem : null,
            data.Appearance.SkinColor.ToColor(),
            data.Appearance.HairColor.ToColor());

        var equipment = new VikingEquipmentData(
            data.Equipment.HasHelmetItem ? data.Equipment.HelmetItem : null,
            data.Equipment.HasChestItem ? data.Equipment.ChestItem : null,
            data.Equipment.HasLegItem ? data.Equipment.LegItem : null,
            data.Equipment.HasShoulderItem ? data.Equipment.ShoulderItem : null,
            data.Equipment.HasRightHandItem ? data.Equipment.RightHandItem : null,
            data.Equipment.HasLeftHandItem ? data.Equipment.LeftHandItem : null);

        return new VikingIdentityData(data.GenerationSeed, data.Role, appearance, equipment);
    }
}
