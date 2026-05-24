using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Authoring;

public interface ISoulsAuthoringApi
{
    void SpawnTestViking();
    bool TrySpawnTestViking(out Character? spawnedCharacter, out string failureReason);
    VikingIdentityData ResolveOrCreateIdentity(Character targetCharacter, NpcRole defaultRole, out bool createdIdentity);
}
