using Knowledge.Core.Abstractions;

namespace Knowledge.Api.Endpoints;

public static class KnowledgeEndpoints
{
    public static void MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var search = app.MapGroup("/api/search").WithTags("Search");
        search.MapGet("/", async (string q, IKnowledgeSearchService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { error = "Query 'q' is required." });
            }

            var result = await service.SearchAsync(q, ct);
            var hits = result.Hits.Select(h => new
            {
                chunkId = h.Chunk.Id,
                document = h.Chunk.Document?.Title,
                heading = h.Chunk.Heading,
                summary = h.Chunk.Summary,
                score = Math.Round(h.Score, 4)
            });
            return Results.Ok(new { question = result.Question, hits });
        });

        var candidates = app.MapGroup("/api/candidates").WithTags("Approval");

        candidates.MapGet("/", async (IApprovalService approval, CancellationToken ct) =>
        {
            var pending = await approval.GetPendingAsync(ct);
            return Results.Ok(pending.Select(c => new
            {
                c.Id,
                c.ExtractedFact,
                c.Reason,
                c.Confidence,
                c.SourceConversationId,
                c.CreatedUtc
            }));
        });

        candidates.MapPost("/{id:guid}/approve", async (Guid id, IApprovalService approval, CancellationToken ct) =>
        {
            var ok = await approval.ApproveAsync(id, ct);
            return ok ? Results.Ok() : Results.NotFound();
        });

        candidates.MapPost("/{id:guid}/reject", async (Guid id, IApprovalService approval, CancellationToken ct) =>
        {
            var ok = await approval.RejectAsync(id, ct);
            return ok ? Results.Ok() : Results.NotFound();
        });
    }
}
