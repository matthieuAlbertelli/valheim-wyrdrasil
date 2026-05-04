using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Authoring;

public interface ISoulsAuthoringApi
{
    void SpawnTestViking();
    VikingIdentityData ResolveOrCreateIdentity(Character targetCharacter, NpcRole defaultRole, out bool createdIdentity);
}
