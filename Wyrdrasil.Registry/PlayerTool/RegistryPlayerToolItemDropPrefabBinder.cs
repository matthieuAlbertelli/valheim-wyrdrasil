using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool;

/// <summary>
/// Keeps spawned inventory instances of the registry tool bound to their runtime prefab.
///
/// Valheim's equipment visual setup expects equipped ItemData.m_dropPrefab to be non-null.
/// Runtime-cloned item prefabs can occasionally reach inventory/equipment code with that
/// reference missing, which causes Humanoid.SetupVisEquipment to throw before the native
/// build-tool UI can even open.
/// </summary>
public sealed class RegistryPlayerToolItemDropPrefabBinder : MonoBehaviour
{
    public GameObject? RuntimePrefab;

    private void Awake()
    {
        BindDropPrefab();
    }

    private void OnEnable()
    {
        BindDropPrefab();
    }

    private void Start()
    {
        // ItemDrop can rewrite m_dropPrefab during its own Awake/initialization.
        // Start runs after all Awake calls on the spawned object, so it is a safer
        // final pass before the player can pick up and equip the item.
        BindDropPrefab();
    }

    public void BindDropPrefab()
    {
        var itemDrop = GetComponent<ItemDrop>();
        if (itemDrop == null)
        {
            return;
        }

        var runtimePrefab = RuntimePrefab;
        if (runtimePrefab == null)
        {
            runtimePrefab = RegistryPlayerToolItemDataRepair.ResolveRuntimePrefab();
        }

        if (runtimePrefab == null)
        {
            return;
        }

        itemDrop.m_itemData.m_dropPrefab = runtimePrefab;

        // Canonicalize spawned world instances before the player can pick them up.
        // This keeps the normal fresh-spawn path out of the periodic legacy repair pass.
        RegistryPlayerToolItemDataRepair.RepairItemDrop(itemDrop, runtimePrefab);
    }
}
