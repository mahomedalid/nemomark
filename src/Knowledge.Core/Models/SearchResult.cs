using Knowledge.Core.Domain;

namespace Knowledge.Core.Models;

/// <summary>A single scored chunk returned from a search.</summary>
public record SearchHit(KnowledgeChunk Chunk, double Score);

/// <summary>The result of a knowledge search.</summary>
public record SearchResult(string Question, IReadOnlyList<SearchHit> Hits);
