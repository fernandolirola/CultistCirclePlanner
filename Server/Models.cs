using System.Text.Json.Serialization;

namespace CultistCircle;

public record PlanResponse
{
    [JsonPropertyName("purchases")] public List<Purchase> Purchases { get; init; } = [];
    [JsonPropertyName("cost")] public Dictionary<string, long> Cost { get; init; } = [];
    [JsonPropertyName("money")] public Dictionary<string, long> Money { get; init; } = [];
    [JsonPropertyName("totalRub")] public long TotalRub { get; set; }
    [JsonPropertyName("operations")] public List<Dictionary<string, object>> Operations { get; init; } = [];
}

public record Purchase
{
    [JsonPropertyName("tpl")] public string Tpl { get; init; } = "";
    [JsonPropertyName("count")] public int Count { get; init; }
}
