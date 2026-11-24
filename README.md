# Đề tài 12 – Hệ thống phê duyệt tài liệu phân tán

Luồng tối thiểu: `Client -> Document API -> Database -> RabbitMQ -> Notification Service -> Client`

```
hethongpheduyettl/
├── DocumentApproval.slnx
├── docker-compose.yml            # RabbitMQ + SQL Server
├── requests/document-api.http    # Thử API bằng file .http
└── src/
    ├── Shared.Contracts/         # Hợp đồng dùng chung
    │   ├── Dtos/                 # Request/Response DTO, ApiError
    │   ├── Events/               # DocumentSubmitted/Approved/Rejected
    │   └── Messaging/            # Tên exchange, queue, routing key
    ├── Document.Api/             # REST API (cổng 5000)
    │   ├── Endpoints/            # Ánh xạ /api/documents
    │   ├── Models/               # Entity nội bộ
    │   ├── Data/                 # Repository (in-memory -> EF Core)
    │   └── Messaging/            # Publish sự kiện lên RabbitMQ
    ├── Notification.Service/     # Consumer RabbitMQ + SignalR (cổng 5001)
    │   ├── Consumers/            # BackgroundService nhận sự kiện
    │   └── Hubs/                 # NotificationHub -> đẩy tới client
    └── Document.Client/          # Console client
        └── Services/             # DocumentApiClient (HttpClient)
```

## Endpoint

| Method | URI | Vai trò | Sự kiện |
|---|---|---|---|
| POST | `/documents` | Tạo tài liệu/hồ sơ (draft) → 201 | – |
| GET | `/documents?status=&createdBy=&approverCode=` | Danh sách tài liệu | – |
| GET | `/documents/{id}` | Chi tiết + lịch sử xử lý | – |
| POST | `/documents/{id}/submit` | draft → pending | `document.submitted` |
| POST | `/documents/{id}/approve` | pending → approved | `document.approved` |
| POST | `/documents/{id}/reject` | pending → rejected (bắt buộc lý do) | `document.rejected` |

Mã lỗi: `400` dữ liệu sai · `403` không phải người tạo/người duyệt · `404` không có tài liệu · `409` sai trạng thái · `503` không phát được sự kiện lên RabbitMQ (khi submit: tài liệu được hoàn tác về draft để gửi lại).

## Luồng gửi tài liệu vào quy trình phê duyệt

1. Người tạo gọi `POST /documents/{id}/submit` với `{ "performedBy": "<mã người tạo>" }`.
2. Document.Api kiểm tra (người gửi = người tạo, trạng thái = draft), chuyển sang `pending`, ghi lịch sử `submitted`.
3. Document.Api publish `DocumentSubmittedEvent` lên exchange `document.events` (topic) với routing key `document.submitted` (persistent, có publisher confirm).
4. Notification.Service consume queue `notification.document-events` (bind `document.*`) và đẩy `DocumentNotification` qua SignalR tới group `user:{approverCode}`.
5. Client kết nối `http://localhost:5001/hubs/notifications?userCode=QL001` và nhận method `DocumentNotification`.

Phê duyệt/từ chối dùng cùng cơ chế, thông báo gửi tới người tạo.

Dữ liệu: tài liệu, người tạo (`createdBy`), người phê duyệt (`approverCode`), trạng thái, lịch sử xử lý (`history`).

## Chạy

```
docker compose up -d
dotnet run --project src/Document.Api
dotnet run --project src/Notification.Service
dotnet run --project src/Document.Client -- http://localhost:5000 http://localhost:5001
```

## Việc còn lại (TODO)
- `Document.Api/Data`: thay `InMemoryDocumentRepository` bằng EF Core + SQL Server.
- Outbox pattern để đảm bảo DB và sự kiện nhất quán khi chuyển sang EF Core.
