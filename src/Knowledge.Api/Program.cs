using Knowledge.Api.Endpoints;
using Knowledge.Infrastructure;
using Knowledge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKnowledgeServices(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure the database schema (and pgvector extension) exists on startup.
if (app.Configuration.GetValue("Knowledge:EnsureDatabaseCreated", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new { service = "Knowledge Chatbot", status = "ok" }));

app.MapChatEndpoints();
app.MapAgentEndpoints();
app.MapIngestionEndpoints();
app.MapKnowledgeEndpoints();

app.Run();
