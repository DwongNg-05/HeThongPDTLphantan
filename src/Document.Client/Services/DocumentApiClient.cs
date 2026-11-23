using System.Net.Http.Json;
using Shared.Contracts.Dtos;

namespace Document.Client.Services;

// Goi Document.Api qua HTTP: kiem tra status code truoc khi doc body
public class DocumentApiClient(HttpClient http)
{
    public async Task<List<DocumentSummaryDto>> GetDocumentsAsync(string? status = null)
    {
        var url = string.IsNullOrWhiteSpace(status) ? "/documents" : $"/documents?status={status}";
        return await http.GetFromJsonAsync<List<DocumentSummaryDto>>(url) ?? [];
    }

    public async Task<(DocumentDto? Document, ApiError? Error)> GetAsync(Guid id) =>
        await ReadAsync(await http.GetAsync($"/documents/{id}"));

    public async Task<(DocumentDto? Document, ApiError? Error)> CreateAsync(CreateDocumentRequest request) =>
        await ReadAsync(await http.PostAsJsonAsync("/documents", request));

    public async Task<(DocumentDto? Document, ApiError? Error)> SubmitAsync(Guid id, SubmitDocumentRequest request) =>
        await ReadAsync(await http.PostAsJsonAsync($"/documents/{id}/submit", request));

    public async Task<(DocumentDto? Document, ApiError? Error)> ApproveAsync(Guid id, ReviewDocumentRequest request) =>
        await ReadAsync(await http.PostAsJsonAsync($"/documents/{id}/approve", request));

    public async Task<(DocumentDto? Document, ApiError? Error)> RejectAsync(Guid id, ReviewDocumentRequest request) =>
        await ReadAsync(await http.PostAsJsonAsync($"/documents/{id}/reject", request));

    private static async Task<(DocumentDto?, ApiError?)> ReadAsync(HttpResponseMessage response) =>
        response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<DocumentDto>(), null)
            : (null, await response.Content.ReadFromJsonAsync<ApiError>());
}
