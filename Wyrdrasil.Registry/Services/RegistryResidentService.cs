using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentService
{
    private readonly ManualLogSource _log;
    private readonly RegistryToolState _toolState;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly RegistrySpawnService _spawnService;
    private readonly RegistryNpcIdentityGenerator _identityGenerator;
    private readonly RegistryNpcCustomizationApplier _customizationApplier;
    private readonly List<RegisteredNpcData> _registeredNpcs = new();
    private readonly Dictionary<int, RegisteredNpcData> _registeredById = new();
    private readonly Dictionary<int, WyrdrasilRegisteredNpcMarker> _markers = new();

    private int _nextRegisteredNpcId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _registeredNpcs;
    public int NextRegisteredNpcId => _nextRegisteredNpcId;

    public RegistryResidentService(
        ManualLogSource log,
        RegistryToolState toolState,
        RegistryModeService modeService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryResidentRuntimeService runtimeService,
        RegistrySpawnService spawnService,
        RegistryNpcIdentityGenerator identityGenerator,
        RegistryNpcCustomizationApplier customizationApplier)
    {
        _log = log;
        _toolState = toolState;
        _slotService = slotService;
        _seatService = seatService;
        _runtimeService = runtimeService;
        _spawnService = spawnService;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public IReadOnlyDictionary<int, WyrdrasilRegisteredNpcMarker> Markers => _markers;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _registeredNpcs.Clear();
        _registeredById.Clear();
        _markers.Clear();

        foreach (var resident in residents)
        {
            _registeredNpcs.Add(resident);
            _registeredById[resident.Id] = resident;
        }

        _nextRegisteredNpcId = nextResidentId;
    }

    public bool TryGetResidentById(int residentId, out RegisteredNpcData resident)
    {
        return _registeredById.TryGetValue(residentId, out resident!);
    }

    public void PrepareResidentPresenceSnapshotsForSave()
    {
        foreach (var resident in _registeredNpcs)
        {
            var runtimeState = _runtimeService.GetRuntimeState(resident.Id);
            if (runtimeState == ResidentRuntimeState.Spawning)
            {
                continue;
            }

            if (!_runtimeService.TryCaptureBoundResidentTransform(resident.Id, out var worldPosition, out var worldYawDegrees, out var isAttached))
            {
                continue;
            }

            if (resident.AssignedSeatId.HasValue && isAttached)
            {
                resident.PresenceSnapshot.SetAssignedSeatAnchor(worldPosition, worldYawDegrees);
                continue;
            }

            resident.PresenceSnapshot.SetWorldPosition(worldPosition, worldYawDegrees);
        }
    }

    public void RestoreResidentsAfterLoad()
    {
        foreach (var resident in _registeredNpcs)
        {
            if (!resident.PresenceSnapshot.ShouldRespawnOnLoad)
            {
                continue;
            }

            if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
            {
                continue;
            }

            if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
            {
                continue;
            }

            switch (resident.PresenceSnapshot.RestoreMode)
            {
                case ResidentRestoreMode.WorldPosition:
                    RestoreResidentAtWorldPosition(resident);
                    break;

                case ResidentRestoreMode.AssignedSeatAnchor:
                    RestoreResidentAtAssignedSeat(resident);
                    break;

                case ResidentRestoreMode.AssignedSlotAnchor:
                    RestoreResidentAtAssignedSlot(resident);
                    break;
            }
        }
    }

    public void ClearAllResidents()
    {
        foreach (var resident in _registeredNpcs)
        {
            _runtimeService.TryDespawnResident(resident.Id);
            resident.PresenceSnapshot.Clear();
        }

        _registeredNpcs.Clear();
        _registeredById.Clear();
        _markers.Clear();
        _toolState.ClearPendingResidentForceAssign();
        _nextRegisteredNpcId = 1;
    }

    public void SetPendingForceAssignResidentVisual(int? residentId)
    {
        foreach (var pair in _markers)
        {
            if (pair.Value == null) continue;
            pair.Value.SetPendingForceAssign(residentId.HasValue && pair.Key == residentId.Value);
        }
    }

    public void RegisterNpcAtCrosshair()
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            _log.LogWarning("Cannot register NPC: no valid character is under the crosshair.");
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer != null && targetCharacter.gameObject == localPlayer.gameObject)
        {
            _log.LogWarning("Cannot register NPC: the local player cannot be registered as a resident.");
            return;
        }

        if (_runtimeService.TryGetResidentId(targetCharacter, out _))
        {
            _log.LogWarning("Cannot register NPC: this character is already registered.");
            return;
        }

        var displayName = GetCharacterName(targetCharacter);
        var identity = ResolveOrCreateIdentity(targetCharacter, NpcRole.Villager, out var createdIdentity);
        var data = new RegisteredNpcData(_nextRegisteredNpcId++, displayName, identity);
        _registeredNpcs.Add(data);
        _registeredById[data.Id] = data;
        _runtimeService.BindResident(data.Id, targetCharacter);
        data.PresenceSnapshot.SetWorldPosition(targetCharacter.transform.position, targetCharacter.transform.eulerAngles.y);
        EnsureMarker(data);

        _log.LogInfo($"Registered NPC #{data.Id}: '{data.DisplayName}' with {(createdIdentity ? "new" : "existing")} identity seed={identity.GenerationSeed}, generatedRole={identity.Role}, female={identity.Appearance.IsFemale}.");
    }

    public void ForceAssignAtCrosshair()
    {
        if (TryGetTargetRegisteredResident(out var targetedResident))
        {
            _toolState.SetPendingResidentForceAssign(targetedResident.Id, targetedResident.DisplayName);
            _log.LogInfo($"Selected resident #{targetedResident.Id} ('{targetedResident.DisplayName}') for force assignment. Target an innkeeper slot or a designated seat to complete the operation.");
            return;
        }

        if (!_toolState.PendingResidentForceAssignId.HasValue || !_registeredById.TryGetValue(_toolState.PendingResidentForceAssignId.Value, out var pendingResident))
        {
            _toolState.ClearPendingResidentForceAssign();
            _log.LogWarning("Cannot force assign: target a registered resident first.");
            return;
        }

        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            ForceAssignResidentToSlot(pendingResident, slotData);
            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            ForceAssignResidentToSeat(pendingResident, seatData);
            return;
        }

        _log.LogWarning($"Cannot force assign resident #{pendingResident.Id}: target an innkeeper slot, a designated seat, or another registered resident.");
    }

    public void ClearTargetInnkeeperSlotAssignmentAtCrosshair()
    {
        if (!_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            _log.LogWarning("Cannot clear slot assignment: no innkeeper slot is under the crosshair.");
            return;
        }

        if (!_slotService.ClearSlotAssignment(slotData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            _log.LogWarning($"Cannot clear slot assignment: innkeeper slot #{slotData.Id} has no assigned resident.");
            return;
        }

        if (_registeredById.TryGetValue(previousResidentId.Value, out var resident))
        {
            resident.ClearAssignedSlot();
            resident.SetRole(NpcRole.Villager);
            UpdateMarker(resident);
        }
    }

    public void ClearTargetSeatAssignmentAtCrosshair()
    {
        if (!_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            _log.LogWarning("Cannot clear seat assignment: no designated seat is under the crosshair.");
            return;
        }

        if (!_seatService.ClearSeatAssignment(seatData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            _log.LogWarning($"Cannot clear seat assignment: seat #{seatData.Id} has no assigned resident.");
            return;
        }

        if (_registeredById.TryGetValue(previousResidentId.Value, out var resident))
        {
            DetachResidentIfBound(resident);
            resident.ClearAssignedSeat();
            UpdateMarker(resident);
        }
    }

    public void DespawnTargetResidentAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot despawn resident", out _, out var resident)) return;
        if (!_runtimeService.TryDespawnResident(resident.Id))
        {
            _log.LogWarning($"Cannot despawn resident #{resident.Id}: runtime destruction failed.");
            return;
        }

        resident.PresenceSnapshot.Clear();
        _log.LogInfo($"Despawned resident #{resident.Id} ('{resident.DisplayName}').");
    }

    public void RespawnAssignedResidentAtCrosshair()
    {
        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            RespawnResidentAssignedToSlot(slotData);
            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            RespawnResidentAssignedToSeat(seatData);
            return;
        }

        _log.LogWarning("Cannot respawn resident: the targeted object is neither a registered innkeeper slot nor a registered designated seat.");
    }

    public void AssignInnkeeperRoleAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot assign innkeeper role", out var targetCharacter, out var data)) return;
        DetachIfAttached(targetCharacter);
        _slotService.ClearAssignmentForResident(data.Id);
        _seatService.ClearAssignmentForResident(data.Id);
        data.ClearAssignedSeat();

        if (!_slotService.TryAssignInnkeeperSlot(data.Id, out var slotData) || slotData == null)
        {
            _log.LogWarning("Cannot assign innkeeper role: no free Innkeeper slot is available.");
            return;
        }

        data.SetRole(NpcRole.Innkeeper);
        data.AssignSlot(slotData.Id);
        UpdateMarker(data);
        _runtimeService.ApplyInnkeeperAssignment(data, slotData);
        _log.LogInfo($"Assigned Innkeeper role to registered NPC #{data.Id} ('{data.DisplayName}') using slot #{slotData.Id}.");
    }

    public void AssignSeatAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot assign seat", out var targetCharacter, out var data)) return;
        if (data.Role == NpcRole.Innkeeper)
        {
            _log.LogWarning("Cannot assign seat: the targeted resident is already an Innkeeper.");
            return;
        }

        DetachIfAttached(targetCharacter);
        _seatService.ClearAssignmentForResident(data.Id);
        data.ClearAssignedSeat();

        if (!_seatService.TryAssignSeat(data.Id, out var seatData) || seatData == null)
        {
            _log.LogWarning("Cannot assign seat: no free designated seat is available.");
            return;
        }

        data.SetRole(NpcRole.Villager);
        data.AssignSeat(seatData.Id);
        UpdateMarker(data);
        _runtimeService.ApplySeatAssignment(data, seatData);
        _log.LogInfo($"Assigned designated seat #{seatData.Id} to registered NPC #{data.Id} ('{data.DisplayName}').");
    }

    public void HandleDeletedSlot(int slotId)
    {
        foreach (var resident in _registeredNpcs.Where(resident => resident.AssignedSlotId == slotId))
        {
            resident.ClearAssignedSlot();
            resident.SetRole(NpcRole.Villager);
            UpdateMarker(resident);
        }
    }

    public void HandleDeletedSeat(int seatId)
    {
        foreach (var resident in _registeredNpcs.Where(resident => resident.AssignedSeatId == seatId))
        {
            DetachResidentIfBound(resident);
            resident.ClearAssignedSeat();
            UpdateMarker(resident);
        }
    }

    private void ForceAssignResidentToSlot(RegisteredNpcData pendingResident, ZoneSlotData slotData)
    {
        if (pendingResident.AssignedSlotId == slotData.Id)
        {
            _toolState.ClearPendingResidentForceAssign();
            return;
        }

        _slotService.ClearAssignmentForResident(pendingResident.Id);
        _seatService.ClearAssignmentForResident(pendingResident.Id);
        pendingResident.ClearAssignedSeat();
        pendingResident.ClearAssignedSlot();
        pendingResident.SetRole(NpcRole.Villager);
        UpdateMarker(pendingResident);
        DetachResidentIfBound(pendingResident);

        if (!_slotService.ForceAssignInnkeeperSlot(slotData.Id, pendingResident.Id, out var previousResidentId, out var resolvedSlot) || resolvedSlot == null) return;
        if (previousResidentId.HasValue && previousResidentId.Value != pendingResident.Id && _registeredById.TryGetValue(previousResidentId.Value, out var displacedResident))
        {
            displacedResident.ClearAssignedSlot();
            displacedResident.SetRole(NpcRole.Villager);
            UpdateMarker(displacedResident);
        }

        pendingResident.SetRole(NpcRole.Innkeeper);
        pendingResident.AssignSlot(resolvedSlot.Id);
        UpdateMarker(pendingResident);
        _runtimeService.ApplyInnkeeperAssignment(pendingResident, resolvedSlot);
        _toolState.ClearPendingResidentForceAssign();
    }

    private void ForceAssignResidentToSeat(RegisteredNpcData pendingResident, RegisteredSeatData seatData)
    {
        if (pendingResident.AssignedSeatId == seatData.Id)
        {
            _toolState.ClearPendingResidentForceAssign();
            return;
        }

        _slotService.ClearAssignmentForResident(pendingResident.Id);
        _seatService.ClearAssignmentForResident(pendingResident.Id);
        pendingResident.ClearAssignedSlot();
        pendingResident.ClearAssignedSeat();
        pendingResident.SetRole(NpcRole.Villager);
        UpdateMarker(pendingResident);
        DetachResidentIfBound(pendingResident);

        if (!_seatService.ForceAssignSeat(seatData.Id, pendingResident.Id, out var previousResidentId, out var resolvedSeat) || resolvedSeat == null) return;
        if (previousResidentId.HasValue && previousResidentId.Value != pendingResident.Id && _registeredById.TryGetValue(previousResidentId.Value, out var displacedResident))
        {
            DetachResidentIfBound(displacedResident);
            displacedResident.ClearAssignedSeat();
            UpdateMarker(displacedResident);
        }

        pendingResident.AssignSeat(resolvedSeat.Id);
        UpdateMarker(pendingResident);
        _runtimeService.ApplySeatAssignment(pendingResident, resolvedSeat);
        _toolState.ClearPendingResidentForceAssign();
    }

    private void RestoreResidentAtWorldPosition(RegisteredNpcData resident)
    {
        var spawnPosition = resident.PresenceSnapshot.WorldPosition;
        var spawnRotation = Quaternion.Euler(0f, resident.PresenceSnapshot.WorldYawDegrees, 0f);
        TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _);
    }

    private void RestoreResidentAtAssignedSeat(RegisteredNpcData resident)
    {
        if (!resident.AssignedSeatId.HasValue)
        {
            return;
        }

        if (!_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            return;
        }

        RespawnResidentAssignedToSeat(seatData);
    }

    private void RestoreResidentAtAssignedSlot(RegisteredNpcData resident)
    {
        if (!resident.AssignedSlotId.HasValue)
        {
            return;
        }

        var slotData = _slotService.Slots.FirstOrDefault(candidate => candidate.Id == resident.AssignedSlotId.Value);
        if (slotData == null)
        {
            return;
        }

        RespawnResidentAssignedToSlot(slotData);
    }

    private void RespawnResidentAssignedToSlot(ZoneSlotData slotData)
    {
        if (!slotData.AssignedRegisteredNpcId.HasValue || !_registeredById.TryGetValue(slotData.AssignedRegisteredNpcId.Value, out var resident)) return;
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _)) return;
        if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning) return;
        var spawnPosition = slotData.Position;
        var spawnRotation = BuildFacingRotation(spawnPosition, GetPlayerLookTargetOrFallback(spawnPosition));
        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _)) return;
        _runtimeService.ApplyInnkeeperAssignment(resident, slotData);
    }

    private void RespawnResidentAssignedToSeat(RegisteredSeatData seatData)
    {
        if (!seatData.AssignedRegisteredNpcId.HasValue || !_registeredById.TryGetValue(seatData.AssignedRegisteredNpcId.Value, out var resident)) return;
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _)) return;
        if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning) return;
        var spawnPosition = seatData.ApproachPosition;
        var spawnRotation = BuildFacingRotation(spawnPosition, seatData.SeatPosition);
        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _)) return;
        _runtimeService.ApplySeatAssignment(resident, seatData);
    }

    private bool TrySpawnAndBindResident(RegisteredNpcData resident, Vector3 spawnPosition, Quaternion spawnRotation, out Character runtimeCharacter)
    {
        runtimeCharacter = null!;
        _runtimeService.MarkResidentSpawning(resident.Id);
        if (!_spawnService.TrySpawnResident(resident, spawnPosition, spawnRotation, out var instance) || instance == null)
        {
            _runtimeService.MarkResidentMissing(resident.Id);
            return false;
        }

        var character = instance.GetComponent<Character>();
        if (character == null)
        {
            UnityEngine.Object.Destroy(instance);
            _runtimeService.MarkResidentMissing(resident.Id);
            return false;
        }

        _runtimeService.BindResident(resident.Id, character);
        resident.PresenceSnapshot.SetWorldPosition(spawnPosition, spawnRotation.eulerAngles.y);
        EnsureMarker(resident);
        runtimeCharacter = character;
        return true;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values) marker.SetVisualizationVisible(isEnabled);
    }

    private bool TryGetTargetCharacter(out Character targetCharacter)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var character = hitInfo.collider.GetComponentInParent<Character>();
                if (character != null)
                {
                    targetCharacter = character;
                    return true;
                }
            }
        }

        targetCharacter = null!;
        return false;
    }

    private bool TryGetTargetRegisteredResident(out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) || !_registeredById.TryGetValue(residentId, out resident))
        {
            resident = null!;
            return false;
        }

        return true;
    }

    private bool TryGetTargetRegisteredResident(string actionLabel, out Character targetCharacter, out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out targetCharacter))
        {
            _log.LogWarning($"{actionLabel}: no valid character is under the crosshair.");
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) || !_registeredById.TryGetValue(residentId, out resident))
        {
            _log.LogWarning($"{actionLabel}: the targeted character is not registered.");
            resident = null!;
            return false;
        }

        return true;
    }

    private string GetCharacterName(Character character)
    {
        var nameField = typeof(Character).GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return nameField?.GetValue(character) as string ?? character.gameObject.name;
    }

    private VikingIdentityData ResolveOrCreateIdentity(Character targetCharacter, NpcRole defaultRole, out bool createdIdentity)
    {
        var existingIdentity = targetCharacter.GetComponent<WyrdrasilVikingIdentityComponent>()?.Identity;
        if (existingIdentity != null)
        {
            createdIdentity = false;
            return existingIdentity;
        }

        var identity = _identityGenerator.Generate(defaultRole);
        try { _customizationApplier.Apply(targetCharacter.gameObject, identity); }
        catch (Exception) { }
        createdIdentity = true;
        return identity;
    }

    private void EnsureMarker(RegisteredNpcData data)
    {
        if (!_runtimeService.TryGetBoundCharacter(data.Id, out var character)) return;
        var marker = character.GetComponent<WyrdrasilRegisteredNpcMarker>() ?? character.gameObject.AddComponent<WyrdrasilRegisteredNpcMarker>();
        marker.Initialize(data.Id, data.DisplayName, data.Role);
        marker.EnsureVisual();
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[data.Id] = marker;
    }

    private void UpdateMarker(RegisteredNpcData data)
    {
        if (_markers.TryGetValue(data.Id, out var marker)) marker.UpdateRole(data.Role);
    }

    private void DetachResidentIfBound(RegisteredNpcData resident)
    {
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character)) DetachIfAttached(character);
    }

    private static void DetachIfAttached(Character character)
    {
        if (character is Humanoid humanoid && humanoid.IsAttached()) humanoid.AttachStop();
    }

    private static Quaternion BuildFacingRotation(Vector3 fromPosition, Vector3 toPosition)
    {
        var direction = toPosition - fromPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) direction = Vector3.forward;
        return Quaternion.LookRotation(direction.normalized);
    }

    private static Vector3 GetPlayerLookTargetOrFallback(Vector3 fallbackPosition)
    {
        var localPlayer = Player.m_localPlayer;
        return localPlayer ? localPlayer.transform.position : fallbackPosition + Vector3.forward;
    }
}
