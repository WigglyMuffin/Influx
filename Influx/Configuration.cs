using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace Influx;

internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ServerConfiguration Server { get; set; } = new();
    public bool AutoEnrollCharacters { get; set; }

    public IList<CharacterInfo> IncludedCharacters { get; set; } = new List<CharacterInfo>();
    public IList<FilterInfo> IncludedInventoryFilters { get; set; } = new List<FilterInfo>();

    public enum ERemoteType
    {
        InfluxDb,
        QuestDb,
    }

    public sealed class ServerConfiguration
    {
        public bool Enabled { get; set; }
        public ERemoteType Type { get; set; } = ERemoteType.InfluxDb;
        public string Server { get; set; } = "http://localhost:8086";

        // Influx
        public string Token { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;

        // QuestDB
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string TablePrefix { get; set; } = string.Empty;

        public ServerConfiguration Copy()
        {
            return new ServerConfiguration
            {
                Enabled = Enabled,
                Type = Type,
                Server = Server,
                Token = Token,
                Organization = Organization,
                Bucket = Bucket,
                Username = Username,
                Password = Password,
                TablePrefix = TablePrefix,
            };
        }
    }

    public sealed class CharacterInfo
    {
        public ulong LocalContentId { get; set; }
        public string? CachedPlayerName { get; set; }
        public string? CachedWorldName { get; set; }
        public bool IncludeFreeCompany { get; set; } = true;
    }

    public sealed class FilterInfo
    {
        public required string Name { get; set; }
    }
}
