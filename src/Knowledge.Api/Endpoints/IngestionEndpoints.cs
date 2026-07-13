using Knowledge.Core.Abstractions;

namespace Knowledge.Api.Endpoints;

public record IngestFileRequest(string SourcePath, string Markdown);

public record IngestDirectoryRequest(string Directory);

public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingest").WithTags("Ingestion");

        group.MapPost("/file", async (IngestFileRequest request, IIngestionService ingestion, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.SourcePath) || string.IsNullOrWhiteSpace(request.Markdown))
            {
                return Results.BadRequest(new { error = "SourcePath and Markdown are required." });
            }

            await ingestion.IngestFileAsync(request.SourcePath, request.Markdown, ct);
            return Results.Accepted();
        });

        group.MapPost("/directory", async (IngestDirectoryRequest request, IIngestionService ingestion, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Directory))
            {
                return Results.BadRequest(new { error = "Directory is required." });
            }

            await ingestion.IngestDirectoryAsync(request.Directory, ct);
            return Results.Accepted();
        });

        group.MapDelete("/file", async (string sourcePath, IIngestionService ingestion, CancellationToken ct) =>
        {
            await ingestion.RemoveFileAsync(sourcePath, ct);
            return Results.NoContent();
        });
    }
}
