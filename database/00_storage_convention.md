# Quy ước lưu trữ file trên S3 — GradePaper

## Nguyên tắc
- **DB không lưu nội dung file.** DB chỉ lưu **con trỏ tới object trên S3** (`s3_key`) + vài metadata tối thiểu
  (`file_name`, `content_type`, `size_bytes`).
- **Hiển thị (xem file):** backend tạo **pre-signed GET URL** từ preview key (`inline`), frontend xem trực tiếp:
  - `application/pdf` → iframe (preview; DOCX được convert sang PDF lúc upload qua Gotenberg)
  - `image/png`, `image/jpeg`, `image/webp` → thẻ `<img>` (zoom)
  - `text/plain` → render text
- **Bản gốc DOCX:** lưu riêng trên S3; chỉ tải về qua endpoint `/original` (attachment), không dùng làm luồng xem chính.
- **Preview path:** `exam-papers/{subjectId}/preview.pdf`, `rubrics/{subjectId}/v{n}/preview.pdf` (khi upload DOCX).
- **Thông tin cho AI** (nội dung barem, đề, đoạn trích bài làm) được **embed sẵn vào Qdrant** để AI truy hồi nhanh,
  không cần đọc lại file từ S3 mỗi lần chấm.

## Một bucket, phân theo prefix (per-environment: `gradepaper-files-{env}`)

```
exam-papers/{subjectId}/{fileName}            # đề thi  (1 file: pdf/docx/image)
rubrics/{subjectId}/v{rubricVersion}/{fileName}  # barem (1 file: pdf/docx/image)
submissions/{subjectId}/{batchId}.zip         # file ZIP gốc do trường gửi
submissions/{subjectId}/{paperAlias}/{fileName}  # các file bài làm của 1 SV (pdf/img/txt/docx)
exports/{subjectId}/{jobId}.xlsx              # file điểm xuất ra (Mark_Input)
```

## Cột chuẩn để tham chiếu file (dùng lại ở mọi bảng có file)
| Cột | Kiểu | Ý nghĩa |
|-----|------|---------|
| `*_s3_key`        | TEXT         | Object key trong bucket (không lưu URL ký sẵn) |
| `*_file_name`     | VARCHAR(500) | Tên gốc để hiển thị / tải về |
| `*_content_type`  | VARCHAR(120) | MIME type → frontend chọn cách render |
| `*_size_bytes`    | BIGINT       | (tuỳ chọn) kích thước file |

## Bảo mật
- Bucket **private**; chỉ truy cập qua pre-signed URL hết hạn ngắn (vd 5–15 phút).
- Service truy cập S3 bằng **IRSA** (IAM Role for Service Account) trên EKS — không hard-code key.
- Mã hoá at-rest bằng **SSE-KMS**; bật **versioning** cho bucket.
```
```
