using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Routines.Components;


[RequireComponent(typeof(Player))]
public sealed class WyrdrasilVikingActor : MonoBehaviour
{
    private static readonly FieldInfo? CharacterNameField = typeof(Character).GetField(
        "m_name",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private Player _player = null!;

    public Player Player => _player;
    public Humanoid Humanoid => _player;
    public Character Character => _player;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    public void Initialize(string displayName)
    {
        if (_player == null)
        {
            _player = GetComponent<Player>();
        }

        ApplyDisplayName(displayName);
    }

    public void ApplyDisplayName(string displayName)
    {
        gameObject.name = displayName;
        name = displayName;

        if (CharacterNameField != null)
        {
            CharacterNameField.SetValue(_player, displayName);
        }
    }

    public bool IsAttached()
    {
        return _player != null && _player.IsAttached();
    }

    public void AttachStop()
    {
        _player?.AttachStop();
    }
}
