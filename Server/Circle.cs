using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace CultistCircle;

public record CircleRequest : IRequestData
{
    // "status", "optimize", "recipes", "results", "buy" (the recipes listed in Recipes), "exclusions", "exclude" or "include" (the item in Tpl)
    [JsonPropertyName("mode")] public string Mode { get; init; } = "optimize";
    [JsonPropertyName("aim")] public int? Aim { get; init; }
    [JsonPropertyName("safety")] public double Safety { get; init; } = 0.8;
    [JsonPropertyName("recipes")] public List<int>? Recipes { get; init; }
    // Items to leave out, so the optimizer finds the next best combination
    [JsonPropertyName("skip")] public List<string>? Skip { get; init; }
    [JsonPropertyName("useStash")] public bool UseStash { get; init; }
    [JsonPropertyName("safeOffers")] public bool SafeOffers { get; init; } = true;
    [JsonPropertyName("tpl")] public string? Tpl { get; init; }
    // Items just bought for this ritual, counted as owned so a retry does not buy them twice
    [JsonPropertyName("have")] public List<string>? Have { get; init; }
}

public record CircleTier
{
    [JsonPropertyName("min")] public int Min { get; init; }
    [JsonPropertyName("max")] public int Max { get; init; }
    [JsonPropertyName("seconds")] public int Seconds { get; init; }
    [JsonPropertyName("chance")] public double Chance { get; set; }
}

public record CircleItem
{
    [JsonPropertyName("tpl")] public string Tpl { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
    [JsonPropertyName("have")] public int Have { get; init; }
    [JsonPropertyName("source")] public string Source { get; init; } = "";
    [JsonPropertyName("rub")] public long Rub { get; init; }
    [JsonPropertyName("handbook")] public long Handbook { get; init; }
}

public record CircleSolution
{
    [JsonPropertyName("items")] public List<CircleItem> Items { get; init; } = [];
    [JsonPropertyName("value")] public long Value { get; init; }
    [JsonPropertyName("cost")] public long Cost { get; init; }
    [JsonPropertyName("needed")] public long Needed { get; init; }
    // Chance of each time tier (same order as the response's tiers) and of the bonus, for this combination
    [JsonPropertyName("chances")] public List<double> Chances { get; set; } = [];
    [JsonPropertyName("bonusChance")] public double BonusChance { get; set; }
    [JsonPropertyName("buy")] public PlanResponse? Buy { get; set; }
}

public record CircleRecipe
{
    [JsonPropertyName("id")] public int Id { get; init; }
    [JsonPropertyName("inputs")] public List<CircleItem> Inputs { get; init; } = [];
    [JsonPropertyName("rewards")] public List<string> Rewards { get; init; } = [];
    [JsonPropertyName("seconds")] public int Seconds { get; init; }
    [JsonPropertyName("repeatable")] public bool Repeatable { get; init; }
    [JsonPropertyName("done")] public bool Done { get; init; }
    [JsonPropertyName("ready")] public bool Ready { get; set; }
    [JsonPropertyName("variants")] public int Variants { get; init; }
    // Cost of what is still missing; -1 when it cannot be bought
    [JsonPropertyName("cost")] public long Cost { get; set; } = -1;
    // -1 when no price is known
    [JsonPropertyName("value")] public long Value { get; set; } = -1;
    // Market price of all the inputs, owned or not, so profit does not count items you hold as free
    [JsonPropertyName("inputsWorth")] public long InputsWorth { get; set; } = -1;
}

public record CircleRitual
{
    [JsonPropertyName("startedAt")] public long StartedAt { get; init; }
    [JsonPropertyName("seconds")] public int Seconds { get; init; }
    // Seconds left, 0 once the ritual has finished
    [JsonPropertyName("endsIn")] public long EndsIn { get; init; }
    [JsonPropertyName("rolled")] public long Rolled { get; init; }
    [JsonPropertyName("baseValue")] public long BaseValue { get; init; }
    [JsonPropertyName("multiplier")] public double Multiplier { get; init; }
    [JsonPropertyName("skill")] public double Skill { get; init; }
    // "tier", "bonus" or "recipe"
    [JsonPropertyName("kind")] public string Kind { get; init; } = "tier";
    [JsonPropertyName("tierIndex")] public int TierIndex { get; init; } = -1;
    [JsonPropertyName("sacrificed")] public List<CircleItem> Sacrificed { get; init; } = [];
    // Hidden until the ritual ends
    [JsonPropertyName("rewards")] public List<CircleItem> Rewards { get; init; } = [];
    [JsonPropertyName("rewardsHidden")] public bool RewardsHidden { get; init; }
    [JsonPropertyName("cost")] public long Cost { get; init; }
    [JsonPropertyName("value")] public long Value { get; init; }
}

public record CircleResponse
{
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("aim")] public int Aim { get; set; }
    [JsonPropertyName("maxItems")] public int MaxItems { get; set; }
    [JsonPropertyName("tiers")] public List<CircleTier> Tiers { get; set; } = [];
    [JsonPropertyName("bonusSeconds")] public int BonusSeconds { get; set; }
    [JsonPropertyName("bonusChance")] public double BonusChance { get; set; }
    // The bonus reward is picked from what your hideout upgrades and started tasks need
    [JsonPropertyName("bonusPool")] public List<string> BonusPool { get; set; } = [];
    // Seconds until the running ritual ends, 0 when it is ready to collect, -1 when none runs
    [JsonPropertyName("ritual")] public long Ritual { get; set; } = -1;
    [JsonPropertyName("alternatives")] public List<CircleSolution> Alternatives { get; set; } = [];
    [JsonPropertyName("recipes")] public List<CircleRecipe>? Recipes { get; set; }
    [JsonPropertyName("buy")] public PlanResponse? Buy { get; set; }
    [JsonPropertyName("rituals")] public List<CircleRitual>? Rituals { get; set; }
    [JsonPropertyName("excluded")] public List<string>? Excluded { get; set; }
    [JsonPropertyName("excludedCategories")] public List<string>? ExcludedCategories { get; set; }
}

[Injectable(InjectionType.Singleton)]
public sealed class Circle(
    ProfileHelper profileHelper,
    HideoutConfig hideoutConfig,
    HideoutTable hideoutTable,
    OfferFinder planner,
    ItemHelper itemHelper,
    QuestHelper questHelper,
    PaymentHelper paymentHelper,
    CircleRecorder recorder,
    HashUtil hashUtil,
    GlobalTable globalTable)
{
    // Any item id works to make the planner load the trader stock and the flea index
    private static readonly MongoId Warmup = new("5449016a4bdc2d6f028b456f");
    private readonly CircleExclusions _exclusions = CircleExclusions.Load();
    private HashSet<MongoId>? _excludedItems;
    private HashSet<MongoId>? _excludedBases;

    private bool Excluded(MongoId tpl)
    {
        _excludedItems ??= _exclusions.Items.Select(e => new MongoId(e.Id)).ToHashSet();
        _excludedBases ??= _exclusions.Categories.Select(e => new MongoId(e.Id)).ToHashSet();
        return _excludedItems.Contains(tpl) || itemHelper.IsOfBaseclasses(tpl, _excludedBases);
    }

    // The circle's own grid decides what it takes: a whitelist of items and classes, minus a few, and a most items count
    private (HashSet<MongoId> allow, HashSet<MongoId> deny, int max)? _grid;

    private (HashSet<MongoId> allow, HashSet<MongoId> deny, int max) GridRules()
    {
        if (_grid is { } known)
        {
            return known;
        }

        var allow = new HashSet<MongoId>();
        var deny = new HashSet<MongoId>();
        int max = 0;
        var stash = itemHelper.GetItem(new MongoId("66740c3739b9da6ce402ee65"));
        if (stash.Key && stash.Value?.Properties?.Grids?.FirstOrDefault()?.Properties is { } props)
        {
            max = (int)(props.MaxCount ?? 0);
            foreach (var filter in props.Filters ?? [])
            {
                allow.UnionWith(filter.Filter ?? []);
                deny.UnionWith(filter.ExcludedFilter ?? []);
            }
        }

        _grid = (allow, deny, max);
        return _grid.Value;
    }

    private bool Allowed(MongoId tpl)
    {
        var (allow, deny, _) = GridRules();
        return (allow.Count == 0 || itemHelper.IsOfBaseclasses(tpl, allow)) && (deny.Count == 0 || !itemHelper.IsOfBaseclasses(tpl, deny));
    }

    private void ExclusionsChanged()
    {
        _excludedItems = null;
        _excludedBases = null;
    }

    private sealed class Ctx
    {
        public MongoId SessionId;
        public PmcData Pmc = null!;
        public bool Flea;
        public bool Safe;
        public OfferFinder.Market Market = new();
        public Dictionary<MongoId, int> Owned = new();
        public Dictionary<MongoId, int> OwnedFir = new();
        public HashSet<MongoId> HasChildren = new();
    }

    public CircleResponse Handle(MongoId sessionId, CircleRequest request)
    {
        var pmc = profileHelper.GetPmcProfile(sessionId) ?? throw new InvalidOperationException("No PMC profile");
        // CCI and SVM change this object in place, so reading it per request follows their settings
        var cfg = hideoutConfig.CultistCircle;
        double lo = cfg.RewardPriceMultiplierMinMax.Min, hi = cfg.RewardPriceMultiplierMinMax.Max;
        double skill = 1 + (pmc.GetSkillFromProfile(SkillTypes.HideoutManagement)?.Progress ?? 0) / 10000.0;

        var tiers = cfg.CraftTimeThresholds.OrderBy(t => t.Min)
            .Select(t => new CircleTier { Min = t.Min, Max = t.Max, Seconds = cfg.CraftTimeOverride != -1 ? cfg.CraftTimeOverride : t.CraftTimeSeconds }).ToList();
        var response = new CircleResponse
        {
            MaxItems = Math.Max(1, GridRules().max > 0 ? GridRules().max : cfg.MaxRewardItemCount),
            Tiers = tiers,
            BonusSeconds = cfg.CraftTimeOverride != -1 ? cfg.CraftTimeOverride : cfg.HideoutTaskRewardTimeSeconds,
            Ritual = Ritual(pmc),
        };

        // Nothing here works until the hideout's Cultist Circle is built
        if (pmc.Hideout?.Areas?.Any(a => a.Type == HideoutAreas.CircleOfCultists && (a.Level ?? 0) >= 1) != true)
        {
            response.Error = "The Cultist Circle is not unlocked in your hideout";
            return response;
        }

        // Only the ritual state, for choosing which screen to open on
        if (request.Mode == "status")
        {
            return response;
        }

        if (request.Mode is "exclusions" or "exclude" or "include")
        {
            if (request.Tpl is { Length: > 0 } tplText)
            {
                if (request.Mode == "exclude") _exclusions.Add(tplText, itemHelper.GetItemName(new MongoId(tplText)) ?? "");
                else _exclusions.Remove(tplText);
                ExclusionsChanged();
            }

            response.Excluded = _exclusions.Items.Select(e => e.Id).ToList();
            response.ExcludedCategories = _exclusions.Categories.Select(e => e.Name).ToList();
            return response;
        }

        if (tiers.Count == 0)
        {
            response.Error = "No time tiers in the server config";
            return response;
        }

        // Chance that the rolled value (base x random multiplier x skill) reaches x
        double AtLeast(double x, double value)
        {
            double needed = x / (skill * value);
            if (hi <= lo)
            {
                return needed <= lo ? 1 : 0;
            }

            return Math.Clamp((hi - needed) / (hi - lo), 0, 1);
        }

        (List<double> tier, double bonus) Chances(double value)
        {
            double bonusAt = cfg.HideoutCraftSacrificeThresholdRub;
            double bonus = cfg.BonusChanceMultiplier * AtLeast(bonusAt, value);
            var list = new List<double>();
            foreach (var tier in tiers)
            {
                double end = tier.Max + 1.0;
                double below = tier.Min < bonusAt ? Math.Max(0, AtLeast(tier.Min, value) - AtLeast(Math.Min(end, bonusAt), value)) : 0;
                double above = end > bonusAt ? Math.Max(0, AtLeast(Math.Max(tier.Min, bonusAt), value) - AtLeast(end, value)) : 0;
                list.Add(below + above * (1 - cfg.BonusChanceMultiplier));
            }

            return (list, bonus);
        }

        var ctx = new Ctx
        {
            SessionId = sessionId,
            Pmc = pmc,
            Flea = pmc.Info?.Level >= globalTable.Configuration.RagFair.MinUserLevel,
            Safe = request.SafeOffers,
        };
        ScanStash(ctx);
        planner.FindOffers(sessionId, pmc, Warmup, ctx.Flea, ctx.Market);
        response.Aim = Math.Clamp(request.Aim ?? tiers.Count - 1, 0, tiers.Count - 1);
        response.BonusPool = Needed(pmc, false).Take(12).Select(t => t.ToString()).ToList();

        if (request.Mode == "results")
        {
            response.Rituals = BuildRituals(ctx, tiers, recorder.Read(sessionId));
            return response;
        }

        if (request.Mode == "optimize")
        {
            double target = Math.Max(1, tiers[response.Aim].Min);
            double multiplier = Math.Max(lo, hi - Math.Clamp(request.Safety, 0.05, 0.99) * (hi - lo));
            double needed = Math.Ceiling(target / (skill * multiplier));
            var skip = (request.Skip ?? []).Select(s => new MongoId(s)).ToHashSet();
            var keep = request.UseStash ? Needed(pmc, true) : [];
            var have = (request.Have ?? []).Select(h => new MongoId(h)).ToHashSet();
            var candidates = Candidates(ctx, request.UseStash, keep, have);
            for (int n = 0; n < 3; n++)
            {
                var solution = Solve(ctx, candidates, skip, needed, response.MaxItems);
                if (solution is null)
                {
                    break;
                }

                var (tierChances, bonus) = Chances(solution.Value);
                solution.Chances = tierChances;
                solution.BonusChance = bonus;
                response.Alternatives.Add(solution);
                skip.UnionWith(solution.Items.Select(i => new MongoId(i.Tpl)));
            }

            if (response.Alternatives.Count == 0)
            {
                response.Error = $"No combination of up to {response.MaxItems} items reaches {needed:N0}";
            }
            else
            {
                for (int i = 0; i < tiers.Count; i++) tiers[i].Chance = response.Alternatives[0].Chances[i];
                response.BonusChance = response.Alternatives[0].BonusChance;
            }

            return response;
        }

        var recipes = BuildRecipes(ctx);
        if (request.Mode == "recipes")
        {
            response.Recipes = recipes;
            return response;
        }

        var cart = new PlanResponse();
        var used = new Dictionary<MongoId, int>();
        foreach (int id in request.Recipes ?? [])
        {
            if (id < 0 || id >= recipes.Count || recipes[id].Done)
            {
                continue;
            }

            foreach (var input in recipes[id].Inputs)
            {
                var tpl = new MongoId(input.Tpl);
                var parts = Fill(ctx, tpl, input.Count - input.Have, used);
                if (parts is null)
                {
                    response.Error = $"Cannot buy {input.Tpl}";
                    return response;
                }

                foreach (var (offer, count) in parts)
                {
                    planner.AddPurchase(cart, tpl, offer, count);
                }
            }
        }

        Money(ctx, cart);
        response.Buy = cart;
        return response;
    }

    // Seconds left on the running ritual, 0 when done, -1 when none
    private static long Ritual(PmcData pmc)
    {
        if (pmc.Hideout?.Production is not { } productions)
        {
            return -1;
        }

        foreach (var production in productions.Values)
        {
            if (production?.SptIsCultistCircle == true)
            {
                long end = (long)(production.StartTimestamp ?? 0) + (long)(production.ProductionTime ?? 0);
                return Math.Max(0, end - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        return -1;
    }

    private static void ScanStash(Ctx ctx)
    {
        var (fir, any) = OfferFinder.CountStash(ctx.Pmc);
        ctx.Owned = any;
        ctx.OwnedFir = fir;
        var items = ctx.Pmc.Inventory?.Items ?? [];
        var parents = items.Where(i => i.ParentId is not null).Select(i => i.ParentId!).ToHashSet();
        // Weapons with attachments or containers with contents would count their parts too, so they are never used as free items
        foreach (var item in items.Where(i => parents.Contains(i.Id.ToString())))
        {
            ctx.HasChildren.Add(item.Template);
        }
    }

    // Items the hideout upgrades and started tasks still ask for; the bonus reward is drawn from the next ones
    private HashSet<MongoId> Needed(PmcData pmc, bool allStages)
    {
        var set = new HashSet<MongoId>();
        foreach (var area in pmc.Hideout?.Areas ?? [])
        {
            var db = hideoutTable.Areas?.FirstOrDefault(a => a.Type == area.Type);
            foreach (var (key, stage) in db?.Stages ?? [])
            {
                int current = area.Level ?? 0;
                if (!int.TryParse(key, out int level) || (allStages ? level <= current : level != current + 1))
                {
                    continue;
                }

                foreach (var req in stage.Requirements ?? [])
                {
                    if (req.Type == "Item" && req.TemplateId is { } tpl && itemHelper.IsValidItem(tpl))
                    {
                        set.Add(tpl);
                    }
                }
            }
        }

        foreach (var quest in pmc.Quests?.Where(q => q.Status == QuestStatusEnum.Started) ?? [])
        {
            var db = questHelper.GetQuestFromDb(quest.QId, pmc);
            foreach (var condition in db?.Conditions.AvailableForFinish?.Where(c => c.ConditionType == "HandoverItem") ?? [])
            {
                foreach (string target in condition?.Target?.List ?? [])
                {
                    if (itemHelper.IsValidItem(target))
                    {
                        set.Add(new MongoId(target));
                    }
                }
            }
        }

        return set;
    }

    // Flea offers only count when enough exist and they are not far under the rest (sniped or bait listings)
    private List<OfferFinder.Offer> Offers(Ctx ctx, MongoId tpl)
    {
        var all = planner.FindOffers(ctx.SessionId, ctx.Pmc, tpl, ctx.Flea, ctx.Market);
        if (!ctx.Safe)
        {
            return all;
        }

        var flea = all.Where(o => o.Source == "flea").OrderBy(o => o.UnitRub).ToList();
        var rest = all.Where(o => o.Source != "flea").ToList();
        if (flea.Count < 3)
        {
            return rest;
        }

        double median = flea[flea.Count / 2].UnitRub;
        rest.AddRange(flea.Where(o => o.UnitRub >= median * 0.5));
        return rest;
    }

    // Cheapest offers that cover count units, or null when the market cannot
    private List<(OfferFinder.Offer offer, int count)>? Fill(Ctx ctx, MongoId tpl, int count, Dictionary<MongoId, int>? used)
    {
        var parts = new List<(OfferFinder.Offer, int)>();
        if (count <= 0)
        {
            return parts;
        }

        int left = count;
        foreach (var offer in Offers(ctx, tpl).OrderBy(o => o.UnitRub))
        {
            int free = offer.Available - (used?.GetValueOrDefault(offer.Id) ?? 0);
            int take = Math.Min(free, left);
            if (take <= 0)
            {
                continue;
            }

            parts.Add((offer, take));
            if (used is not null)
            {
                used[offer.Id] = used.GetValueOrDefault(offer.Id) + take;
            }

            left -= take;
            if (left == 0)
            {
                return parts;
            }
        }

        return null;
    }

    // What the stash holds of each currency, so the client can say when the cart is too expensive
    private static void Money(Ctx ctx, PlanResponse cart)
    {
        foreach (var currency in cart.Cost.Keys)
        {
            cart.Money[currency] = ctx.Owned.GetValueOrDefault(new MongoId(currency));
        }
    }

    private List<CircleRecipe> BuildRecipes(Ctx ctx)
    {
        var done = profileHelper.GetFullProfile(ctx.SessionId).SptData?.CultistRewards;
        var all = hideoutConfig.CultistCircle.DirectRewards;
        string Key(DirectRewardSettings r) => hashUtil.GenerateHashForData(HashingAlgorithm.MD5, string.Join(",", r.RequiredItems.OrderBy(i => i.ToString())));
        var keys = all.Select(Key).ToList();
        var variants = keys.GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());

        var priceCache = new Dictionary<MongoId, double?>();
        double? Price(MongoId tpl)
        {
            if (priceCache.TryGetValue(tpl, out var known))
            {
                return known;
            }

            // The few cheapest offers, so one bait offer does not set the price
            var low = Offers(ctx, tpl).Where(o => o.Source == "flea").OrderBy(o => o.UnitRub).Take(3).Select(o => o.UnitRub).ToList();
            double? price = low.Count > 0 ? low.Average() : itemHelper.GetDynamicItemPrice(tpl) ?? (itemHelper.GetStaticItemPrice(tpl) is var s and >= 1 ? s : null);
            return priceCache[tpl] = price;
        }

        var list = new List<CircleRecipe>();
        for (int i = 0; i < all.Count; i++)
        {
            var r = all[i];
            bool accepted = done?.ContainsKey(keys[i]) == true;
            var recipe = new CircleRecipe
            {
                Id = i,
                Inputs = r.RequiredItems.GroupBy(x => x)
                    .Select(g => new CircleItem { Tpl = g.Key.ToString(), Count = g.Count(), Have = Math.Min(g.Count(), ctx.Owned.GetValueOrDefault(g.Key)) }).ToList(),
                Rewards = r.Reward.Select(x => x.ToString()).ToList(),
                Seconds = r.CraftTimeSeconds,
                Repeatable = r.Repeatable,
                Done = accepted && !r.Repeatable,
                Variants = variants[keys[i]],
            };
            recipe.Ready = recipe.Inputs.All(x => x.Have >= x.Count);

            double worth = 0;
            bool priced = true;
            foreach (var input in recipe.Inputs)
            {
                var whole = Fill(ctx, new MongoId(input.Tpl), input.Count, null);
                if (whole is null) priced = false; else worth += whole.Sum(p => p.offer.UnitRub * p.count);
            }

            if (priced)
            {
                recipe.InputsWorth = (long)Math.Ceiling(worth);
            }

            double cost = 0;
            bool buyable = true;
            foreach (var input in recipe.Inputs)
            {
                var parts = Fill(ctx, new MongoId(input.Tpl), input.Count - input.Have, null);
                if (parts is null)
                {
                    buyable = false;
                    break;
                }

                cost += parts.Sum(p => p.offer.UnitRub * p.count);
            }

            if (buyable)
            {
                recipe.Cost = (long)Math.Ceiling(cost);
            }

            var prices = r.Reward.Select(x => Price(x)).ToList();
            if (prices.Count > 0 && prices.All(p => p is not null))
            {
                recipe.Value = (long)Math.Round(prices.Sum(p => p!.Value));
            }

            list.Add(recipe);
        }

        return list;
    }

    // The recorded rituals with what the sacrificed items cost now and what the rewards are worth now
    private List<CircleRitual> BuildRituals(Ctx ctx, List<CircleTier> tiers, List<RitualRecord> records)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        double Cost(RitualItem item)
        {
            var tpl = new MongoId(item.Tpl);
            var cheapest = Offers(ctx, tpl).OrderBy(o => o.UnitRub).FirstOrDefault();
            return cheapest?.UnitRub ?? itemHelper.GetDynamicItemPrice(tpl) ?? item.Handbook;
        }

        double Value(RitualItem item)
        {
            var tpl = new MongoId(item.Tpl);
            if (item.HasParts)
            {
                return itemHelper.GetItemMaxPrice(tpl) * item.Count;
            }

            var low = Offers(ctx, tpl).Where(o => o.Source == "flea").OrderBy(o => o.UnitRub).Take(3).Select(o => o.UnitRub).ToList();
            double unit = low.Count > 0 ? low.Average() : itemHelper.GetDynamicItemPrice(tpl) ?? itemHelper.GetStaticItemPrice(tpl);
            return unit * item.Count;
        }

        var list = new List<CircleRitual>();
        foreach (var record in records.Take(8))
        {
            long endsIn = Math.Max(0, record.StartedAt + record.Seconds - now);
            bool hidden = endsIn > 0;
            var sacrificed = record.Sacrificed.Select(s => new CircleItem { Tpl = s.Tpl, Count = 1, Handbook = (long)s.Handbook, Rub = (long)Math.Round(Cost(s)) }).ToList();
            var rewards = hidden ? new List<CircleItem>() : record.Rewards.Select(r => new CircleItem { Tpl = r.Tpl, Count = r.Count, Rub = (long)Math.Round(Value(r)) }).ToList();
            list.Add(new CircleRitual
            {
                StartedAt = record.StartedAt,
                Seconds = record.Seconds,
                EndsIn = endsIn,
                Rolled = (long)record.Rolled,
                BaseValue = (long)record.Base,
                Multiplier = record.Base > 0 && record.Skill > 0 ? record.Rolled / (record.Base * record.Skill) : 0,
                Skill = record.Skill,
                Kind = record.Kind,
                TierIndex = record.Kind == "tier" ? tiers.FindIndex(t => record.Rolled >= t.Min && record.Rolled <= t.Max) : -1,
                Sacrificed = sacrificed,
                Rewards = rewards,
                RewardsHidden = hidden,
                Cost = sacrificed.Sum(s => s.Rub),
                Value = rewards.Sum(r => r.Rub),
            });
        }

        return list;
    }

    private sealed record Candidate(MongoId Tpl, long Value, OfferFinder.Offer Offer, bool Stash);

    // Everything that could be sacrificed: bought from the market, or taken from the stash when allowed
    private List<Candidate> Candidates(Ctx ctx, bool useStash, HashSet<MongoId> keep, HashSet<MongoId> have)
    {
        var tpls = new HashSet<MongoId>();
        if (ctx.Market.Flea is not null)
        {
            tpls.UnionWith(ctx.Market.Flea.Select(g => g.Key));
        }

        foreach (var stock in ctx.Market.Traders.Values)
        {
            if (stock is { } found)
            {
                tpls.UnionWith(found.byTpl.Select(g => g.Key));
            }
        }

        if (useStash)
        {
            tpls.UnionWith(ctx.Owned.Keys);
        }

        tpls.UnionWith(have.Where(h => ctx.Owned.GetValueOrDefault(h) >= 1));

        var result = new List<Candidate>();
        foreach (var tpl in tpls)
        {
            // Stacks count once whatever their size, so money is worthless here
            if (paymentHelper.IsMoneyTpl(tpl) || !itemHelper.IsValidItem(tpl) || !Allowed(tpl) || Excluded(tpl))
            {
                continue;
            }

            double handbook = itemHelper.GetStaticItemPrice(tpl);
            if (handbook < 1)
            {
                continue;
            }

            // Free items: owned, not found in raid, not needed by the hideout or a started task, and not carrying parts
            if (have.Contains(tpl) && ctx.Owned.GetValueOrDefault(tpl) >= 1 || useStash && ctx.Owned.GetValueOrDefault(tpl) - ctx.OwnedFir.GetValueOrDefault(tpl) >= 1 && !keep.Contains(tpl) && !ctx.HasChildren.Contains(tpl))
            {
                result.Add(new Candidate(tpl, (long)handbook, new OfferFinder.Offer("stash", default, default, default, 0, 0, 1), true));
                continue;
            }

            var cheapest = Offers(ctx, tpl).OrderBy(o => o.UnitRub).FirstOrDefault();
            if (cheapest is not null)
            {
                result.Add(new Candidate(tpl, (long)handbook, cheapest, false));
            }
        }

        return result;
    }

    // Cheapest set of up to maxItems different items whose handbook prices add up to the needed value
    private CircleSolution? Solve(Ctx ctx, List<Candidate> candidates, HashSet<MongoId> skip, double needed, int maxItems)
    {
        // An item is only worth considering when nothing worth at least as much costs less
        var frontier = new List<Candidate>();
        double bestCost = double.MaxValue;
        foreach (var c in candidates.Where(c => !skip.Contains(c.Tpl)).OrderByDescending(c => c.Value).ThenBy(c => c.Offer.UnitRub))
        {
            if (c.Offer.UnitRub < bestCost)
            {
                frontier.Add(c);
                bestCost = c.Offer.UnitRub;
            }
        }

        for (int attempt = 0; attempt < 4; attempt++, needed *= 1.03)
        {
            var picked = Knapsack(frontier, needed, maxItems);
            if (picked is null)
            {
                return null;
            }

            if (picked.Sum(i => frontier[i].Value) < needed)
            {
                continue;
            }

            var cart = new PlanResponse();
            var items = new List<CircleItem>();
            foreach (int i in picked.OrderByDescending(i => frontier[i].Value))
            {
                var c = frontier[i];
                if (!c.Stash)
                {
                    planner.AddPurchase(cart, c.Tpl, c.Offer, 1);
                }

                items.Add(new CircleItem { Tpl = c.Tpl.ToString(), Count = 1, Source = c.Stash ? "stash" : c.Offer.Source, Rub = (long)Math.Ceiling(c.Offer.UnitRub), Handbook = c.Value });
            }

            Money(ctx, cart);
            return new CircleSolution { Items = items, Value = items.Sum(i => i.Handbook), Cost = items.Sum(i => i.Rub), Needed = (long)Math.Ceiling(needed), Buy = cart };
        }

        return null;
    }

    // Values are bucketed (rounded down) to keep the table small; the caller checks the real sum
    private static List<int>? Knapsack(List<Candidate> items, double needed, int maxItems)
    {
        double bucket = Math.Max(50, Math.Ceiling(needed / 400));
        int cap = (int)Math.Ceiling(needed / bucket);
        int width = cap + 1;
        var dp = new double[maxItems + 1][];
        for (int k = 0; k <= maxItems; k++)
        {
            dp[k] = Enumerable.Repeat(double.MaxValue, width).ToArray();
        }

        dp[0][0] = 0;
        // prev[item, k, b]: the bucket before taking the item when the item improved that state, else -1
        var prev = new short[items.Count * (maxItems + 1) * width];
        Array.Fill(prev, (short)-1);
        for (int i = 0; i < items.Count; i++)
        {
            int vb = (int)Math.Min(cap, (long)(items[i].Value / bucket));
            if (vb == 0)
            {
                continue;
            }

            double cost = items[i].Offer.UnitRub;
            for (int k = maxItems; k >= 1; k--)
            {
                for (int pb = 0; pb < width; pb++)
                {
                    double from = dp[k - 1][pb];
                    if (from == double.MaxValue)
                    {
                        continue;
                    }

                    int nb = Math.Min(pb + vb, cap);
                    if (from + cost < dp[k][nb])
                    {
                        dp[k][nb] = from + cost;
                        prev[(i * (maxItems + 1) + k) * width + nb] = (short)pb;
                    }
                }
            }
        }

        int bestK = -1;
        for (int k = 1; k <= maxItems; k++)
        {
            if (dp[k][cap] < (bestK < 0 ? double.MaxValue : dp[bestK][cap]))
            {
                bestK = k;
            }
        }

        if (bestK < 0)
        {
            return null;
        }

        var picked = new List<int>();
        int state = cap, count = bestK;
        for (int i = items.Count - 1; i >= 0 && count > 0; i--)
        {
            short before = prev[(i * (maxItems + 1) + count) * width + state];
            if (before >= 0)
            {
                picked.Add(i);
                state = before;
                count--;
            }
        }

        return count == 0 ? picked : null;
    }
}
