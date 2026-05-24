using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Authoring;

public sealed class SoulsAuthoringApi : ISoulsAuthoringApi
{
    private readonly NpcSpawnService _spawnService;
    private readonly NpcIdentityService _identityService;

    public SoulsAuthoringApi(
        NpcSpawnService spawnService,
        NpcIdentityService identityService)
    {
        _spawnService = spawnService;
        _identityService = identityService;
    }

    public void SpawnTestViking()
    {
        _spawnService.SpawnTestViking();
    }

    public bool TrySpawnTestViking(out Character? spawnedCharacter, out string failureReason)
    {
        return _spawnService.TrySpawnTestViking(out spawnedCharacter, out failureReason);
    }

    public VikingIdentityData ResolveOrCreateIdentity(Character targetCharacter, NpcRole defaultRole, out bool createdIdentity)
    {
        return _identityService.ResolveOrCreateIdentity(targetCharacter, defaultRole, out createdIdentity);
    }
}
