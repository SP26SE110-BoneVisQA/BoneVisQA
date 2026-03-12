## Student API Notes

### 1. Chạy API & đăng nhập Student

- Chạy backend: `dotnet run --project BoneVisQA.API`
- Mở Swagger: `https://localhost:5001/swagger`.

#### Đăng ký Student

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

#### Đăng nhập Student

- `POST /api/Auths/login`
  ```json
  {
    "email": "student@example.com",
    "password": "P@ssw0rd!"
  }
  ```
- Lấy `token` trong response, dùng cho các request sau:
  - Header: `Authorization: Bearer <token>`

> Lưu ý: hiện tại controller đang nhận `studentId` qua query. Khi tích hợp bảo mật đầy đủ, có thể lấy `studentId` trực tiếp từ JWT.

---

### 2. Chức năng chính cho Student (StudentController)

#### 2.1. Xem danh sách ca bệnh

- `GET /api/Student/cases?studentId={GUID-STUDENT-ID}`
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

#### 2.2. Xem chi tiết ca bệnh (và ghi log xem ca)

- `GET /api/Student/cases/{caseId}?studentId={GUID-STUDENT-ID}`
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

#### 2.3. Tạo annotation trên ảnh

- `POST /api/Student/annotations?studentId={GUID-STUDENT-ID}`
- Body:
  ```json
  {
    "imageId": "GUID-IMAGE-ID",
    "label": "Suspicious fracture line",
    "coordinates": "{\"x\":100,\"y\":150,\"width\":80,\"height\":60}"
  }
  ```
- Kết quả: `AnnotationDto` với `id`, `imageId`, `label`, `coordinates`, `createdAt`.

#### 2.4. Gửi câu hỏi (Visual Q&A – lưu lịch sử)

- `POST /api/Student/questions?studentId={GUID-STUDENT-ID}`
- Body:
  ```json
  {
    "caseId": "GUID-CASE-ID",
    "annotationId": "GUID-ANNOTATION-ID",
    "questionText": "What type of fracture does this lesion suggest?",
    "language": "en"
  }
  ```
- Kết quả: `StudentQuestionDto`.

#### 2.5. Xem lịch sử câu hỏi

- `GET /api/Student/questions?studentId={GUID-STUDENT-ID}`
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

#### 2.6. Xem danh sách quiz được giao

- `GET /api/Student/quizzes?studentId={GUID-STUDENT-ID}`
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

#### 2.7. Bắt đầu làm quiz

- `POST /api/Student/quizzes/{quizId}/start?studentId={GUID-STUDENT-ID}`
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

#### 2.8. Nộp bài quiz

- `POST /api/Student/quizzes/submit?studentId={GUID-STUDENT-ID}`
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

#### 2.9. Xem tổng quan tiến độ học tập

- `GET /api/Student/progress?studentId={GUID-STUDENT-ID}`
- Kết quả: `StudentProgressDto`:
  ```json
  {
    "totalCasesViewed": 15,
    "totalQuestionsAsked": 7,
    "avgQuizScore": 82.5
  }
  ```

---

### 3. Flow mẫu cho Student

1. Đăng ký Student → đăng nhập lấy JWT.
2. Vào Swagger:
   - Xem danh sách ca bệnh (`/api/Student/cases`).
   - Mở chi tiết ca để xem ảnh (`/api/Student/cases/{caseId}`).
   - Tạo annotation vùng nghi ngờ (`/api/Student/annotations`).
   - Gửi câu hỏi cho hệ thống (lưu lịch sử) (`/api/Student/questions`).
3. Làm quiz:
   - Xem quiz được giao (`/api/Student/quizzes`).
   - Bắt đầu một quiz (`/api/Student/quizzes/{quizId}/start`).
   - Nộp bài (`/api/Student/quizzes/submit`).
4. Xem `progress` để theo dõi quá trình học.

