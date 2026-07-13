using Knowledge.Core.Abstractions;
using Knowledge.Core.Models;

namespace Knowledge.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        group.MapPost("/", async (ChatRequest request, IChatService chat, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required." });
            }

            var response = await chat.ChatAsync(request, ct);
            return Results.Ok(response);
        });

        group.MapPost("/stream", (ChatRequest request, IChatService chat, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required." });
            }

            return Results.Stream(async stream =>
            {
                await using var writer = new StreamWriter(stream);
                await foreach (var token in chat.ChatStreamingAsync(request, ct))
                {
                    await writer.WriteAsync(token);
                    await writer.FlushAsync(ct);
                }
            }, "text/plain");
        });
    }
}
