using System.Collections.Concurrent;

namespace Document.Api.Data;

// Tam thoi luu trong bo nho. TODO: thay bang EF Core + SQL Server (AppDbContext).
public class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Models.Document> _store = new();

    public Task<IReadOnlyList<Models.Document>> GetAllAsync(string? status = null)
    {
        IEnumerable<Models.Document> query = _store.Values;
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<Models.Document>>(query.OrderByDescending(d => d.SubmittedAt).ToList());
    }

    public Task<Models.Document?> GetByIdAsync(Guid id) =>
        Task.FromResult(_store.TryGetValue(id, out var doc) ? doc : null);

    public Task AddAsync(Models.Document document)
    {
        _store[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Models.Document document)
    {
        _store[document.Id] = document;
        return Task.CompletedTask;
    }
}
