using Knowledge.Core.Domain;
using Knowledge.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Knowledge.Infrastructure.Persistence;

public class KnowledgeDbContext : DbContext
{
    private readonly int _embeddingDimensions;

    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options, IOptions<AzureOpenAiOptions> aoai)
        : base(options)
    {
        _embeddingDimensions = aoai.Value.EmbeddingDimensions;
    }

    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> Chunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeTag> Tags => Set<KnowledgeTag>();
    public DbSet<KnowledgeEntity> Entities => Set<KnowledgeEntity>();
    public DbSet<CandidateKnowledge> Candidates => Set<CandidateKnowledge>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> Messages => Set<ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeDocument>(e =>
        {
            e.ToTable("knowledge_documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired();
            e.Property(x => x.SourcePath).IsRequired();
            e.HasIndex(x => x.SourcePath).IsUnique();
            e.Property(x => x.Hash).IsRequired();
            e.HasMany(x => x.Chunks)
                .WithOne(x => x.Document!)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeChunk>(e =>
        {
            e.ToTable("knowledge_chunks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Markdown).IsRequired();
            e.Property(x => x.PlainText).IsRequired();
            e.Property(x => x.Hash).IsRequired();
            e.Property(x => x.Embedding).HasColumnType($"vector({_embeddingDimensions})");
            e.HasIndex(x => new { x.DocumentId, x.Hash });
            e.HasMany(x => x.Tags)
                .WithOne(x => x.Chunk!)
                .HasForeignKey(x => x.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Entities)
                .WithOne(x => x.Chunk!)
                .HasForeignKey(x => x.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeTag>(e =>
        {
            e.ToTable("knowledge_tags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tag).IsRequired();
            e.HasIndex(x => new { x.ChunkId, x.Tag });
        });

        modelBuilder.Entity<KnowledgeEntity>(e =>
        {
            e.ToTable("knowledge_entities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Entity).IsRequired();
            e.HasIndex(x => new { x.ChunkId, x.Entity });
        });

        modelBuilder.Entity<CandidateKnowledge>(e =>
        {
            e.ToTable("candidate_knowledge");
            e.HasKey(x => x.Id);
            e.Property(x => x.ExtractedFact).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("conversations");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Messages)
                .WithOne(x => x.Conversation!)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationMessage>(e =>
        {
            e.ToTable("conversation_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasConversion<string>();
            e.Property(x => x.Message).IsRequired();
            e.HasIndex(x => new { x.ConversationId, x.CreatedUtc });
        });
    }
}
