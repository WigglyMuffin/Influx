using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace Influx.Remote.Clients;

internal sealed class InfluxDbStatisticsClient : BaseStatisticsClient
{
    private readonly IPluginLog _pluginLog;
    private readonly Configuration.ServerConfiguration _serverConfiguration;
    private readonly InfluxDBClient? _influxClient;

    public InfluxDbStatisticsClient(
        IChatGui chatGui,
        Configuration configuration,
        GameData gameData,
        IClientState clientState,
        IPlayerState playerState,
        IPluginLog pluginLog)
        : base(chatGui, configuration, gameData, clientState, playerState, pluginLog)
    {
        _pluginLog = pluginLog;
        _serverConfiguration = configuration.Server.Copy();

        if (Enabled)
            _influxClient = new InfluxDBClient(_serverConfiguration.Server, _serverConfiguration.Token);
    }

    public override bool Enabled
        => _serverConfiguration is { Enabled: true, Type: Configuration.ERemoteType.InfluxDb } &&
           !string.IsNullOrEmpty(_serverConfiguration.Server) &&
           (_serverConfiguration.Server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            _serverConfiguration.Server.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
           !string.IsNullOrEmpty(_serverConfiguration.Token) &&
           !string.IsNullOrEmpty(_serverConfiguration.Organization) &&
           !string.IsNullOrEmpty(_serverConfiguration.Bucket);

    protected override async Task SaveStatistics(List<StatisticsValues> values)
    {
        if (_influxClient == null)
            return;

        var mappedValues = values.Select(MapValue).ToList();

        var writeApi = _influxClient.GetWriteApiAsync();
        await writeApi.WritePointsAsync(
                mappedValues,
                bucket: _serverConfiguration.Bucket,
                org: _serverConfiguration.Organization)
            .ConfigureAwait(false);

        _pluginLog.Verbose($"Influx: Sent {values.Count} data points to server");
    }

    private PointData MapValue(StatisticsValues value)
    {
        var pointData = PointData.Measurement(value.Name);

        foreach (var tag in value.Tags)
            pointData = pointData.Tag(tag.Key, tag.Value);

        foreach (var field in value.BoolFields)
            pointData = pointData.Field(field.Key, field.Value ? 1 : 0);

        foreach (var field in value.LongFields)
            pointData = pointData.Field(field.Key, field.Value);

        foreach (var field in value.UlongFields)
            pointData = pointData.Field(field.Key, field.Value);

        return pointData.Timestamp(value.Time, WritePrecision.S);
    }

    public override void Dispose()
    {
        _influxClient?.Dispose();
    }

    public override async Task<(bool Success, string Error)> TestConnection(CancellationToken cancellationToken)
    {
        string orgName = _serverConfiguration.Organization;
        string bucketName = _serverConfiguration.Bucket;
        if (_influxClient == null)
            return (false, "InfluxDB client is not initialized");

        try
        {
            bool ping = await _influxClient.PingAsync().ConfigureAwait(false);
            if (!ping)
                return (false, "Ping failed");
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Unable to connect to InfluxDB server");
            return (false, "Failed to ping InfluxDB server");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var buckets = await _influxClient.GetBucketsApi()
                .FindBucketsByOrgNameAsync(orgName, cancellationToken)
                .ConfigureAwait(false);

            if (buckets == null)
                return (false, "InfluxDB returned no buckets");

            if (buckets.Count == 0)
                return (true,
                    "Could not check if bucket exists (the token might not have permissions to query buckets)");

            if (buckets.All(x => x.Name != bucketName))
                return (false, $"Bucket '{bucketName}' not found");
        }
        catch (Exception e)
        {
            _pluginLog.Error(e, "Could not query buckets from InfluxDB");
            return (false, "Failed to load buckets from InfluxDB server");
        }

        return (true, string.Empty);
    }
}