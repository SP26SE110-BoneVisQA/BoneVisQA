# Student API Notes

## 1. Chạy API & đăng nhập Student

- Chạy backend: `dotnet run --project BoneVisQA.API`
- Mở Swagger: `http://localhost:5046/swagger` (hoặc port trong console)

### Đăng ký Student

- `POST /api/Auths/register`
  ```json
  {
    "fullName": "Nguyen Van A",
    "email": "student@example.com",
    "password": "P@ssw0rd!",
    "schoolCohort": "Y4 - Orthopedics 2026",
    "roleName": "Student"
  }
  ```

### Đăng nhập Student

- `POST /api/Auths/login`
  ```json
  {
    "email": "student@example.com",
    "password": "P@ssw0rd!"
  }
  ```
- Lấy `token` trong response, dùng cho các request sau:
  - Header: `Authorization: Bearer <token>`

---

## 2. Chức năng chính cho Student (StudentsController)

### 2.1. Xem danh sách ca bệnh

- `GET /api/Students/cases?studentId={GUID-STUDENT-ID}`
- Kết quả: danh sách `CaseListItemDto`:
  ```json
  [
    {
      "id": "GUID-CASE-ID",
      "title": "Distal radius fracture",
      "description": "Short description...",
      "difficulty": "basic",
      "categoryName": "Long bones - Wrist",
      "thumbnailImageUrl": "https://..."
    }
  ]
  ```

### 2.2. Lọc ca bệnh (Filter)

- `GET /api/Students/cases/filter?studentId={GUID-STUDENT-ID}`
- Query params:
  - `difficulty`: "basic" | "intermediate" | "advanced" (optional)
  - `categoryId`: GUID category (optional)
  - `searchTerm`: từ khóa tìm kiếm (optional)
- Kết quả: danh sách `CaseListItemDto` (tương tự 2.1)

### 2.3. Xem chi tiết ca bệnh (và ghi log xem ca)

- `GET /api/Students/cases/{caseId}?studentId={GUID-STUDENT-ID}`
- Kết quả: `CaseDetailDto`:
  ```json
  {
    "id": "GUID-CASE-ID",
    "title": "Distal radius fracture",
    "description": "Full case description...",
    "difficulty": "basic",
    "categoryName": "Long bones - Wrist",
    "images": [
      {
        "id": "GUID-IMAGE-ID",
        "imageUrl": "https://...",
        "modality": "X-ray"
      }
    ]
  }
  ```

### 2.4. Tạo annotation trên ảnh

- `POST /api/Students/annotations?studentId={GUID-STUDENT-ID}`
- Body:
  ```json
  {
    "imageId": "GUID-IMAGE-ID",
    "label": "Suspicious fracture line",
    "coordinates": "{\"x\":100,\"y\":150,\"width\":80,\"height\":60}"
  }
  ```
- **Lưu ý**: `coordinates` phải là JSON hợp lệ (định dạng `{"x":..., "y":..., "width":..., "height":...}`).
- Kết quả: `AnnotationDto` với `id`, `imageId`, `label`, `coordinates`, `createdAt`.

### 2.5. Gửi câu hỏi (Visual Q&A – lưu lịch sử)

- `POST /api/Students/questions?studentId={GUID-STUDENT-ID}`
- Body:
  ```json
  {
    "caseId": "GUID-CASE-ID",
    "annotationId": "GUID-ANNOTATION-ID",
    "questionText": "What type of fracture does this lesion suggest?",
    "language": "en"
  }
  ```
- **Lưu ý**:
  - `language`: chỉ chấp nhận "vi" hoặc "en" (hệ thống tự chuyển đổi VIE → vi, EN → en).
  - `caseId` có thể để trống (null) nếu câu hỏi không gắn với ca bệnh cụ thể.
- Kết quả: `StudentQuestionDto`.

### 2.6. Xem lịch sử câu hỏi

- `GET /api/Students/questions?studentId={GUID-STUDENT-ID}`
- Kết quả: danh sách `StudentQuestionHistoryItemDto`:
  ```json
  [
    {
      "id": "GUID-QUESTION-ID",
      "caseId": "GUID-CASE-ID",
      "questionText": "What type of fracture does this lesion suggest?",
      "createdAt": "2026-03-07T10:00:00Z"
    }
  ]
  ```

### 2.7. Xem thông báo của lớp

- `GET /api/Students/announcements?studentId={GUID-STUDENT-ID}`
- Kết quả: danh sách `StudentAnnouncementDto`:
  ```json
  [
    {
      "id": "GUID-ANNOUNCEMENT-ID",
      "classId": "GUID-CLASS-ID",
      "className": "Orthopedics - Class A 2026",
      "title": "New bone fracture cases",
      "content": "Please review...",
      "createdAt": "2026-03-07T09:00:00Z"
    }
  ]
  ```

### 2.8. Xem danh sách quiz được giao

- `GET /api/Students/quizzes?studentId={GUID-STUDENT-ID}`
- Kết quả: danh sách `QuizListItemDto`:
  ```json
  [
    {
      "quizId": "GUID-QUIZ-ID",
      "title": "Long bone fractures quiz",
      "openTime": "2026-03-01T00:00:00Z",
      "closeTime": "2026-03-10T23:59:59Z",
      "timeLimit": 900,
      "passingScore": 60,
      "isCompleted": false,
      "score": null
    }
  ]
  ```

### 2.9. Bắt đầu làm quiz

- `POST /api/Students/quizzes/{quizId}/start?studentId={GUID-STUDENT-ID}`
- Kết quả: `QuizSessionDto`:
  ```json
  {
    "attemptId": "GUID-ATTEMPT-ID",
    "quizId": "GUID-QUIZ-ID",
    "title": "Long bone fractures quiz",
    "questions": [
      {
        "questionId": "GUID-QUESTION-1",
        "questionText": "What is the most likely diagnosis?",
        "type": "MCQ",
        "caseId": "GUID-CASE-ID"
      }
    ]
  }
  ```

### 2.10. Nộp bài quiz

- `POST /api/Students/quizzes/submit?studentId={GUID-STUDENT-ID}`
- **Lưu ý**: `studentId` có thể bỏ trống nếu đã đăng nhập (hệ thống tự lấy từ JWT).
- Body:
  ```json
  {
    "attemptId": "GUID-ATTEMPT-ID",
    "answers": [
      {
        "questionId": "GUID-QUESTION-1",
        "studentAnswer": "A"
      }
    ]
  }
  ```
- Kết quả: `QuizResultDto`:
  ```json
  {
    "attemptId": "GUID-ATTEMPT-ID",
    "quizId": "GUID-QUIZ-ID",
    "score": 100.0,
    "passingScore": 60,
    "passed": true
  }
  ```

### 2.11. Xem tổng quan tiến độ học tập

- `GET /api/Students/progress?studentId={GUID-STUDENT-ID}`
- Kết quả: `StudentProgressDto`:
  ```json
  {
    "totalCasesViewed": 15,
    "totalQuestionsAsked": 7,
    "avgQuizScore": 82.5
  }
  ```

---

## 3. Flow mẫu cho Student

1. **Đăng ký & Đăng nhập** → lấy JWT.
2. **Xem ca bệnh**:
   - Xem danh sách (`/api/Students/cases`).
   - Lọc ca (`/api/Students/cases/filter`).
   - Mở chi tiết ca (`/api/Students/cases/{caseId}`) → tự động ghi log.
3. **Tương tác**:
   - Tạo annotation (`/api/Students/annotations`).
   - Gửi câu hỏi (`/api/Students/questions`).
   - Xem lịch sử câu hỏi (`/api/Students/questions`).
4. **Làm quiz**:
   - Xem quiz được giao (`/api/Students/quizzes`).
   - Bắt đầu quiz (`/api/Students/quizzes/{quizId}/start`).
   - Nộp bài (`/api/Students/quizzes/submit`).
5. **Theo dõi**:
   - Xem thông báo (`/api/Students/announcements`).
   - Xem tiến độ (`/api/Students/progress`).

---

## 4. Các lỗi thường gặp & cách xử lý

| Lỗi | Nguyên nhân | Cách xử lý |
|-----|-------------|-------------|
| `student_questions_language_check` | `language` không phải "vi" hoặc "en" | Dùng đúng giá trị "vi" hoặc "en" |
| `invalid input syntax for type json` | `coordinates` không phải JSON hợp lệ | Dùng format `{"x":100,"y":150,"width":80,"height":60}` |
| `student_questions_student_id_fkey` | `studentId` không tồn tại trong bảng users | Truyền đúng `studentId` (id từ bảng users) |
| `Lần làm quiz không tồn tại` | `attemptId` không đúng hoặc không thuộc student đó | Dùng `attemptId` từ API start quiz, `studentId` phải trùng |
| `CaseId` null error | DB chấp nhận null nhưng code không | Dùng `Guid.Empty` hoặc bỏ trống nếu không có case |

