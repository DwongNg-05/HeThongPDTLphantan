namespace Shared.Contracts.Dtos;

// ===== Request DTO =====

// POST /documents - tao tai lieu (ho so) o trang thai draft
public record CreateDocumentRequest(
    string Title,
    string? Content,
    string CreatedBy,      // ma nguoi tao
    string ApproverCode);  // ma nguoi phe duyet duoc chi dinh

// POST /documents/{id}/submit
public record SubmitDocumentRequest(string PerformedBy);

// POST /documents/{id}/approve | /reject
public record ReviewDocumentRequest(string ReviewerCode, string? Comment);

// ===== Response DTO =====

// Dung cho danh sach (GET /documents)
public record DocumentSummaryDto(
    Guid Id,
    string Title,
    string CreatedBy,
    string ApproverCode,
    string Status,
    DateTimeOffset CreatedAt);

// Dung cho chi tiet (GET /documents/{id}) va ket qua cac thao tac
public record DocumentDto(
    Guid Id,
    string Title,
    string Content,
    string CreatedBy,
    string ApproverCode,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    IReadOnlyList<DocumentHistoryDto> History);

// Mot buoc trong lich su xu ly
public record DocumentHistoryDto(
    string Action,        // created | submitted | approved | rejected
    string? FromStatus,
    string ToStatus,
    string PerformedBy,
    string? Comment,
    DateTimeOffset At);

// ===== Error DTO =====
public record ApiError(string Code, string Message);
