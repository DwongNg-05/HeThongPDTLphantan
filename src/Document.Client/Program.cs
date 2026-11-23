using Document.Client.Services;
using Shared.Contracts.Dtos;

var apiBaseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
var api = new DocumentApiClient(http);

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

// 2. Gui tai lieu di phe duyet
var (submitted, submitError) = await api.SubmitAsync(created.Id, new SubmitDocumentRequest("NV001"));
Console.WriteLine(submitError is null
    ? $"Da gui, trang thai: {submitted!.Status}"
    : $"{submitError.Code}: {submitError.Message}");

// 3. Nguoi phe duyet duyet tai lieu
var (approved, approveError) = await api.ApproveAsync(created.Id, new ReviewDocumentRequest("QL001", "Dong y"));
Console.WriteLine(approveError is null
    ? $"Trang thai moi: {approved!.Status}"
    : $"{approveError.Code}: {approveError.Message}");

// 4. Doc lai lich su xu ly
var (detail, _) = await api.GetAsync(created.Id);
Console.WriteLine("\nLich su xu ly:");
foreach (var h in detail?.History ?? [])
    Console.WriteLine($"  {h.At:HH:mm:ss} {h.Action,-10} {h.FromStatus ?? "-"} -> {h.ToStatus} boi {h.PerformedBy} {h.Comment}");

// 5. Gui du lieu sai de quan sat 400
var (_, invalid) = await api.CreateAsync(new CreateDocumentRequest("", null, "NV001", "QL001"));
Console.WriteLine($"\nThu tao tai lieu sai -> {invalid?.Code}: {invalid?.Message}");

// TODO: ket noi SignalR (goi Microsoft.AspNetCore.SignalR.Client) toi
// http://localhost:5001/hubs/notifications de nhan thong bao thoi gian thuc.
