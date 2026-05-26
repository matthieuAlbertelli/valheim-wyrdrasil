using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool.Commanding;

public sealed class RegistryPlayerToolCommandMessageService
{
    public void Update()
    {
        var player = Player.m_localPlayer;
        if (!RegistryPlayerToolSelectionService.IsRegistryToolEquipped(player))
        {
            return;
        }

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        if (MessageHud.instance == null)
        {
            return;
        }

        MessageHud.instance.ShowMessage(
            MessageHud.MessageType.Center,
            RegistryPlayerToolConstants.CommandMessage);
    }
}
