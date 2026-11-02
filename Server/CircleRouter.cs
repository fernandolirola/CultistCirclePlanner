using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Utils;

namespace CultistCircle;

[Injectable]
public sealed class CircleRouter(JsonUtil jsonUtil, Circle circle, ISptLogger<CircleRouter> logger) : StaticRouter(jsonUtil,
[
    new RouteAction<CircleRequest>("/cultistcircle/circle", (_, request, sessionId, _, _) =>
    {
        try
        {
            return new ValueTask<string>(jsonUtil.Serialize(circle.Handle(sessionId, request), typeof(CircleResponse))!);
        }
        catch (Exception ex)
        {
            logger.Error($"[CultistCircle] Request failed: {ex}");
            return new ValueTask<string>("{\"error\":" + System.Text.Json.JsonSerializer.Serialize(ex.Message) + "}");
        }
    }),
]);

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class CircleLoaded(ISptLogger<CircleLoaded> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Success($"[CultistCircle] {typeof(CircleLoaded).Assembly.GetName().Version!.ToString(3)} loaded");
        return Task.CompletedTask;
    }
}
