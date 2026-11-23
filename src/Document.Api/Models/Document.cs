namespace Document.Api.Models;

// Thuc the noi bo (entity) - khong tra truc tiep ra ngoai, chi anh xa sang DTO
public class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string CreatedBy { get; set; } = "";       // nguoi tao
    public string ApproverCode { get; set; } = "";    // nguoi phe duyet
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }

    public List<DocumentHistory> History { get; set; } = [];  // lich su xu ly

    // Ghi nhan mot lan chuyen trang thai vao lich su
    public void ChangeStatus(string action, DocumentStatus newStatus, string performedBy, string? comment, DateTimeOffset at)
    {
        History.Add(new DocumentHistory
        {
            Id = Guid.NewGuid(),
            DocumentId = Id,
            Action = action,
            FromStatus = History.Count == 0 ? null : Status,
            ToStatus = newStatus,
            PerformedBy = performedBy,
            Comment = comment,
            At = at
        });
        Status = newStatus;
    }
}

public class DocumentHistory
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Action { get; set; } = "";
    public DocumentStatus? FromStatus { get; set; }
    public DocumentStatus ToStatus { get; set; }
    public string PerformedBy { get; set; } = "";
    public string? Comment { get; set; }
    public DateTimeOffset At { get; set; }
}

// Draft --submit--> Pending --approve--> Approved
//                           --reject---> Rejected
public enum DocumentStatus
{
    Draft,
    Pending,
    Approved,
    Rejected
}
