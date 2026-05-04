using BepInEx.Logging;
using Wyrdrasil.Souls.Authoring;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Persistence;
using Wyrdrasil.Souls.Runtime;
using Wyrdrasil.Souls.Services;

namespace Wyrdrasil.Souls.Bootstrap;

public sealed class SoulsModuleBootstrap
{
    public SoulsModuleBootstrap(
        NpcAppearanceCatalog appearanceCatalog,
        NpcEquipmentCatalog equipmentCatalog,
        NpcAppearanceGenerator appearanceGenerator,
        NpcEquipmentGenerator equipmentGenerator,
        NpcIdentityGenerator identityGenerator,
        NpcCustomizationApplier customizationApplier,
        NpcIdentityService identityService,
        VikingPrefabFactory vikingPrefabFactory,
        NpcSpawnService spawnService,
        ResidentCatalogService residentCatalogService,
        ResidentRuntimeService residentRuntimeService,
        IWorldPersistenceParticipant persistenceParticipant,
        ISoulsAuthoringApi authoringApi,
        ISoulsRuntimeApi runtimeApi)
    {
        AppearanceCatalog = appearanceCatalog;
        EquipmentCatalog = equipmentCatalog;
        AppearanceGenerator = appearanceGenerator;
        EquipmentGenerator = equipmentGenerator;
        IdentityGenerator = identityGenerator;
        CustomizationApplier = customizationApplier;
        IdentityService = identityService;
        VikingPrefabFactory = vikingPrefabFactory;
        SpawnService = spawnService;
        ResidentCatalogService = residentCatalogService;
        ResidentRuntimeService = residentRuntimeService;
        PersistenceParticipant = persistenceParticipant;
        AuthoringApi = authoringApi;
        RuntimeApi = runtimeApi;
    }

    public NpcAppearanceCatalog AppearanceCatalog { get; }
    public NpcEquipmentCatalog EquipmentCatalog { get; }
    public NpcAppearanceGenerator AppearanceGenerator { get; }
    public NpcEquipmentGenerator EquipmentGenerator { get; }
    public NpcIdentityGenerator IdentityGenerator { get; }
    public NpcCustomizationApplier CustomizationApplier { get; }
    public NpcIdentityService IdentityService { get; }
    public VikingPrefabFactory VikingPrefabFactory { get; }
    public NpcSpawnService SpawnService { get; }
    public ResidentCatalogService ResidentCatalogService { get; }
    public ResidentRuntimeService ResidentRuntimeService { get; }
    public IWorldPersistenceParticipant PersistenceParticipant { get; }
    public ISoulsAuthoringApi AuthoringApi { get; }
    public ISoulsRuntimeApi RuntimeApi { get; }

    public static SoulsModuleBootstrap Create(ManualLogSource log)
    {
        var appearanceCatalog = new NpcAppearanceCatalog();
        var equipmentCatalog = new NpcEquipmentCatalog();
        var appearanceGenerator = new NpcAppearanceGenerator(appearanceCatalog);
        var equipmentGenerator = new NpcEquipmentGenerator(equipmentCatalog);
        var identityGenerator = new NpcIdentityGenerator(appearanceGenerator, equipmentGenerator);
        var customizationApplier = new NpcCustomizationApplier(log);
        WyrdrasilVikingVisualBootstrap.ConfigureLogger(log);

        var identityService = new NpcIdentityService(identityGenerator, customizationApplier);
        var vikingPrefabFactory = new VikingPrefabFactory(log);
        var spawnService = new NpcSpawnService(log, vikingPrefabFactory, identityGenerator, customizationApplier);
        var residentCatalogService = new ResidentCatalogService();
        var residentRuntimeService = new ResidentRuntimeService(log);

        var authoringApi = new SoulsAuthoringApi(spawnService, identityService);
        var runtimeApi = new SoulsRuntimeApi(residentCatalogService, residentRuntimeService, spawnService);
        var persistenceParticipant = new SoulsPersistenceParticipant(runtimeApi);

        return new SoulsModuleBootstrap(
            appearanceCatalog,
            equipmentCatalog,
            appearanceGenerator,
            equipmentGenerator,
            identityGenerator,
            customizationApplier,
            identityService,
            vikingPrefabFactory,
            spawnService,
            residentCatalogService,
            residentRuntimeService,
            persistenceParticipant,
            authoringApi,
            runtimeApi);
    }
}
