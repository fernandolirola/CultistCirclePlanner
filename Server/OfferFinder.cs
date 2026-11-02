using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Utils;

namespace CultistCircle;

/// <summary>Trader stock and flea offers, searched per item, plus the purchase operations the client sends to buy them.</summary>
[Injectable(InjectionType.Singleton)]
public sealed class OfferFinder(
    TradersTable tradersTable,
    TraderAssortHelper traderAssortHelper,
    TraderPurchasePersisterService purchasePersister,
    RagfairOfferService ragfairOfferService,
    HandbookHelper handbookHelper,
    PaymentHelper paymentHelper,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    ISptLogger<OfferFinder> logger)
{
    internal sealed record Offer(string Source, MongoId Trader, MongoId Id, MongoId Currency, double UnitPrice, double UnitRub, int Available, bool Fir);

    internal void AddPurchase(PlanResponse response, MongoId tpl, Offer offer, int count)
    {
        long price = (long)Math.Ceiling(offer.UnitPrice * count);
        long rub = (long)Math.Ceiling(offer.UnitRub * count);
        response.Purchases.Add(new Purchase { Tpl = tpl.ToString(), Count = count, Source = offer.Source, Trader = offer.Trader.ToString(), Currency = offer.Currency.ToString(), Price = price, Rub = rub });
        response.Cost[offer.Currency.ToString()] = response.Cost.GetValueOrDefault(offer.Currency.ToString()) + price;
        response.TotalRub += rub;

        var payment = new List<Dictionary<string, object>> { new() { ["id"] = offer.Currency.ToString(), ["count"] = price } };
        if (offer.Source == "trader")
        {
            response.Operations.Add(new()
            {
                ["Action"] = "TradingConfirm",
                ["type"] = "buy_from_trader",
                ["tid"] = offer.Trader.ToString(),
                ["item_id"] = offer.Id.ToString(),
                ["count"] = count,
                ["scheme_id"] = 0,
                ["scheme_items"] = payment,
            });
        }
        else
        {
            // One offer per request: a single sold-out offer (BetterFlea snipes some at purchase) would fail a whole cart
            response.Operations.Add(new()
            {
                ["Action"] = "RagFairBuyOffer",
                ["offers"] = new List<Dictionary<string, object>> { new() { ["id"] = offer.Id.ToString(), ["count"] = count, ["items"] = payment } },
            });
        }
    }

    internal sealed class Market
    {
        public readonly Dictionary<MongoId, (TraderAssort assort, ILookup<MongoId, Item> byTpl, HashSet<string> parents)?> Traders = new();
        public ILookup<MongoId, RagfairOffer>? Flea;
    }

    internal List<Offer> FindOffers(MongoId sessionId, PmcData pmc, MongoId tpl, bool fleaUnlocked, Market market)
    {
        var result = new List<Offer>();
        foreach (var traderId in tradersTable.Keys)
        {
            if (traderId == Traders.FENCE || pmc.TradersInfo?.GetValueOrDefault(traderId) is not { Unlocked: true } info)
            {
                continue;
            }

            try
            {
                if (!market.Traders.TryGetValue(traderId, out var stock))
                {
                    // Null when the trader failed, so it is not retried for every item
                    market.Traders[traderId] = null;
                    var full = traderAssortHelper.GetAssort(sessionId, traderId);
                    market.Traders[traderId] = stock = (full, full.Items.Where(i => i.ParentId == "hideout").ToLookup(i => i.Template),
                        new HashSet<string>(full.Items.Where(i => i.ParentId is not null).Select(i => i.ParentId!)));
                }

                if (stock is not { } found)
                {
                    continue;
                }

                var (assort, byTpl, parents) = found;

                foreach (var item in byTpl[tpl])
                {
                    if (parents.Contains(item.Id.ToString())
                        || assort.LoyalLevelItems.GetValueOrDefault(item.Id, 1) > (info.LoyaltyLevel ?? 1)
                        || assort.BarterScheme.GetValueOrDefault(item.Id)?.FirstOrDefault() is not [{ } pay] || !paymentHelper.IsMoneyTpl(pay.Template) || pay.Template == tpl)
                    {
                        continue;
                    }

                    int available = item.Upd?.UnlimitedCount == true ? 999999 : (int)(item.Upd?.StackObjectsCount ?? 0);
                    if (item.Upd?.BuyRestrictionMax is { } max)
                    {
                        int bought = (int)(purchasePersister.GetProfileTraderPurchase(sessionId, traderId, item.Id.ToString())?.PurchaseCount ?? 0);
                        available = Math.Min(available, (int)max - bought);
                    }

                    double unit = pay.Count ?? 0;
                    if (available > 0 && unit > 0)
                    {
                        result.Add(new Offer("trader", traderId, item.Id, pay.Template, unit, handbookHelper.InRoubles(unit, pay.Template), available, traderConfig.PurchasesAreFoundInRaid));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"[ShoppingList] Skipped trader {traderId}: {ex.Message}");
            }
        }

        if (!fleaUnlocked)
        {
            return result;
        }

        market.Flea ??= ragfairOfferService.GetOffers().Where(o => o.Items is [_]).ToLookup(o => o.Items![0].Template);
        foreach (var offer in market.Flea[tpl])
        {
            if (offer.Items is not [{ } item] || item.Template != tpl || offer.SellInOnePiece == true || offer.Locked == true
                || offer.User.Id == pmc.Id || offer.Quantity <= 0
                || offer.Requirements?.ToList() is not [{ } pay] || !paymentHelper.IsMoneyTpl(pay.TemplateId))
            {
                continue;
            }

            if (offer.IsTraderOffer() && (pmc.TradersInfo?.GetValueOrDefault(offer.User.Id)?.LoyaltyLevel ?? 0) < (offer.LoyaltyLevel ?? 1))
            {
                continue;
            }

            int available = offer.Quantity;
            if (offer.BuyRestrictionMax is { } max)
            {
                available = Math.Min(available, max - (offer.BuyRestrictionCurrent ?? 0));
            }

            double unit = pay.Count ?? 0;
            if (available > 0 && unit > 0)
            {
                bool fir = offer.IsTraderOffer() ? traderConfig.PurchasesAreFoundInRaid : ragfairConfig.Dynamic.PurchasesAreFoundInRaid;
                result.Add(new Offer("flea", offer.User.Id, offer.Id, pay.TemplateId, unit, handbookHelper.InRoubles(unit, pay.TemplateId), available, fir));
            }
        }

        return result;
    }

    internal static (Dictionary<MongoId, int> fir, Dictionary<MongoId, int> any) CountStash(PmcData pmc)
    {
        var items = pmc.Inventory?.Items ?? [];
        var stashId = pmc.Inventory?.Stash?.ToString();
        var children = items.Where(i => i.ParentId is not null).ToLookup(i => i.ParentId!);
        var fir = new Dictionary<MongoId, int>();
        var any = new Dictionary<MongoId, int>();

        var stack = new Stack<string>();
        if (stashId is not null)
        {
            stack.Push(stashId);
        }

        while (stack.Count > 0)
        {
            foreach (var item in children[stack.Pop()])
            {
                int count = (int)(item.Upd?.StackObjectsCount ?? 1);
                any[item.Template] = any.GetValueOrDefault(item.Template) + count;
                if (item.Upd?.SpawnedInSession == true)
                {
                    fir[item.Template] = fir.GetValueOrDefault(item.Template) + count;
                }

                stack.Push(item.Id.ToString());
            }
        }

        return (fir, any);
    }
}
