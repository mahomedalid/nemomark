namespace Knowledge.Core.Abstractions;

/// <summary>Ingests Markdown content into the knowledge base.</summary>
public interface IIngestionService
{
    /// <summary>Ingests (or updates) a single Markdown document identified by its source path.</summary>
    Task IngestFileAsync(string sourcePath, string markdown, CancellationToken cancellationToken = default);

    /// <summary>Ingests all Markdown files found under a directory.</summary>
    Task IngestDirectoryAsync(string directory, CancellationToken cancellationToken = default);

    /// <summary>Removes a document and its chunks from the knowledge base.</summary>
    Task RemoveFileAsync(string sourcePath, CancellationToken cancellationToken = default);
}
