using Knowledge.Infrastructure;
using Knowledge.Infrastructure.Persistence;
using Knowledge.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKnowledgeServices(builder.Configuration);
builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();

if (builder.Configuration.GetValue("Knowledge:EnsureDatabaseCreated", true))
{
    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

host.Run();
