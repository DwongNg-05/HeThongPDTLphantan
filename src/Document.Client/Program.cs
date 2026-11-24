using Document.Client.Services;
using Shared.Contracts.Dtos;

var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
var notificationBaseUrl = args.Length > 1 ? args[1] : "http://localhost:5001";
var notifyTimeout = TimeSpan.FromSeconds(5);

using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
var api = new DocumentApiClient(http);

// 0. Nguoi tao (NV001) va nguoi phe duyet (QL001) ket noi SignalR de nhan thong bao
await using var creator = new NotificationListener(notificationBaseUrl, "NV001");
await using var approver = new NotificationListener(notificationBaseUrl, "QL001");
var realtime = await creator.TryStartAsync() & await approver.TryStartAsync();

// 1. Tao tai lieu / ho so can phe duyet (draft)
var (created, error) = await api.CreateAsync(new CreateDocumentRequest(
    Title: "De xuat mua thiet bi phong lab",
    Content: "Mua 10 may tinh cho phong thuc hanh.",
    CreatedBy: "NV001",
    ApproverCode: "QL001"));
if (error is not null)
{
    Console.WriteLine($"{error.Code}: {error.Message}");
    return;
}
Console.WriteLine($"Da tao tai lieu: {created!.Id} - trang thai: {created.Status}");

// 2. Gui tai lieu vao quy trinh phe duyet: draft -> pending, QL001 nhan thong bao
var submittedNotice = approver.ExpectAsync(created.Id, "submitted");
var (submitted, submitError) = await api.SubmitAsync(created.Id, new SubmitDocumentRequest("NV001"));
if (submitError is not null)
{
    Console.WriteLine($"Gui that bai -> {submitError.Code}: {submitError.Message}");
    return;
}
Console.WriteLine($"Da gui, trang thai: {submitted!.Status}");
await WaitForNotificationAsync(submittedNotice, "QL001");

// 2b. Cac truong hop gui sai
var (_, notOwner) = await api.SubmitAsync(created.Id, new SubmitDocumentRequest("NV999"));
Console.WriteLine($"Nguoi khac gui -> {notOwner?.Code}: {notOwner?.Message}");
var (_, again) = await api.SubmitAsync(created.Id, new SubmitDocumentRequest("NV001"));
Console.WriteLine($"Gui lai lan 2  -> {again?.Code}: {again?.Message}");

// 3. Nguoi phe duyet duyet tai lieu, NV001 nhan thong bao
var approvedNotice = creator.ExpectAsync(created.Id, "approved");
var (approved, approveError) = await api.ApproveAsync(created.Id, new ReviewDocumentRequest("QL001", "Dong y"));
Console.WriteLine(approveError is null
    ? $"Trang thai moi: {approved!.Status}"
    : $"{approveError.Code}: {approveError.Message}");
if (approveError is null) await WaitForNotificationAsync(approvedNotice, "NV001");

// 4. Doc lai lich su xu ly
var (detail, _) = await api.GetAsync(created.Id);
Console.WriteLine("\nLich su xu ly:");
foreach (var h in detail?.History ?? [])
    Console.WriteLine($"  {h.At:HH:mm:ss} {h.Action,-10} {h.FromStatus ?? "-"} -> {h.ToStatus} boi {h.PerformedBy} {h.Comment}");

// 5. Gui du lieu sai de quan sat 400
var (_, invalid) = await api.CreateAsync(new CreateDocumentRequest("", null, "NV001", "QL001"));
Console.WriteLine($"\nThu tao tai lieu sai -> {invalid?.Code}: {invalid?.Message}");

async Task WaitForNotificationAsync(Task<DocumentNotification> notice, string who)
{
    if (!realtime) return;
    if (await Task.WhenAny(notice, Task.Delay(notifyTimeout)) != notice)
        Console.WriteLine($"  (khong nhan duoc thong bao cho {who} sau {notifyTimeout.TotalSeconds}s)");
}
