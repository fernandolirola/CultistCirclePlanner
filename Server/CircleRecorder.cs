using System.Text.Json;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Hideout;
using SPTarkov.Server.Core.Services.Hideout;

namespace CultistCircle;

public sealed class RitualItem
{
    public string Tpl { get; set; } = "";
    public int Count { get; set; } = 1;
    public double Handbook { get; set; }
    public bool HasParts { get; set; }
}

// What the server rolled when a sacrifice started, kept because the game forgets it right away
public sealed class RitualRecord
{
    public long StartedAt { get; set; }
    public int Seconds { get; set; }
    public double Rolled { get; set; }
    public double Base { get; set; }
    public double Skill { get; set; }
    // "tier", "bonus" or "recipe"
    public string Kind { get; set; } = "tier";
    public List<RitualItem> Sacrificed { get; set; } = [];
    public List<RitualItem> Rewards { get; set; } = [];
}

/// <summary>
/// Watches the circle's sacrifice: a postfix on the time/reward roll keeps the rolled value, and a postfix on the sacrifice itself
/// writes the record once the rewards exist. Both only observe, so a failure here never touches the ritual.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad)]
public sealed class CircleRecorder(ProfileHelper profileHelper, ItemHelper itemHelper, ISptLogger<CircleRecorder> logger) : IOnLoad
{
    private const int Keep = 15;
    private static CircleRecorder? _instance;
    private static double _lastRolled;
    private static bool _lastDirect;
    private static CircleCraftDetails? _lastDetails;

    private readonly ISptLogger<CircleRecorder> _log = logger;
    private readonly object _lock = new();
    private Dictionary<string, List<RitualRecord>>? _history;

    private static string FilePath => Path.Combine(Path.GetDirectoryName(typeof(CircleRecorder).Assembly.Location) ?? ".", "circle-history.json");

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        _instance = this;
        try
        {
            var harmony = new Harmony("com.zezinho.cultistcircleplanner");
            var roll = AccessTools.Method(typeof(CircleOfCultistService), "GetCircleCraftingInfo");
            var start = AccessTools.Method(typeof(CircleOfCultistService), nameof(CircleOfCultistService.StartSacrifice));
            if (roll is null || start is null)
            {
                logger.Warning("[CultistCircle] Circle history is off: the sacrifice methods were not found");
                return Task.CompletedTask;
            }

            harmony.Patch(roll, postfix: new HarmonyMethod(typeof(CircleRecorder), nameof(CaptureRoll)));
            harmony.Patch(start, postfix: new HarmonyMethod(typeof(CircleRecorder), nameof(AfterSacrifice)));
        }
        catch (Exception ex)
        {
            logger.Warning($"[CultistCircle] Circle history is off: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    // Postfixes run even when another mod's prefix replaced the original, so CCI's changes are seen as they are
    private static void CaptureRoll(double rewardAmountRoubles, DirectRewardSettings? directRewardSettings, CircleCraftDetails __result)
    {
        _lastRolled = rewardAmountRoubles;
        _lastDirect = directRewardSettings is not null;
        _lastDetails = __result;
    }

    private static void AfterSacrifice(MongoId sessionId, PmcData pmcData)
    {
        try
        {
            _instance?.Record(sessionId, pmcData);
        }
        catch (Exception ex)
        {
            _instance?._log.Warning($"[CultistCircle] Could not record the ritual: {ex.Message}");
        }
    }

    private void Record(MongoId sessionId, PmcData pmc)
    {
        var production = pmc.Hideout?.Production?.Values.FirstOrDefault(p => p?.SptIsCultistCircle == true);
        if (production is null || _lastDetails is null)
        {
            return;
        }

        var sacrificed = (production.GivenItemsInStart ?? []).Select(i => new RitualItem { Tpl = i.Template.ToString(), Handbook = itemHelper.GetItemPrice(i.Template) ?? 0 }).ToList();
        var items = pmc.Inventory?.Items ?? [];
        var rewards = new List<RitualItem>();
        if (pmc.Inventory?.HideoutAreaStashes?.GetValueOrDefault(((int)HideoutAreas.CircleOfCultists).ToString()) is { } stashId)
        {
            string stash = stashId.ToString();
            foreach (var item in items.Where(i => i.ParentId == stash && i.SlotId == "CircleOfCultistsGrid1"))
            {
                rewards.Add(new RitualItem { Tpl = item.Template.ToString(), Count = (int)(item.Upd?.StackObjectsCount ?? 1), HasParts = items.Any(c => c.ParentId == item.Id.ToString()) });
            }
        }

        var record = new RitualRecord
        {
            StartedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Seconds = (int)(production.ProductionTime ?? _lastDetails.Time),
            Rolled = _lastRolled,
            Base = sacrificed.Sum(s => s.Handbook),
            Skill = 1 + (pmc.GetSkillFromProfile(SkillTypes.HideoutManagement)?.Progress ?? 0) / 10000.0,
            Kind = _lastDirect ? "recipe" : _lastDetails.RewardType == CircleRewardType.HIDEOUT_TASK ? "bonus" : "tier",
            Sacrificed = sacrificed,
            Rewards = rewards,
        };

        lock (_lock)
        {
            var history = Load();
            string key = sessionId.ToString();
            if (!history.TryGetValue(key, out var list))
            {
                history[key] = list = [];
            }

            list.Insert(0, record);
            if (list.Count > Keep)
            {
                list.RemoveRange(Keep, list.Count - Keep);
            }

            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                logger.Warning($"[CultistCircle] Could not save the circle history: {ex.Message}");
            }
        }
    }

    private Dictionary<string, List<RitualRecord>> Load()
    {
        if (_history is not null)
        {
            return _history;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                _history = JsonSerializer.Deserialize<Dictionary<string, List<RitualRecord>>>(File.ReadAllText(FilePath));
            }
        }
        catch
        {
            // A broken file starts a new history
        }

        return _history ??= [];
    }

    // Newest first
    public List<RitualRecord> Read(MongoId sessionId)
    {
        lock (_lock)
        {
            return Load().TryGetValue(sessionId.ToString(), out var list) ? [.. list] : [];
        }
    }
}
