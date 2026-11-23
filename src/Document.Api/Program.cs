using Document.Api.Data;
using Document.Api.Endpoints;
using Document.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Pha 1: dang ky dich vu
builder.Services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

var app = builder.Build();

// Pha 2: anh xa endpoint
app.MapGet("/", () => Results.Ok(new { service = "Document.Api", status = "running" }));
app.MapDocumentEndpoints();

app.Run();
