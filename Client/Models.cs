using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CultistCircle.Client;

public class PlanResponse
{
    [JsonProperty("purchases")] public List<Purchase> Purchases = new();
    [JsonProperty("cost")] public Dictionary<string, long> Cost = new();
    [JsonProperty("money")] public Dictionary<string, long> Money = new();
    [JsonProperty("totalRub")] public long TotalRub;
    [JsonProperty("operations")] public List<JObject> Operations = new();
}

public class Purchase
{
    [JsonProperty("tpl")] public string Tpl = "";
    [JsonProperty("count")] public int Count;
}
