using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dalamud.Plugin.Services;
using LLib.GameData;
using Lumina.Excel.Sheets;

namespace Influx.Remote;

internal sealed class GameData
{
    public GameData(IDataManager dataManager)
    {
        ClassJobToArrayIndex = dataManager.GetExcelSheet<ClassJob>().Where(x => x.RowId > 0)
            .ToDictionary(x => (byte)x.RowId, x => (byte)x.ExpArrayIndex)
            .AsReadOnly();
        ClassJobNames = dataManager.GetExcelSheet<ClassJob>().Where(x => x.RowId > 0)
            .ToDictionary(x => (byte)x.RowId, x => x.Abbreviation.ToString())
            .AsReadOnly();
        ExpToJobs = dataManager.GetExcelSheet<ClassJob>()
            .Where(x => x.RowId > 0 && !string.IsNullOrEmpty(x.Name.ToString()))
            .Where(x => x.JobIndex > 0 || x.DohDolJobIndex >= 0)
            .Where(x => x.RowId != (uint)EClassJob.Summoner)
            .ToDictionary(x => x.ExpArrayIndex,
                x => new ClassJobDetail(x.Abbreviation.ToString(), x.DohDolJobIndex >= 0))
            .AsReadOnly();
        Prices = dataManager.GetExcelSheet<Item>()
            .AsEnumerable()
            .ToDictionary(x => x.RowId, x => new PriceInfo
            {
                Name = x.Name.ToString(),
                Normal = x.PriceLow,
                UiCategory = x.ItemUICategory.RowId,
            })
            .AsReadOnly();
        WorldNames = dataManager.GetExcelSheet<World>()
            .Where(x => x.RowId > 0 && x.IsPublic)
            .ToDictionary(x => x.RowId, x => x.Name.ToString())
            .AsReadOnly();
    }

    public ReadOnlyDictionary<byte, byte> ClassJobToArrayIndex { get; }
    public ReadOnlyDictionary<byte, string> ClassJobNames { get; }
    public ReadOnlyDictionary<sbyte, ClassJobDetail> ExpToJobs { get; }
    public ReadOnlyDictionary<uint, PriceInfo> Prices { get; }
    public ReadOnlyDictionary<uint, string> WorldNames { get; }

    internal struct PriceInfo
    {
        public string Name { get; init; }
        public uint Normal { get; init; }
        public uint Hq => Normal + (uint)Math.Ceiling((decimal)Normal / 10);
        public uint UiCategory { get; set; }
    }

    internal sealed record ClassJobDetail(string Abbreviation, bool IsNonCombat)
    {
        public string Type => IsNonCombat ? "doh_dol" : "combat";
    }
}
