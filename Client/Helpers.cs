using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using Newtonsoft.Json.Linq;
using SPT.Reflection.Utils;

namespace CultistCircle.Client;

public partial class Plugin
{
    private static readonly HashSet<string> MoneyTpls = new() { "5449016a4bdc2d6f028b456f", "5696686a4bdc2da3298b456a", "569668774bdc2da2298b4568", "5d235b4d86f7742e017bc88a" };

    private enum BuyFail { Gone, Money, Space, Limit, Other }

    private static BuyFail Classify(string? error)
    {
        bool Has(string s) => error?.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
        if (Has("space")) return BuyFail.Space;
        if (Has("money")) return BuyFail.Money;
        if (Has("limit")) return BuyFail.Limit;
        if (Has("offer") || Has("exist") || Has("not found") || Has("stock")) return BuyFail.Gone;
        return BuyFail.Other;
    }

    private static async Task<IResult> Send(ClientBackendSession session, JObject op)
    {
        // Continuing inside the game's callback would queue the next purchase before the game finishes
        // handling this one, and a warning there clears its queue, losing that purchase
        var source = new TaskCompletionSource<IResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SendOperationRightNow(op, result => source.TrySetResult(result));
        var finished = await Task.WhenAny(source.Task, Task.Delay(20000));
        await Task.Yield();
        return finished == source.Task ? source.Task.Result : new FailedResult("Timed out");
    }

    private sealed class FailedResult(string error) : IResult
    {
        public string Error => error;
        public bool Succeed => false;
        public bool Failed => true;
        public int ErrorCode => 0;
    }

    private static IEnumerable<(string currency, long missing)> Shortfall(PlanResponse plan) =>
        // Dollars and euros bought in the same run count as money on hand
        plan.Cost.Select(c => (c.Key, c.Value - (plan.Money.TryGetValue(c.Key, out var have) ? have : 0) - plan.Purchases.Where(p => p.Tpl == c.Key).Sum(p => (long)p.Count)))
            .Where(s => s.Item2 > 0);

    private static string Short(long value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000d).ToString("0.##") + "M",
        >= 1_000 => (value / 1000d).ToString("0.#") + "k",
        _ => value.ToString("N0"),
    };

    // Some items (the sealed weapon cases) have no short name, so the full one is used
    private static string Name(string tpl)
    {
        string key = tpl + " ShortName";
        string name = key.Localized();
        if (string.IsNullOrWhiteSpace(name) || name == key) name = (tpl + " Name").Localized();
        return Clean(name);
    }

    // Other mods mark needed items with stars in their names
    private static string Clean(string name) => name.TrimStart('★', '☆', ' ');

    private static string Currency(string tpl) => tpl switch
    {
        "5449016a4bdc2d6f028b456f" => "₽",
        "5696686a4bdc2da3298b456a" => "$",
        "569668774bdc2da2298b4568" => "€",
        _ => Name(tpl),
    };

    private void DrawIcon(UnityEngine.Rect r, string tpl)
    {
        var ui = _ui;
        ui.Box(r, ui.Cell, ui.CellBorder);
        if (UnityEngine.Event.current.type != UnityEngine.EventType.Repaint)
        {
            return;
        }

        if (MoneyTpls.Contains(tpl))
        {
            UnityEngine.GUI.Label(r, Currency(tpl), ui.Styled(ui.Header, Ui.Orange, 18, UnityEngine.TextAnchor.MiddleCenter));
            return;
        }

        _icons.Draw(new UnityEngine.Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), tpl);
    }
}
