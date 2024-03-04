using System.Collections.Generic;
using Dalamud.Configuration;

namespace Influx;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ServerConfiguration Server { get; set; } = new();

    public List<CharacterInfo> IncludedCharacters { get; set; } = new();
    public List<FilterInfo> IncludedInventoryFilters { get; set; } = new();

    public sealed class ServerConfiguration
    {
        public bool Enabled { get; set; }
        public string Server { get; set; } = "http://localhost:8086";
        public string Token { get; set; } = "";
        public string Organization { get; set; } = "";
        public string Bucket { get; set; } = "";
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
