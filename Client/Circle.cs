using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using SPT.Common.Http;
using SPT.Reflection.Utils;
using UnityEngine;

namespace CultistCircle.Client;

public class CircleTier
{
    [JsonProperty("min")] public int Min;
    [JsonProperty("max")] public int Max;
    [JsonProperty("seconds")] public int Seconds;
    [JsonProperty("chance")] public double Chance;
}

public class CircleItem
{
    [JsonProperty("tpl")] public string Tpl = "";
    [JsonProperty("count")] public int Count;
    [JsonProperty("have")] public int Have;
    [JsonProperty("source")] public string Source = "";
    [JsonProperty("rub")] public long Rub;
    [JsonProperty("handbook")] public long Handbook;
}

public class CircleSolution
{
    [JsonProperty("items")] public List<CircleItem> Items = new();
    [JsonProperty("value")] public long Value;
    [JsonProperty("cost")] public long Cost;
    [JsonProperty("needed")] public long Needed;
    [JsonProperty("chances")] public List<double> Chances = new();
    [JsonProperty("bonusChance")] public double BonusChance;
    [JsonProperty("buy")] public PlanResponse? Buy;
}

public class CircleRecipe
{
    [JsonProperty("id")] public int Id;
    [JsonProperty("inputs")] public List<CircleItem> Inputs = new();
    [JsonProperty("rewards")] public List<string> Rewards = new();
    [JsonProperty("seconds")] public int Seconds;
    [JsonProperty("repeatable")] public bool Repeatable;
    [JsonProperty("done")] public bool Done;
    [JsonProperty("ready")] public bool Ready;
    [JsonProperty("variants")] public int Variants;
    [JsonProperty("cost")] public long Cost = -1;
    [JsonProperty("value")] public long Value = -1;
    [JsonProperty("inputsWorth")] public long InputsWorth = -1;

    // Something to pick: inputs to buy, or inputs already in the stash
    public bool Buyable => !Done && (Cost > 0 || Ready);
    public long? Profit => InputsWorth >= 0 && Value >= 0 ? Value - InputsWorth : (long?)null;
}

public class CircleRitual
{
    [JsonProperty("startedAt")] public long StartedAt;
    [JsonProperty("seconds")] public int Seconds;
    [JsonProperty("endsIn")] public long EndsIn;
    [JsonProperty("rolled")] public long Rolled;
    [JsonProperty("baseValue")] public long BaseValue;
    [JsonProperty("multiplier")] public double Multiplier;
    [JsonProperty("skill")] public double Skill;
    [JsonProperty("kind")] public string Kind = "tier";
    [JsonProperty("tierIndex")] public int TierIndex = -1;
    [JsonProperty("sacrificed")] public List<CircleItem> Sacrificed = new();
    [JsonProperty("rewards")] public List<CircleItem> Rewards = new();
    [JsonProperty("rewardsHidden")] public bool RewardsHidden;
    [JsonProperty("cost")] public long Cost;
    [JsonProperty("value")] public long Value;
}

public class CircleResponse
{
    [JsonProperty("error")] public string? Error;
    [JsonProperty("aim")] public int Aim;
    [JsonProperty("maxItems")] public int MaxItems;
    [JsonProperty("tiers")] public List<CircleTier> Tiers = new();
    [JsonProperty("bonusSeconds")] public int BonusSeconds;
    [JsonProperty("bonusThreshold")] public int BonusThreshold;
    [JsonProperty("bonusChance")] public double BonusChance;
    [JsonProperty("bonusPool")] public List<string> BonusPool = new();
    [JsonProperty("ritual")] public long Ritual = -1;
    [JsonProperty("alternatives")] public List<CircleSolution> Alternatives = new();
    [JsonProperty("recipes")] public List<CircleRecipe>? Recipes;
    [JsonProperty("buy")] public PlanResponse? Buy;
    [JsonProperty("rituals")] public List<CircleRitual>? Rituals;
    [JsonProperty("excluded")] public List<string>? Excluded;
    [JsonProperty("excludedCategories")] public List<string>? ExcludedCategories;
}

public partial class Plugin
{
    private static readonly double[] Safeties = { 0.95, 0.8, 0.5 };

    private enum CircleView { Optimizer, Recipes, Excluded, Results }

    private CircleView _view;
    private CircleResponse? _cc;
    private int _aim = -1;
    private int _safety = 1;
    private int _pick;
    private bool _useStash;
    private bool _safeOffers = true;
    private bool _ccPending = true;
    private bool _ccProfitable = true;
    private int _ccSort;
    private string _ccSearch = "";
    private readonly HashSet<int> _ccSel = new();
    private readonly HashSet<string> _ccSkip = new();
    // Items bought for the current ritual, kept so a retry counts them as owned
    private readonly HashSet<string> _ccBought = new();
    private readonly Dictionary<string, string> _ccResult = new();
    private bool _ccPlaced;
    private bool _ccBusy;
    private string _ccStatus = "";
    private int _ccRequests;
    private Vector2 _ccScroll;
    private GUIStyle? _wrap, _field;
    private ConfigEntry<int>? _aimCfg, _safetyCfg;
    private ConfigEntry<bool>? _stashCfg, _safeCfg;

    private void CircleInit()
    {
        if (_aimCfg != null) return;
        var hidden = new ConfigurationManagerAttributes { Browsable = false };
        _aimCfg = Config.Bind("State", "Circle aim", -1, new ConfigDescription("Time tier last picked in the Cultist Circle window", null, hidden));
        _safetyCfg = Config.Bind("State", "Circle safety", 1, new ConfigDescription("Safety last picked in the Cultist Circle window", null, hidden));
        _stashCfg = Config.Bind("State", "Circle use stash", false, new ConfigDescription("Use junk from the stash in the Cultist Circle window", null, hidden));
        _safeCfg = Config.Bind("State", "Circle safe offers", true, new ConfigDescription("Ignore flea items with few offers", null, hidden));
        _aim = _aimCfg.Value;
        _safety = Mathf.Clamp(_safetyCfg.Value, 0, 2);
        _useStash = _stashCfg.Value;
        _safeOffers = _safeCfg.Value;
    }

    // A running ritual (or one with rewards still in the circle) takes no more items, so the window opens on what is happening
    private bool Locked => _cc?.Ritual >= 0;

    private string LockText() => _cc!.Ritual == 0 ? T("COLLECT THE REWARDS FIRST", "COLETE AS RECOMPENSAS PRIMEIRO") : T($"RITUAL RUNNING  ·  ENDS IN {Duration(_cc.Ritual).ToUpperInvariant()}", $"RITUAL EM ANDAMENTO  ·  TERMINA EM {Duration(_cc.Ritual).ToUpperInvariant()}");

    private async void CircleOpen()
    {
        CircleInit();
        _cc = null;
        _ccStatus = "";
        _ccScroll = Vector2.zero;
        _ccSkip.Clear();
        _ccBought.Clear();
        _ccResult.Clear();
        _pick = 0;
        _ccSel.Clear();
        try
        {
            var status = await CirclePost(new { mode = "status" });
            if (status?.Ritual >= 0) _view = CircleView.Results;
            else if (_view == CircleView.Results) _view = CircleView.Optimizer;
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }

        CircleRefresh();
    }

    private static async System.Threading.Tasks.Task<CircleResponse?> CirclePost(object body)
    {
        var json = await RequestHandler.PostJsonAsync("/cultistcircle/circle", JsonConvert.SerializeObject(body));
        return JsonConvert.DeserializeObject<CircleResponse>(json);
    }

    private async void CircleRefresh()
    {
        int request = ++_ccRequests;
        _ccBusy = true;
        _ccPlaced = false;
        try
        {
            object body = _view switch
            {
                CircleView.Recipes => new { mode = "recipes", safeOffers = _safeOffers },
                CircleView.Excluded => new { mode = "exclusions" },
                CircleView.Results => new { mode = "results", safeOffers = _safeOffers },
                _ => new { mode = "optimize", aim = _aim >= 0 ? (int?)_aim : null, safety = Safeties[_safety], skip = _ccSkip.ToList(), useStash = _useStash, safeOffers = _safeOffers, have = _ccBought.ToList() },
            };
            var reply = await CirclePost(body);
            if (request != _ccRequests) return;
            _cc = reply;
            if (reply != null && _aim < 0) _aim = reply.Aim;
            _pick = 0;
            _ccSel.IntersectWith(reply?.Recipes?.Where(r => r.Buyable).Select(r => r.Id) ?? Enumerable.Empty<int>());
        }
        catch (Exception ex)
        {
            _ccStatus = T("Could not reach the server", "Sem resposta do servidor");
            Log.LogError(ex);
        }
        finally
        {
            if (request == _ccRequests) _ccBusy = false;
        }
    }

    private async void CircleExclude(string tpl, bool exclude)
    {
        _ccBusy = true;
        try
        {
            await CirclePost(new { mode = exclude ? "exclude" : "include", tpl });
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }

        _ccBusy = false;
        CircleRefresh();
    }

    private CircleSolution? Chosen => _cc?.Alternatives.Count > 0 ? _cc.Alternatives[Mathf.Clamp(_pick, 0, _cc.Alternatives.Count - 1)] : null;

    private async void CircleBuy()
    {
        if (InRaid || _ccBusy || ClientAppUtils.GetClientApp()?.Session is not ClientBackendSession session) return;
        _ccBusy = true;
        _ccStatus = "";
        try
        {
            var buy = _view == CircleView.Recipes ? null : Chosen?.Buy;
            if (_view == CircleView.Recipes)
            {
                var reply = await CirclePost(new { mode = "buy", recipes = _ccSel.ToList(), safeOffers = _safeOffers });
                if (reply?.Error != null)
                {
                    _ccStatus = reply.Error;
                    return;
                }

                buy = reply?.Buy;
            }

            if (buy == null || buy.Operations.Count == 0) return;
            int ok = 0;
            string? error = null;
            for (int i = 0; i < buy.Operations.Count && i < buy.Purchases.Count; i++)
            {
                string tpl = buy.Purchases[i].Tpl;
                _ccResult[tpl] = "...";
                var result = await Send(session, buy.Operations[i]);
                if (result.Succeed)
                {
                    ok++;
                    _ccResult[tpl] = "bought";
                    _ccBought.Add(tpl);
                }
                else
                {
                    var fail = Classify(result.Error);
                    error ??= result.Error;
                    _ccResult[tpl] = "failed:" + FailLabelText(fail);
                    // Money and space problems would fail the rest the same way
                    if (fail is BuyFail.Money or BuyFail.Space) break;
                }
            }

            _ccStatus = ok == buy.Operations.Count
                ? T($"Bought {ok} purchases. Put the items in the circle.", $"{ok} compras feitas. Coloque os itens no círculo.")
                : T($"Bought {ok} of {buy.Operations.Count}: {error}", $"{ok} de {buy.Operations.Count} compradas: {error}");
        }
        catch (Exception ex)
        {
            _ccStatus = ex.Message;
            Log.LogError(ex);
        }
        finally
        {
            _ccBusy = false;
        }

        if (_view == CircleView.Recipes) CircleRefresh();
    }

    // Moves items from the stash into the circle the way the game's own quick-move does: the grid's filter and size still apply
    private async void CirclePlace(List<(string tpl, int count)>? wanted = null)
    {
        wanted ??= Chosen?.Items.Select(i => (i.Tpl, 1)).ToList();
        if (InRaid || _ccBusy || Locked || wanted == null || !Singleton<HideoutRepresentation>.Instantiated) return;
        var controller = Singleton<HideoutRepresentation>.Instance._inventoryController;
        var inventory = controller?.Inventory;
        if (controller == null || inventory?.Stash == null || inventory.HideoutAreaStashes == null || !inventory.HideoutAreaStashes.TryGetValue(EAreaType.CircleOfCultists, out var circle))
        {
            _ccStatus = T("Could not find the circle's container", "Não achei o contêiner do círculo");
            return;
        }

        _ccBusy = true;
        _ccStatus = "";
        int placed = 0, total = wanted.Sum(w => w.count);
        var problems = new List<string>();
        try
        {
            foreach (var (tpl, count) in wanted)
            {
                for (int n = 0; n < count; n++)
                {
                    var candidates = inventory.Stash.GetNotMergedItems().Where(i => i.TemplateId.ToString() == tpl && i.StackObjectsCount >= 1 && i.Parent?.Container?.ParentItem != circle).ToList();
                    // A bought item is not found in raid; keep found-in-raid copies for what needs them
                    var item = candidates.FirstOrDefault(i => !i.SpawnedInSession && i.StackObjectsCount == 1) ?? candidates.FirstOrDefault(i => i.StackObjectsCount == 1);
                    if (item == null)
                    {
                        problems.Add(candidates.Count > 0 ? T($"{Name(tpl)} is a stack, split one off", $"{Name(tpl)} é uma pilha, separe uma unidade") : T($"{Name(tpl)} not in the stash", $"{Name(tpl)} não está no estoque"));
                        break;
                    }

                    var op = ItemManipulator.QuickFindAppropriatePlace(item, controller, new[] { circle }, ItemManipulator.EMoveItemOrder.MoveToAnotherSide | ItemManipulator.EMoveItemOrder.IgnoreItemParent, simulate: true);
                    if (op.Failed || !controller.CanExecute(op.Value))
                    {
                        problems.Add(T($"{Name(tpl)} does not fit", $"{Name(tpl)} não cabe"));
                        break;
                    }

                    var result = await controller.TryRunNetworkTransaction(op);
                    if (result.Succeed) placed++;
                    else
                    {
                        problems.Add($"{Name(tpl)}: {result.Error}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            problems.Add(ex.Message);
            Log.LogError(ex);
        }
        finally
        {
            _ccBusy = false;
        }

        _ccPlaced = placed == total;
        _ccStatus = problems.Count == 0
            ? T($"Placed {placed} items in the circle. Open it and press the start button.", $"{placed} itens colocados no círculo. Abra-o e aperte iniciar.")
            : T($"Placed {placed} of {total}: ", $"{placed} de {total} colocados: ") + string.Join("; ", problems.ToArray());
    }

    private string FailLabelText(BuyFail fail) => fail switch
    {
        BuyFail.Money => T("no money", "sem dinheiro"),
        BuyFail.Space => T("no space", "sem espaço"),
        BuyFail.Gone => T("sold out", "esgotado"),
        BuyFail.Limit => T("limit", "limite"),
        BuyFail.Price => T("price up", "preço subiu"),
        _ => T("failed", "falhou"),
    };

    // After a failed buy: skip what failed, count what was bought as owned, and plan again
    private void CircleRetry()
    {
        foreach (var pair in _ccResult.Where(p => p.Value.StartsWith("failed")).ToList())
        {
            _ccSkip.Add(pair.Key);
            _ccResult.Remove(pair.Key);
        }

        CircleRefresh();
    }

    private static string Hours(int seconds) => seconds >= 3600 ? (seconds / 3600.0).ToString("0.#") + "h" : Math.Max(1, seconds / 60) + " min";

    private static string Duration(long seconds) => seconds >= 3600 ? $"{seconds / 3600}h {seconds % 3600 / 60}m" : $"{Math.Max(1, seconds / 60)}m";

    private static string Range(CircleTier tier) => tier.Max >= 99999999 ? Short(tier.Min) + "+" : tier.Min <= 1 ? "up to " + Short(tier.Max) : Short(tier.Min) + "-" + Short(tier.Max);

    private void IconTip(Rect r, string tpl, string? extra = null)
    {
        DrawIcon(r, tpl);
        if (r.Contains(Event.current.mousePosition)) _tooltip = Name(tpl) + (extra != null ? "\n" + extra : "");
    }

    private void DrawCircleWindow()
    {
        _tooltip = "";
        var ui = _ui;
        GUI.Label(new Rect(Pad, 16, 300, 14), "Z E Z I N H O ' S", ui.Brand);
        GUI.Label(new Rect(Pad, 32, 420, 30), "CULTIST CIRCLE PLANNER", ui.Title);

        if (ui.FlatButton(new Rect(W - 368, 24, 100, 24), _ccBusy ? "..." : T("Refresh", "Atualizar"), false, !_ccBusy)) CircleRefresh();
        if (ui.FlatButton(new Rect(W - 150, 24, 38, 24), "EN", !PT)) _portuguese.Value = false;
        if (ui.FlatButton(new Rect(W - 112, 24, 38, 24), "PT", PT)) _portuguese.Value = true;
        if (ui.FlatButton(new Rect(W - 56, 24, 34, 24), "✕", false)) _open = false;

        string[] tabs = { T("Optimizer", "Otimizador"), T("Recipes", "Receitas"), T("Excluded items", "Itens excluídos"), T("Results", "Resultados") };
        int[] order = { 0, 1, 3, 2 };
        float[] widths = { 110, 110, 100, 140 };
        float tabX = Pad;
        for (int slot = 0; slot < order.Length; slot++)
        {
            int i = order[slot];
            if (ui.FlatButton(new Rect(tabX, 78, widths[slot], 28), tabs[i], (int)_view == i) && (int)_view != i)
            {
                _view = (CircleView)i;
                _cc = null;
                _ccScroll = Vector2.zero;
                CircleRefresh();
            }

            tabX += widths[slot] + 8;
        }

        if (_cc is { Ritual: >= 0 })
        {
            string ritual = _cc.Ritual == 0 ? T("Ritual ready to collect", "Ritual pronto para coletar") : T($"Ritual ends in {Duration(_cc.Ritual)}", $"Ritual termina em {Duration(_cc.Ritual)}");
            GUI.Label(new Rect(W - Pad - 260, 82, 260, 20), ritual, ui.Styled(ui.Muted, _cc.Ritual == 0 ? Ui.Green : Ui.Amber, null, TextAnchor.MiddleRight));
        }

        if (_cc is null)
        {
            GUI.Label(new Rect(Pad, 200, W - Pad * 2, 60), _ccStatus.Length > 0 ? _ccStatus : T("Loading...", "Carregando..."), ui.Center);
        }
        else if (_view == CircleView.Recipes)
        {
            DrawRecipes(_cc);
        }
        else if (_view == CircleView.Excluded)
        {
            DrawExcluded(_cc);
        }
        else if (_view == CircleView.Results)
        {
            DrawResults(_cc);
        }
        else
        {
            DrawOptimizer(_cc);
        }

        if (_tooltip.Length > 0)
        {
            float width = Mathf.Min(ui.Tooltip.CalcSize(new GUIContent(_tooltip)).x, 320);
            float height = ui.Tooltip.CalcHeight(new GUIContent(_tooltip), width);
            var mouse = Event.current.mousePosition;
            GUI.Label(new Rect(Mathf.Min(mouse.x + 14, W - width - 4), Mathf.Min(mouse.y + 18, H - height - 4), width, height), _tooltip, ui.Tooltip);
        }

        GUI.DragWindow(new Rect(0, 0, W, 112));
    }

    private void SectionLabel(float x, float y, string text) => GUI.Label(new Rect(x, y, 300, 14), text, _ui.ChipLabel);

    private bool CircleToggle(float x, float y, string label, bool value, string tip)
    {
        var box = new Rect(x, y, 230, 20);
        bool next = _ui.Checkbox(new Rect(x, y, 22, 20), value);
        GUI.Label(new Rect(x + 26, y, 204, 20), label, _ui.Muted);
        if (box.Contains(Event.current.mousePosition)) _tooltip = tip;
        return next;
    }

    private void DrawOptimizer(CircleResponse cc)
    {
        var ui = _ui;
        SectionLabel(Pad, 122, T("AIM FOR", "OBJETIVO"));
        float tierW = (W - Pad * 2 - 6 * (cc.Tiers.Count - 1)) / Math.Max(1, cc.Tiers.Count);
        for (int i = 0; i < cc.Tiers.Count; i++)
        {
            var tier = cc.Tiers[i];
            if (ui.FlatButton(new Rect(Pad + i * (tierW + 6), 140, tierW, 40), Hours(tier.Seconds) + "  " + Range(tier), i == cc.Aim) && i != cc.Aim)
            {
                _aim = i;
                _aimCfg!.Value = i;
                CircleRefresh();
            }
        }

        SectionLabel(Pad, 192, T("SAFETY", "SEGURANÇA"));
        string[] names = { T("Sure thing", "Garantido"), T("Likely", "Provável"), T("Cheapest", "Mais barato") };
        string[] hints = { "95%", "80%", "50%" };
        for (int i = 0; i < 3; i++)
        {
            if (ui.FlatButton(new Rect(Pad + i * 150, 210, 144, 30), names[i] + "  " + hints[i], i == _safety) && i != _safety)
            {
                _safety = i;
                _safetyCfg!.Value = i;
                CircleRefresh();
            }
        }

        bool stash = CircleToggle(Pad + 466, 204, T("Use junk from my stash", "Usar itens do estoque"), _useStash,
            T("Counts items you own for free. Items found in raid, needed by the hideout or by a started task, or carrying parts are never used.",
                "Conta itens que você já tem como grátis. Itens FIR, necessários ao esconderijo ou a tarefas ativas, ou com acessórios nunca são usados."));
        bool safe = CircleToggle(Pad + 466, 226, T("Safe flea offers", "Ofertas seguras"), _safeOffers,
            T("Ignores flea items with fewer than 3 offers and offers far below the rest.", "Ignora itens do mercado com menos de 3 ofertas e ofertas bem abaixo das outras."));
        if (stash != _useStash || safe != _safeOffers)
        {
            _useStash = stash;
            _safeOffers = safe;
            _stashCfg!.Value = stash;
            _safeCfg!.Value = safe;
            CircleRefresh();
        }

        float top = 262;
        float listW = 430;
        var solution = Chosen;
        SectionLabel(Pad, top, T("BUY THESE", "COMPRE ESTES"));
        // Alternatives, and a way to leave everything shown out and look further
        for (int i = 0; i < cc.Alternatives.Count; i++)
        {
            var chip = new Rect(Pad + 112 + i * 28, top - 4, 24, 22);
            if (ui.FlatButton(chip, (i + 1).ToString(), i == _pick)) _pick = i;
            if (chip.Contains(Event.current.mousePosition))
            {
                var option = cc.Alternatives[i];
                double optionReach = option.BonusChance + Enumerable.Range(Mathf.Clamp(cc.Aim, 0, Math.Max(0, cc.Tiers.Count - 1)), Math.Max(0, cc.Tiers.Count - cc.Aim)).Sum(t => t < option.Chances.Count ? option.Chances[t] : 0);
                _tooltip = T($"Option {i + 1}: {Short(option.Buy?.TotalRub ?? option.Cost)} ₽ to buy, {Math.Round(Math.Min(1, optionReach) * 100)}% to reach the aim", $"Opção {i + 1}: {Short(option.Buy?.TotalRub ?? option.Cost)} ₽ para comprar, {Math.Round(Math.Min(1, optionReach) * 100)}% de chegar ao objetivo");
            }
        }

        if (cc.Alternatives.Count > 0 && ui.FlatButton(new Rect(Pad + listW - 100, top - 4, 100, 22), T("More options", "Mais opções"), false, !_ccBusy))
        {
            foreach (var option in cc.Alternatives) foreach (var shown in option.Items) _ccSkip.Add(shown.Tpl);
            _ccResult.Clear();
            CircleRefresh();
        }

        if (_ccSkip.Count > 0 && ui.FlatButton(new Rect(Pad + listW - 100 - 84, top - 4, 80, 22), T($"Undo ({_ccSkip.Count})", $"Desfazer ({_ccSkip.Count})"), false, !_ccBusy))
        {
            _ccSkip.Clear();
            CircleRefresh();
        }

        _wrap ??= new GUIStyle(ui.Muted) { wordWrap = true, alignment = TextAnchor.UpperLeft };
        if (solution is null)
        {
            GUI.Label(new Rect(Pad, top + 28, listW, 60), cc.Error ?? "", _wrap);
        }
        else
        {
            for (int i = 0; i < solution.Items.Count; i++)
            {
                var item = solution.Items[i];
                var r = new Rect(Pad, top + 22 + i * 54, listW, 50);
                if (i % 2 == 0) GUI.DrawTexture(r, ui.RowAlt);
                IconTip(new Rect(r.x + 4, r.y + 3, 44, 44), item.Tpl);
                GUI.Label(new Rect(r.x + 58, r.y + 4, listW - 58 - 150, 20), $"<b>{Name(item.Tpl)}</b>", ui.Text);

                string line;
                if (_ccResult.TryGetValue(item.Tpl, out var result))
                {
                    line = result == "bought" ? $"<color=#78CC6B>{T("bought", "comprado")}</color>" : result == "..." ? "..." : $"<color=#E05C4D>{result.Substring(7)}</color>";
                }
                else if (item.Source == "stash")
                {
                    line = $"<color=#78CC6B>{T("in your stash", "no seu estoque")}</color>  ·  {T("value", "valor")} {Short(item.Handbook)} ₽";
                }
                else
                {
                    line = $"{(item.Source == "trader" ? T("trader", "comerciante") : T("flea market", "mercado"))}  ·  {T("value", "valor")} {Short(item.Handbook)} ₽";
                }

                GUI.Label(new Rect(r.x + 58, r.y + 26, listW - 58 - 150, 18), line, ui.Muted);
                GUI.Label(new Rect(r.xMax - 150, r.y + 6, 80, 20), item.Source == "stash" ? "0 ₽" : $"{Short(item.Rub)} ₽", ui.Styled(ui.Text, null, null, TextAnchor.MiddleRight));
                string ratio = item.Source == "stash" ? T("free", "grátis") : item.Handbook > 0 ? T($"{Math.Round(100.0 * item.Rub / item.Handbook)}% of value", $"{Math.Round(100.0 * item.Rub / item.Handbook)}% do valor") : "";
                GUI.Label(new Rect(r.xMax - 150, r.y + 26, 80, 16), ratio, ui.Styled(ui.Muted, null, 11, TextAnchor.MiddleRight));

                var skipBtn = new Rect(r.xMax - 66, r.y + 14, 26, 22);
                var banBtn = new Rect(r.xMax - 36, r.y + 14, 34, 22);
                if (skipBtn.Contains(Event.current.mousePosition)) _tooltip = T("Skip this item for now", "Pular este item por enquanto");
                if (banBtn.Contains(Event.current.mousePosition)) _tooltip = T("Never suggest this item again. Undo it in Excluded items.", "Nunca mais sugerir este item. Desfaça em Itens excluídos.");
                if (ui.FlatButton(skipBtn, "✕", false, !_ccBusy))
                {
                    _ccSkip.Add(item.Tpl);
                    CircleRefresh();
                }

                if (ui.FlatButton(banBtn, T("Ban", "Banir"), false, !_ccBusy)) CircleExclude(item.Tpl, true);
            }
        }

        // Right side: headline chance, each time, and what the bonus can give
        float cx = Pad + listW + 18, cw = W - Pad - cx;
        SectionLabel(cx, top, T("CHANCE", "CHANCE"));
        var tiers = cc.Tiers;
        var chances = solution?.Chances ?? tiers.Select(t => t.Chance).ToList();
        double bonus = solution?.BonusChance ?? cc.BonusChance;
        int aim = Mathf.Clamp(cc.Aim, 0, Math.Max(0, tiers.Count - 1));
        double reach = bonus + Enumerable.Range(aim, Math.Max(0, tiers.Count - aim)).Sum(i => i < chances.Count ? chances[i] : 0);
        if (tiers.Count > 0)
        {
            string percent = Math.Round(Math.Min(1, reach) * 100) + "%";
            var percentStyle = ui.Styled(ui.Title, reach >= 0.75 ? Ui.Green : reach >= 0.5 ? Ui.Amber : Ui.Red);
            float percentW = percentStyle.CalcSize(new GUIContent(percent)).x;
            GUI.Label(new Rect(cx, top + 18, percentW + 4, 34), percent, percentStyle);
            GUI.Label(new Rect(cx + percentW + 10, top + 18, cw - percentW - 10, 34), T($"to reach {Hours(tiers[aim].Seconds)} or better", $"de chegar a {Hours(tiers[aim].Seconds)} ou mais"), ui.Muted);
        }

        float y = top + 62;
        if (bonus > 0.0005)
        {
            DrawChance(new Rect(cx, y, cw, 24), Hours(cc.BonusSeconds) + " " + T("bonus", "bônus"), bonus, Ui.Orange);
            y += 28;
        }

        for (int i = tiers.Count - 1; i >= 0; i--)
        {
            double chance = i < chances.Count ? chances[i] : 0;
            if (chance < 0.0005) continue;
            DrawChance(new Rect(cx, y, cw, 24), Hours(tiers[i].Seconds), chance, Ui.Green);
            y += 28;
        }

        if (bonus > 0.0005 && cc.BonusPool.Count > 0)
        {
            y += 8;
            GUI.Label(new Rect(cx, y, cw, 14), T("BONUS CAN GIVE WHAT YOU NEED", "BÔNUS DÁ O QUE VOCÊ PRECISA"), ui.ChipLabel);
            y += 18;
            for (int i = 0; i < cc.BonusPool.Count && i < 12; i++)
            {
                IconTip(new Rect(cx + (i % 6) * 44, y + (i / 6) * 44, 40, 40), cc.BonusPool[i]);
            }

            y += (cc.BonusPool.Count > 6 ? 2 : 1) * 44;
        }

        GUI.Label(new Rect(cx, y + 8, cw, 70), T("Value x random roll x hideout skill.\nStack sizes do not count.", "Valor x sorteio x habilidade.\nTamanho de pilha não conta."), _wrap);

        DrawCircleFooter(solution?.Buy, solution != null ? T($"Base value {Short(solution.Value)} ₽  ·  needs {Short(solution.Needed)} ₽  ·  up to {cc.MaxItems} items", $"Valor base {Short(solution.Value)} ₽  ·  precisa de {Short(solution.Needed)} ₽  ·  até {cc.MaxItems} itens") : "", solution?.Items.Count ?? 0);
    }

    private void DrawCircleFooter(PlanResponse? buy, string hint, int items)
    {
        var ui = _ui;
        var footer = new Rect(Pad, H - 100, W - Pad * 2, 84);
        GUI.DrawTexture(new Rect(0, H - 101, W, 1), ui.Line);
        bool failed = _ccResult.Values.Any(v => v.StartsWith("failed"));
        bool allDone = buy != null && buy.Purchases.Count > 0 && buy.Purchases.All(p => _ccResult.TryGetValue(p.Tpl, out var v) && v == "bought");
        var shortfall = buy == null ? new List<(string currency, long missing)>() : Shortfall(buy).ToList();
        GUI.Label(new Rect(footer.x, footer.y + 4, footer.width, 18), _ccStatus.Length > 0 ? _ccStatus : hint, ui.Muted);

        string text;
        bool can = !_ccBusy && !InRaid;
        bool retry = false, place = false;
        if (InRaid) { text = T("BUYING WORKS IN THE HIDEOUT", "COMPRAS SÓ NO ESCONDERIJO"); can = false; }
        else if (Locked) { text = LockText(); can = false; }
        else if (failed) { text = T("TRY AGAIN WITHOUT THE ONES THAT FAILED", "TENTAR DE NOVO SEM OS QUE FALHARAM"); retry = true; }
        else if (_ccPlaced) { text = T("PLACED. START THE RITUAL IN THE CIRCLE", "COLOCADO. INICIE O RITUAL NO CÍRCULO"); can = false; }
        else if (allDone || (buy == null || buy.Operations.Count == 0) && items > 0) { text = T("PUT THE ITEMS IN THE CIRCLE", "COLOCAR OS ITENS NO CÍRCULO"); place = true; }
        else if (buy == null || buy.Operations.Count == 0) { text = T("NOTHING TO BUY", "NADA PARA COMPRAR"); can = false; }
        else if (shortfall.Count > 0) { text = T($"NOT ENOUGH MONEY  ·  SHORT {string.Join(" + ", shortfall.Select(s => $"{Short(s.missing)} {Currency(s.currency)}").ToArray())}", "DINHEIRO INSUFICIENTE"); can = false; }
        else text = T($"BUY {buy.Operations.Count} ITEMS  ·  {Short(buy.TotalRub)} ₽", $"COMPRAR {buy.Operations.Count} ITENS  ·  {Short(buy.TotalRub)} ₽");

        if (ui.FlatButton(new Rect(footer.x, H - Pad - 46, footer.width, 46), text, !allDone || place, can))
        {
            if (retry) CircleRetry(); else if (place) CirclePlace(); else CircleBuy();
        }
    }

    // Recipes: one at a time, since the circle runs one ritual and mixed inputs lose the fixed reward
    private void DrawRecipeFooter(CircleRecipe? picked)
    {
        var ui = _ui;
        var footer = new Rect(Pad, H - 100, W - Pad * 2, 84);
        GUI.DrawTexture(new Rect(0, H - 101, W, 1), ui.Line);
        string hint = _ccStatus.Length > 0 ? _ccStatus : picked == null
            ? T("Pick one recipe. The circle runs one ritual at a time.", "Escolha uma receita. O círculo faz um ritual por vez.")
            : T($"Reward worth {Short(Math.Max(0, picked.Value))} ₽  ·  inputs worth {Short(Math.Max(0, picked.InputsWorth))} ₽  ·  prices before flea fee", $"Recompensa vale {Short(Math.Max(0, picked.Value))} ₽  ·  itens valem {Short(Math.Max(0, picked.InputsWorth))} ₽  ·  preços sem taxa do mercado");
        GUI.Label(new Rect(footer.x, footer.y + 4, footer.width, 18), hint, ui.Muted);

        string text;
        bool can = !_ccBusy && !InRaid && !Locked;
        bool place = false;
        if (InRaid) text = T("BUYING WORKS IN THE HIDEOUT", "COMPRAS SÓ NO ESCONDERIJO");
        else if (Locked) text = LockText();
        else if (_ccPlaced) { text = T("PLACED. START THE RITUAL IN THE CIRCLE", "COLOCADO. INICIE O RITUAL NO CÍRCULO"); can = false; }
        else if (picked == null) { text = T("PICK A RECIPE", "ESCOLHA UMA RECEITA"); can = false; }
        else if (picked.Ready) { text = T("PUT THE ITEMS IN THE CIRCLE", "COLOCAR OS ITENS NO CÍRCULO"); place = true; }
        else text = T($"BUY THE INPUTS  ·  {Short(picked.Cost)} ₽", $"COMPRAR OS ITENS  ·  {Short(picked.Cost)} ₽");

        if (ui.FlatButton(new Rect(footer.x, H - Pad - 46, footer.width, 46), text, true, can))
        {
            if (place) CirclePlace(picked!.Inputs.Select(i => (i.Tpl, i.Count)).ToList()); else CircleBuy();
        }
    }

    private void DrawChance(Rect r, string label, double chance, Color color)
    {
        var ui = _ui;
        GUI.Label(new Rect(r.x, r.y, 78, r.height), label, ui.Text);
        var bar = new Rect(r.x + 82, r.y + 8, r.width - 82 - 46, 8);
        GUI.DrawTexture(bar, ui.Tint(Ui.Grey, 0.2f));
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (float)Math.Min(1, chance), bar.height), ui.Tint(color, 0.9f));
        GUI.Label(new Rect(r.xMax - 42, r.y, 42, r.height), Math.Round(chance * 100) + "%", ui.Styled(ui.Text, null, null, TextAnchor.MiddleRight));
    }

    private IEnumerable<CircleRecipe> ShownRecipes(CircleResponse cc)
    {
        string q = _ccSearch.Trim().ToLowerInvariant();
        var list = (cc.Recipes ?? new List<CircleRecipe>()).Where(r => (!_ccPending || !r.Done) && (!_ccProfitable || r.Profit > 0)
            && (q.Length == 0 || r.Inputs.Select(i => Name(i.Tpl)).Concat(r.Rewards.Select(Name)).Any(n => n.ToLowerInvariant().Contains(q))));
        switch (_ccSort)
        {
            case 1: return list.OrderByDescending(r => r.Profit.HasValue ? r.Profit.Value / Math.Max(0.1, r.Seconds / 3600.0) : double.MinValue);
            case 2: return list.OrderBy(r => r.Cost < 0 ? long.MaxValue : r.Cost);
            case 3: return list.OrderBy(r => r.Seconds);
            default: return list.OrderByDescending(r => r.Profit ?? long.MinValue);
        }
    }

    private void DrawRecipes(CircleResponse cc)
    {
        var ui = _ui;
        float y = 116;
        bool pending = ui.Checkbox(new Rect(Pad, y, 22, 24), _ccPending);
        GUI.Label(new Rect(Pad + 26, y, 110, 24), T("Pending only", "Só pendentes"), ui.Muted);
        bool profitable = ui.Checkbox(new Rect(Pad + 140, y, 22, 24), _ccProfitable);
        GUI.Label(new Rect(Pad + 166, y, 120, 24), T("Profitable only", "Só lucrativas"), ui.Muted);
        if (pending != _ccPending || profitable != _ccProfitable)
        {
            _ccPending = pending;
            _ccProfitable = profitable;
            _ccScroll = Vector2.zero;
        }

        string[] sorts = { T("Profit", "Lucro"), T("Per hour", "Por hora"), T("Cost", "Custo"), T("Time", "Tempo") };
        for (int i = 0; i < sorts.Length; i++)
        {
            if (ui.FlatButton(new Rect(W - Pad - (sorts.Length - i) * 84 + 4, y, 80, 24), sorts[i], _ccSort == i)) _ccSort = i;
        }

        // Search box: this game's skin ignores backgrounds, so the box is drawn by hand
        var search = new Rect(Pad, 146, 300, 24);
        ui.Box(search, ui.Cell, ui.CellBorder);
        _field ??= new GUIStyle(GUI.skin.textField) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 0, 0) };
        _field.normal.textColor = _field.focused.textColor = _field.hover.textColor = Ui.Cream;
        _field.normal.background = _field.focused.background = _field.hover.background = null;
        string typed = GUI.TextField(search, _ccSearch, 40, _field);
        if (typed != _ccSearch)
        {
            _ccSearch = typed;
            _ccScroll = Vector2.zero;
        }

        if (_ccSearch.Length == 0) GUI.Label(new Rect(search.x + 8, search.y, 280, search.height), T("Search item or reward", "Buscar item ou recompensa"), ui.Muted);

        float hy = 178;
        GUI.Label(new Rect(Pad + 30, hy, 120, 14), T("SACRIFICE", "SACRIFÍCIO"), ui.ChipLabel);
        GUI.Label(new Rect(Pad + 190, hy, 120, 14), T("REWARD", "RECOMPENSA"), ui.ChipLabel);
        GUI.Label(new Rect(Pad + 326, hy, 60, 14), T("TIME", "TEMPO"), ui.ChipLabel);
        GUI.Label(new Rect(Pad + 436, hy, 60, 14), T("TO BUY", "COMPRAR"), ui.ChipLabel);
        GUI.Label(new Rect(Pad + 502, hy, 70, 14), T("VALUE", "VALOR"), ui.ChipLabel);
        GUI.Label(new Rect(Pad + 584, hy, 100, 14), T("PROFIT", "LUCRO"), ui.ChipLabel);

        var body = new Rect(Pad, 196, W - Pad * 2, H - 196 - 106);
        GUILayout.BeginArea(body);
        _ccScroll = GUILayout.BeginScrollView(_ccScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
        var rows = ShownRecipes(cc).ToList();
        if (rows.Count == 0) GUILayout.Label(T("No recipes match. Untick the filters to see more.", "Nenhuma receita encontrada. Desmarque os filtros."), ui.Center, GUILayout.Height(120));
        for (int i = 0; i < rows.Count; i++) DrawRecipeRow(rows[i], body.width - 20, i % 2 == 1);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawRecipeFooter((cc.Recipes ?? new List<CircleRecipe>()).FirstOrDefault(r => _ccSel.Contains(r.Id)));
    }

    // The sacrifice reads better as hours and minutes than as a decimal
    private static string Span(int seconds) => seconds >= 3600 ? (seconds % 3600 >= 60 ? $"{seconds / 3600}h {seconds % 3600 / 60:00}m" : $"{seconds / 3600}h") : $"{Math.Max(1, seconds / 60)}m";

    private void DrawRecipeRow(CircleRecipe recipe, float width, bool alt)
    {
        var ui = _ui;
        var r = GUILayoutUtility.GetRect(width, 52);
        bool picked = _ccSel.Contains(recipe.Id);
        if (alt) GUI.DrawTexture(r, ui.RowAlt);
        if (picked) GUI.DrawTexture(r, ui.Tint(Ui.Orange, 0.10f));
        else if (r.Contains(Event.current.mousePosition)) GUI.DrawTexture(r, ui.Hover);
        var old = GUI.color;
        GUI.color = new Color(1, 1, 1, recipe.Done ? 0.5f : 1f);

        // One at a time: ticking a recipe unticks the previous one
        if (recipe.Buyable && ui.Checkbox(new Rect(r.x + 4, r.y, 22, r.height), picked) != picked)
        {
            _ccSel.Clear();
            if (!picked) _ccSel.Add(recipe.Id);
            _ccPlaced = false;
        }

        float x = r.x + 30;
        foreach (var input in recipe.Inputs.Take(3))
        {
            IconTip(new Rect(x, r.y + 6, 40, 40), input.Tpl, input.Count > 1 || input.Have > 0 ? $"{input.Have} / {input.Count} {T("in stash", "no estoque")}" : null);
            if (input.Count > 1 || input.Have > 0)
            {
                // The count sits on a dark strip so it stays readable over the icon
                GUI.DrawTexture(new Rect(x + 12, r.y + 32, 28, 14), ui.Tint(Color.black, 0.65f));
                GUI.Label(new Rect(x, r.y + 32, 39, 14), $"{input.Have}/{input.Count}", ui.Styled(ui.Badge, input.Have >= input.Count ? Ui.Green : Ui.Cream, 10, TextAnchor.MiddleRight));
            }

            x += 42;
        }

        if (recipe.Inputs.Count > 3) GUI.Label(new Rect(x, r.y, 24, r.height), "+" + (recipe.Inputs.Count - 3), ui.Muted);
        GUI.Label(new Rect(r.x + 160, r.y, 24, r.height), "to", ui.Muted);
        x = r.x + 186;
        foreach (var reward in recipe.Rewards.Take(3).GroupBy(t => t))
        {
            IconTip(new Rect(x, r.y + 6, 40, 40), reward.Key);
            if (reward.Count() > 1)
            {
                GUI.DrawTexture(new Rect(x + 12, r.y + 32, 28, 14), ui.Tint(Color.black, 0.65f));
                GUI.Label(new Rect(x, r.y + 32, 39, 14), "x" + reward.Count(), ui.Styled(ui.Badge, Ui.Cream, 10, TextAnchor.MiddleRight));
            }

            x += 42;
        }

        GUI.Label(new Rect(r.x + 322, r.y + 2, 100, 24), Span(recipe.Seconds), ui.Text);
        var (label, color) = recipe.Done ? (T("DONE", "FEITA"), Ui.Grey)
            : recipe.Ready ? (T("READY", "PRONTA"), Ui.Green)
            : recipe.Cost < 0 ? (T("NOT SOLD", "SEM OFERTA"), Ui.Red)
            : recipe.Repeatable ? (T("REPEATABLE", "REPETÍVEL"), Ui.Green) : (T("ONE-TIME", "ÚNICA"), Ui.Orange);
        var pill = new Rect(r.x + 322, r.y + 28, 104, 18);
        ui.QuietPill(pill, recipe.Variants > 1 ? label + "  1/" + recipe.Variants : label, color);
        if (recipe.Variants > 1 && pill.Contains(Event.current.mousePosition))
            _tooltip = T($"{recipe.Variants} recipes share these inputs. The game picks one at random.", $"{recipe.Variants} receitas usam estes itens. O jogo sorteia uma.");

        GUI.Label(new Rect(r.x + 434, r.y, 66, r.height), recipe.Cost < 0 ? "-" : recipe.Cost == 0 ? T("owned", "tem") : Short(recipe.Cost), recipe.Cost == 0 ? ui.Styled(ui.Muted, Ui.Green) : ui.Text);
        GUI.Label(new Rect(r.x + 502, r.y, 70, r.height), recipe.Value < 0 ? T("no price", "sem preço") : Short(recipe.Value), recipe.Value < 0 ? ui.Muted : ui.Text);
        var profit = recipe.Profit;
        string text = profit is null ? "-" : (profit >= 0 ? "+" : "-") + Short(Math.Abs(profit.Value));
        GUI.Label(new Rect(r.x + 584, r.y, 112, 28), text, ui.Styled(ui.Header, profit is null ? Ui.Grey : profit >= 0 ? Ui.Green : Ui.Red));
        if (profit != null) GUI.Label(new Rect(r.x + 584, r.y + 28, 112, 18), Short((long)(profit.Value / Math.Max(0.1, recipe.Seconds / 3600.0))) + "/h", ui.Muted);
        GUI.color = old;
    }

    private static string Stamp(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("MM-dd HH:mm");

    private static string Signed(long value) => (value >= 0 ? "+" : "-") + Short(Math.Abs(value));

    // A row of item tiles with the price of each under it
    private void DrawRitualItems(float x, float y, List<CircleItem> items)
    {
        var ui = _ui;
        for (int i = 0; i < items.Count && i < 12; i++)
        {
            var item = items[i];
            float tx = x + i * 58;
            IconTip(new Rect(tx, y, 48, 48), item.Tpl);
            if (item.Count > 1) GUI.Label(new Rect(tx, y + 32, 48, 16), "x" + item.Count, ui.Styled(ui.Badge, Ui.Cream, 10, TextAnchor.LowerRight));
            GUI.Label(new Rect(tx - 4, y + 50, 56, 14), Short(item.Rub), ui.Styled(ui.Muted, null, 11, TextAnchor.MiddleCenter));
        }
    }

    private void DrawResults(CircleResponse cc)
    {
        var ui = _ui;
        var rituals = cc.Rituals ?? new List<CircleRitual>();
        if (rituals.Count == 0)
        {
            GUI.Label(new Rect(Pad, 200, W - Pad * 2, 80), T("No ritual recorded yet. The next sacrifice is saved here with its roll and rewards.", "Nenhum ritual registrado. O próximo sacrifício é salvo aqui com o sorteio e as recompensas."), ui.Center);
            return;
        }

        var last = rituals[0];
        bool running = last.EndsIn > 0;
        GUI.Label(new Rect(Pad, 120, 460, 22), running ? T($"RITUAL RUNNING  ·  ends in {Duration(last.EndsIn)}", $"RITUAL EM ANDAMENTO  ·  termina em {Duration(last.EndsIn)}") : T("LAST RITUAL  ·  FINISHED", "ÚLTIMO RITUAL  ·  CONCLUÍDO"), ui.Styled(ui.Header, running ? Ui.Amber : Ui.Green));
        GUI.Label(new Rect(W - Pad - 200, 120, 200, 22), Stamp(last.StartedAt), ui.Styled(ui.Muted, null, null, TextAnchor.MiddleRight));

        string roll = last.Kind == "recipe"
            ? T("Fixed recipe: the reward does not depend on a roll.", "Receita fixa: a recompensa não depende de sorteio.")
            : T($"Base value {Short(last.BaseValue)}  x  roll {last.Multiplier:0.00}  x  hideout skill {last.Skill:0.00}  =  {Short(last.Rolled)}", $"Valor base {Short(last.BaseValue)}  x  sorteio {last.Multiplier:0.00}  x  habilidade {last.Skill:0.00}  =  {Short(last.Rolled)}");
        GUI.Label(new Rect(Pad, 146, W - Pad * 2, 20), roll, ui.Text);

        string landed;
        if (last.Kind == "bonus") landed = T($"Bonus roll: {Hours(last.Seconds)}, rewards come from what your hideout and tasks need.", $"Sorteio bônus: {Hours(last.Seconds)}, as recompensas vêm do que seu esconderijo e tarefas precisam.");
        else if (last.Kind == "recipe") landed = T($"Recipe ritual: {Hours(last.Seconds)}.", $"Ritual de receita: {Hours(last.Seconds)}.");
        else if (last.TierIndex >= 0 && last.TierIndex < cc.Tiers.Count) landed = T($"Landed in the {Hours(cc.Tiers[last.TierIndex].Seconds)} tier ({Range(cc.Tiers[last.TierIndex])}).", $"Caiu na faixa de {Hours(cc.Tiers[last.TierIndex].Seconds)} ({Range(cc.Tiers[last.TierIndex])}).");
        else landed = T($"Ritual time {Hours(last.Seconds)}.", $"Tempo do ritual {Hours(last.Seconds)}.");
        GUI.Label(new Rect(Pad, 168, W - Pad * 2, 20), landed, ui.Styled(ui.Text, Ui.Orange));

        GUI.Label(new Rect(Pad, 204, 300, 14), T("SACRIFICED (COST NOW)", "SACRIFICADO (CUSTO ATUAL)"), ui.ChipLabel);
        DrawRitualItems(Pad, 222, last.Sacrificed);

        GUI.Label(new Rect(Pad, 300, 300, 14), T("REWARDS (WORTH NOW)", "RECOMPENSAS (VALOR ATUAL)"), ui.ChipLabel);
        if (last.RewardsHidden)
        {
            for (int i = 0; i < 5; i++)
            {
                ui.Box(new Rect(Pad + i * 58, 318, 48, 48), ui.Cell, ui.CellBorder);
                GUI.Label(new Rect(Pad + i * 58, 318, 48, 48), "?", ui.Styled(ui.Header, Ui.Grey, 20, TextAnchor.MiddleCenter));
            }

            GUI.Label(new Rect(Pad + 306, 318, W - Pad * 2 - 306, 48), T("Revealed when the ritual ends.", "Revelado quando o ritual termina."), ui.Muted);
        }
        else
        {
            DrawRitualItems(Pad, 318, last.Rewards);
        }

        // Cost against what came back
        float by = 392;
        float bw = (W - Pad * 2 - 20) / 3f;
        long profit = last.Value - last.Cost;
        string[] captions = { T("COST OF THE SACRIFICE", "CUSTO DO SACRIFÍCIO"), T("REWARDS WORTH", "RECOMPENSAS VALEM"), T("PROFIT OR LOSS", "LUCRO OU PERDA") };
        string[] values = { Short(last.Cost) + " ₽", last.RewardsHidden ? "?" : Short(last.Value) + " ₽", last.RewardsHidden ? "?" : Signed(profit) + " ₽" };
        for (int i = 0; i < 3; i++)
        {
            var box = new Rect(Pad + i * (bw + 10), by, bw, 58);
            ui.Box(box, ui.Card, ui.Line);
            GUI.Label(new Rect(box.x + 12, box.y + 6, box.width - 24, 14), captions[i], ui.ChipLabel);
            var color = i == 2 && !last.RewardsHidden ? (profit >= 0 ? Ui.Green : Ui.Red) : Ui.Cream;
            GUI.Label(new Rect(box.x + 12, box.y + 22, box.width - 24, 30), values[i], ui.Styled(ui.ChipValue, color));
        }

        GUI.Label(new Rect(Pad, by + 64, W - Pad * 2, 16), T("Prices are today's flea and trader prices, not what you paid.", "Preços são os de hoje no mercado e comerciantes, não o que você pagou."), ui.Muted);

        string next = running || last.EndsIn == 0 && cc.Ritual == 0 ? T("PLAN THE NEXT RITUAL", "PLANEJAR O PRÓXIMO RITUAL") : T("PLAN A NEW RITUAL", "PLANEJAR UM NOVO RITUAL");
        if (ui.FlatButton(new Rect(Pad, H - Pad - 46, W - Pad * 2, 46), next, false))
        {
            _view = CircleView.Optimizer;
            _cc = null;
            _ccScroll = Vector2.zero;
            CircleRefresh();
        }

        if (rituals.Count > 1)
        {
            GUI.Label(new Rect(Pad, 486, 300, 14), T("PREVIOUS RITUALS", "RITUAIS ANTERIORES"), ui.ChipLabel);
            for (int i = 1; i < rituals.Count && i < 7; i++)
            {
                var past = rituals[i];
                var row = new Rect(Pad, 506 + (i - 1) * 32, W - Pad * 2, 30);
                if (i % 2 == 1) GUI.DrawTexture(row, ui.RowAlt);
                string kind = past.Kind == "bonus" ? T("bonus", "bônus") : past.Kind == "recipe" ? T("recipe", "receita") : past.TierIndex >= 0 && past.TierIndex < cc.Tiers.Count ? Hours(cc.Tiers[past.TierIndex].Seconds) : Hours(past.Seconds);
                long pastProfit = past.Value - past.Cost;
                GUI.Label(new Rect(row.x + 8, row.y, 130, row.height), Stamp(past.StartedAt), ui.Muted);
                GUI.Label(new Rect(row.x + 150, row.y, 120, row.height), kind, ui.Text);
                GUI.Label(new Rect(row.x + 290, row.y, 140, row.height), T("cost ", "custo ") + Short(past.Cost), ui.Muted);
                GUI.Label(new Rect(row.x + 440, row.y, 130, row.height), T("worth ", "vale ") + Short(past.Value), ui.Muted);
                GUI.Label(new Rect(row.xMax - 130, row.y, 122, row.height), Signed(pastProfit) + " ₽", ui.Styled(ui.Header, pastProfit >= 0 ? Ui.Green : Ui.Red, null, TextAnchor.MiddleRight));
            }
        }
    }

    private void DrawExcluded(CircleResponse cc)
    {
        var ui = _ui;
        _wrap ??= new GUIStyle(ui.Muted) { wordWrap = true, alignment = TextAnchor.UpperLeft };
        string cats = cc.ExcludedCategories is { Count: > 0 } ? string.Join(", ", cc.ExcludedCategories.ToArray()) : "-";
        GUI.Label(new Rect(Pad, 120, W - Pad * 2, 52), T("The optimizer never suggests these categories: ", "O otimizador nunca sugere estas categorias: ") + cats + T(". Edit circle-exclusions.json in the server mod folder to change them.", ". Edite circle-exclusions.json na pasta do mod no servidor para mudar."), _wrap);

        var groups = (cc.Excluded ?? new List<string>()).GroupBy(Name).OrderBy(g => g.Key).ToList();
        GUI.Label(new Rect(Pad, 176, 300, 14), T($"EXCLUDED ITEMS ({groups.Sum(g => g.Count())})", $"ITENS EXCLUÍDOS ({groups.Sum(g => g.Count())})"), ui.ChipLabel);
        var body = new Rect(Pad, 196, W - Pad * 2, H - 196 - 24);
        GUILayout.BeginArea(body);
        _ccScroll = GUILayout.BeginScrollView(_ccScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var r = GUILayoutUtility.GetRect(body.width - 20, 44);
            if (i % 2 == 0) GUI.DrawTexture(r, ui.RowAlt);
            IconTip(new Rect(r.x + 4, r.y + 3, 38, 38), group.First());
            GUI.Label(new Rect(r.x + 52, r.y, 420, r.height), group.Key + (group.Count() > 1 ? $"  <color=#9a9a94>x{group.Count()}</color>" : ""), ui.Text);
            if (ui.FlatButton(new Rect(r.xMax - 110, r.y + 10, 100, 24), T("Allow again", "Permitir"), false, !_ccBusy))
            {
                foreach (var tpl in group.ToList()) CircleExclude(tpl, false);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
