using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Components;

public sealed class WyrdrasilVikingIdentityComponent : MonoBehaviour
{
    public VikingIdentityData? Identity { get; private set; }

    public void SetIdentity(VikingIdentityData identity)
    {
        Identity = identity;
    }
}
