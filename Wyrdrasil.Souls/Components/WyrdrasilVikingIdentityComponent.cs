using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Components;


public sealed class WyrdrasilVikingIdentityComponent : MonoBehaviour
{
    public VikingIdentityData? Identity { get; private set; }

    public void SetIdentity(VikingIdentityData identity)
    {
        Identity = identity;
    }
}
