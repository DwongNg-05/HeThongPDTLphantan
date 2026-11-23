using Document.Api.Models;

namespace Document.Api.Data;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Models.Document>> GetAllAsync(string? status = null);
    Task<Models.Document?> GetByIdAsync(Guid id);
    Task AddAsync(Models.Document document);
    Task UpdateAsync(Models.Document document);
}
