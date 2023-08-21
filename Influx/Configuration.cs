using System.Collections.Generic;
using Dalamud.Configuration;

namespace Influx;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public ServerConfiguration Server { get; set; } = new();

    public List<ulong> ExcludedCharacters { get; set; } = new();

    public sealed class ServerConfiguration
    {
        public string Server { get; set; } = "http://localhost:8086";
        public string Token { get; set; } = "xxx";
        public string Organization { get; set; } = "org";
        public string Bucket { get; set; } = "bucket";
    }
}
