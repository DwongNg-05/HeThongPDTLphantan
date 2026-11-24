using System.Collections.Concurrent;
using Document.Api.Data;
using Document.Api.Messaging;
using Document.Api.Models;
using Shared.Contracts.Dtos;
using Shared.Contracts.Events;
using Shared.Contracts.Messaging;

namespace Document.Api.Endpoints;

public static class DocumentEndpoints
{
    private const int MaxTitleLength = 200;

    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/documents");

        // POST /documents -> tao tai lieu / ho so can phe duyet (trang thai draft)
        group.MapPost("/", CreateAsync);

        // GET /documents?status=pending&createdBy=NV001&approverCode=QL001
        group.MapGet("/", async (string? status, string? createdBy, string? approverCode, IDocumentRepository repo) =>
        {
            var docs = await repo.GetAllAsync(status);
            var result = docs
                .Where(d => createdBy is null || d.CreatedBy.Equals(createdBy, StringComparison.OrdinalIgnoreCase))
                .Where(d => approverCode is null || d.ApproverCode.Equals(approverCode, StringComparison.OrdinalIgnoreCase))
                .Select(ToSummaryDto);
            return Results.Ok(result);
        });

        // GET /documents/{id}
        group.MapGet("/{id:guid}", async (Guid id, IDocumentRepository repo) =>
            await repo.GetByIdAsync(id) is { } doc
                ? Results.Ok(ToDto(doc))
                : NotFound());

        // POST /documents/{id}/submit
        group.MapPost("/{id:guid}/submit", SubmitAsync);

        // POST /documents/{id}/approve
        group.MapPost("/{id:guid}/approve",
            (Guid id, ReviewDocumentRequest request, IDocumentRepository repo, IEventPublisher publisher) =>
                ReviewAsync(id, request, approve: true, repo, publisher));

        // POST /documents/{id}/reject
        group.MapPost("/{id:guid}/reject",
            (Guid id, ReviewDocumentRequest request, IDocumentRepository repo, IEventPublisher publisher) =>
                ReviewAsync(id, request, approve: false, repo, publisher));
    }

    // ===== Tao tai lieu =====
    private static async Task<IResult> CreateAsync(CreateDocumentRequest request, IDocumentRepository repo)
    {
        // Validation tai bien he thong
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("INVALID_DOCUMENT", "Tieu de tai lieu khong duoc de trong.");
        if (request.Title.Trim().Length > MaxTitleLength)
            return BadRequest("INVALID_DOCUMENT", $"Tieu de khong vuot qua {MaxTitleLength} ky tu.");
        if (string.IsNullOrWhiteSpace(request.CreatedBy))
            return BadRequest("INVALID_DOCUMENT", "Thieu ma nguoi tao.");
        if (string.IsNullOrWhiteSpace(request.ApproverCode))
            return BadRequest("INVALID_DOCUMENT", "Thieu ma nguoi phe duyet.");
        if (request.CreatedBy.Trim().Equals(request.ApproverCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return BadRequest("INVALID_APPROVER", "Nguoi phe duyet phai khac nguoi tao.");

        var now = DateTimeOffset.UtcNow;
        var doc = new Models.Document
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content?.Trim() ?? "",
            CreatedBy = request.CreatedBy.Trim(),
            ApproverCode = request.ApproverCode.Trim(),
            CreatedAt = now
        };
        doc.ChangeStatus("created", DocumentStatus.Draft, doc.CreatedBy, null, now);

        await repo.AddAsync(doc);

        // Tao draft khong phat su kien: chi khi submit/approve/reject moi phat
        return Results.Created($"/documents/{doc.Id}", ToDto(doc));
    }

    // ===== Gui tai lieu =====
    // draft -> pending, phat su kien document.submitted de Notification.Service bao cho nguoi phe duyet.
    private static async Task<IResult> SubmitAsync(Guid id, SubmitDocumentRequest? request,
        IDocumentRepository repo, IEventPublisher publisher, ILoggerFactory loggerFactory)
    {
        if (string.IsNullOrWhiteSpace(request?.PerformedBy))
            return BadRequest("INVALID_SUBMIT", "Thieu ma nguoi gui.");

        var doc = await repo.GetByIdAsync(id);
        if (doc is null) return NotFound();

        if (!doc.CreatedBy.Equals(request.PerformedBy.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbidden("NOT_DOCUMENT_OWNER", "Chi nguoi tao moi duoc gui tai lieu.");

        // Khoa theo tai lieu: tranh 2 request submit dong thoi cung vuot qua kiem tra trang thai
        var gate = SubmitLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (doc.Status != DocumentStatus.Draft)
                return Conflict("INVALID_STATE", $"Chi gui duoc tai lieu o trang thai draft (hien tai: {Status(doc.Status)}).");
            if (string.IsNullOrWhiteSpace(doc.ApproverCode))
                return BadRequest("INVALID_APPROVER", "Tai lieu chua co nguoi phe duyet.");

            var now = DateTimeOffset.UtcNow;
            doc.SubmittedAt = now;
            doc.ChangeStatus("submitted", DocumentStatus.Pending, doc.CreatedBy, null, now);
            await repo.UpdateAsync(doc);

            try
            {
                await publisher.PublishAsync(MessagingConstants.RoutingKeySubmitted,
                    new DocumentSubmittedEvent(doc.Id, doc.Title, doc.CreatedBy, doc.ApproverCode, now));
            }
            catch (EventPublishException ex)
            {
                // Khong bao duoc cho nguoi phe duyet -> hoan tac ve draft de nguoi tao gui lai sau
                loggerFactory.CreateLogger("Document.Submit")
                    .LogError(ex, "Gui tai lieu {DocumentId} that bai, hoan tac ve draft", doc.Id);
                doc.RevertLastChange();
                doc.SubmittedAt = null;
                await repo.UpdateAsync(doc);
                return Results.Json(
                    new ApiError("MESSAGING_UNAVAILABLE", "He thong thong bao tam thoi khong kha dung, vui long gui lai sau."),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(ToDto(doc));
        }
        finally
        {
            gate.Release();
        }
    }

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SubmitLocks = new();

    // ===== Phe duyet / tu choi =====
    private static async Task<IResult> ReviewAsync(Guid id, ReviewDocumentRequest request, bool approve,
        IDocumentRepository repo, IEventPublisher publisher)
    {
        if (string.IsNullOrWhiteSpace(request.ReviewerCode))
            return BadRequest("INVALID_REVIEW", "Thieu ma nguoi phe duyet.");
        if (!approve && string.IsNullOrWhiteSpace(request.Comment))
            return BadRequest("INVALID_REVIEW", "Tu choi phai kem ly do.");

        var doc = await repo.GetByIdAsync(id);
        if (doc is null) return NotFound();

        if (!doc.ApproverCode.Equals(request.ReviewerCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return Forbidden("NOT_ASSIGNED_APPROVER", "Ban khong phai nguoi phe duyet cua tai lieu nay.");
        if (doc.Status != DocumentStatus.Pending)
            return Conflict("INVALID_STATE", $"Chi xu ly duoc tai lieu dang cho duyet (hien tai: {Status(doc.Status)}).");

        var now = DateTimeOffset.UtcNow;
        var comment = request.Comment?.Trim();
        doc.ReviewedAt = now;
        doc.ReviewComment = comment;
        doc.ChangeStatus(approve ? "approved" : "rejected",
            approve ? DocumentStatus.Approved : DocumentStatus.Rejected,
            doc.ApproverCode, comment, now);
        await repo.UpdateAsync(doc);

        if (approve)
            await publisher.PublishAsync(MessagingConstants.RoutingKeyApproved,
                new DocumentApprovedEvent(doc.Id, doc.Title, doc.CreatedBy, doc.ApproverCode, comment, now));
        else
            await publisher.PublishAsync(MessagingConstants.RoutingKeyRejected,
                new DocumentRejectedEvent(doc.Id, doc.Title, doc.CreatedBy, doc.ApproverCode, comment!, now));

        return Results.Ok(ToDto(doc));
    }

    // ===== Helpers =====
    private static string Status(DocumentStatus s) => s.ToString().ToLowerInvariant();

    private static IResult NotFound() =>
        Results.NotFound(new ApiError("DOCUMENT_NOT_FOUND", "Khong tim thay tai lieu."));
    private static IResult BadRequest(string code, string message) =>
        Results.BadRequest(new ApiError(code, message));
    private static IResult Conflict(string code, string message) =>
        Results.Conflict(new ApiError(code, message));
    private static IResult Forbidden(string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: StatusCodes.Status403Forbidden);

    private static DocumentSummaryDto ToSummaryDto(Models.Document d) =>
        new(d.Id, d.Title, d.CreatedBy, d.ApproverCode, Status(d.Status), d.CreatedAt);

    private static DocumentDto ToDto(Models.Document d) => new(
        d.Id, d.Title, d.Content, d.CreatedBy, d.ApproverCode, Status(d.Status),
        d.CreatedAt, d.SubmittedAt, d.ReviewedAt, d.ReviewComment,
        d.History.OrderBy(h => h.At)
            .Select(h => new DocumentHistoryDto(h.Action,
                h.FromStatus is null ? null : Status(h.FromStatus.Value),
                Status(h.ToStatus), h.PerformedBy, h.Comment, h.At))
            .ToList());
}
