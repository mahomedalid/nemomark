using Knowledge.Core.Abstractions;

namespace Knowledge.Api.Endpoints;

public record AgentRequest(string Message);

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/agent").WithTags("Agent");

        group.MapPost("/", async (AgentRequest request, IKnowledgeAgent agent, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required." });
            }

            var answer = await agent.RunAsync(request.Message, ct);
            return Results.Ok(new { answer });
        });

        group.MapPost("/stream", (AgentRequest request, IKnowledgeAgent agent, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required." });
            }

            return Results.Stream(async stream =>
            {
                await using var writer = new StreamWriter(stream);
                await foreach (var token in agent.RunStreamingAsync(request.Message, ct))
                {
                    await writer.WriteAsync(token);
                    await writer.FlushAsync(ct);
                }
            }, "text/plain");
        });
    }
}
