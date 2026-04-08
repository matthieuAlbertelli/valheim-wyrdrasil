using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Wyrdrasil.Core.Persistence;

public sealed class WorldPersistenceCoordinator
{
    public WorldSaveEnvelope Capture(IEnumerable<IWorldPersistenceParticipant> participants)
    {
        var envelope = new WorldSaveEnvelope();
        foreach (var participant in participants)
        {
            envelope.Sections.Add(new ModuleSaveSectionData
            {
                ModuleId = participant.ModuleId,
                SchemaVersion = participant.SchemaVersion,
                PayloadXml = participant.CapturePayload()
            });
        }

        return envelope;
    }

    public void Restore(WorldSaveEnvelope envelope, IEnumerable<IWorldPersistenceParticipant> participants)
    {
        var sectionByModuleId = envelope.Sections.ToDictionary(section => section.ModuleId, section => section);
        foreach (var participant in participants)
        {
            if (sectionByModuleId.TryGetValue(participant.ModuleId, out var section))
            {
                participant.RestorePayload(section.PayloadXml ?? string.Empty);
            }
            else
            {
                participant.RestorePayload(string.Empty);
            }
        }
    }

    public static string SerializePayload<T>(T payload)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        serializer.Serialize(writer, payload);
        return writer.ToString();
    }

    public static T? DeserializePayload<T>(string payloadXml)
    {
        if (string.IsNullOrWhiteSpace(payloadXml))
        {
            return default;
        }

        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(payloadXml);
        var value = serializer.Deserialize(reader);
        return value is T typedValue ? typedValue : default;
    }
}
