namespace Shared.Contracts.Events;

// Su kien phat len RabbitMQ (publish-subscribe) khi tai lieu duoc gui, phe duyet hoac tu choi.
// Tao tai lieu (draft) KHONG phat su kien.

// Gui tai lieu -> thong bao cho nguoi phe duyet
public record DocumentSubmittedEvent(
    Guid DocumentId, string Title, string CreatedBy, string ApproverCode, DateTimeOffset OccurredAt);

// Phe duyet -> thong bao cho nguoi tao
public record DocumentApprovedEvent(
    Guid DocumentId, string Title, string CreatedBy, string ApproverCode, string? Comment, DateTimeOffset OccurredAt);

// Tu choi -> thong bao cho nguoi tao kem ly do
public record DocumentRejectedEvent(
    Guid DocumentId, string Title, string CreatedBy, string ApproverCode, string Reason, DateTimeOffset OccurredAt);
