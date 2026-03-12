## Ghi chú chức năng & cách chạy (Lecturer / Student)

### 1. Cách chạy API

- **Chạy backend**:
  - Mở terminal tại thư mục solution.
  - Lệnh: `dotnet run --project BoneVisQA.API`
  - API mặc định chạy ở `https://localhost:5001` (hoặc URL hiển thị trong console).
- **Xem & test nhanh**:
  - Mở Swagger: truy cập `https://localhost:5001/swagger` trên trình duyệt.

### 2. Đăng ký & đăng nhập (dùng cho cả Student và Lecturer)

- **Đăng ký** – `POST /api/Auths/register`
  - Body JSON mẫu (Student):
    ```json
    {
      "fullName": "Nguyen Van A",
      "email": "student@example.com",
      "password": "P@ssw0rd!",
      "schoolCohort": "Y4 - Orthopedics 2026",
      "roleName": "Student"
    }
    ```
  - Body JSON mẫu (Lecturer):
    ```json
    {
      "fullName": "Dr. Lecturer",
      "email": "lecturer@example.com",
      "password": "P@ssw0rd!",
      "schoolCohort": "Faculty of Medicine",
      "roleName": "Lecturer"
    }
    ```
- **Đăng nhập** – `POST /api/Auths/login`
  - Body:
    ```json
    {
      "email": "student@example.com",
      "password": "P@ssw0rd!"
    }
    ```
  - Kết quả trả về có `token` (JWT). Khi gọi các API khác, thêm header:
    - `Authorization: Bearer <token>`

> Hiện tại các controller đang nhận `studentId` / `lecturerId` qua query/body. Sau này có thể lấy ID trực tiếp từ JWT để bảo mật hơn.

---

### 3. Chức năng & API cho Lecturer

#### 3.1. Tạo lớp học (Class)

- **Endpoint**: `POST /api/Lecturer/classes`
- **Body mẫu**:
  ```json
  {
    "className": "Orthopedics - Class A 2026",
    "semester": "Spring 2026",
    "lecturerId": "GUID-LECTURER-ID"
  }
  ```
- **Kết quả**: `ClassDto` chứa `id`, `className`, `semester`, `lecturerId`, `createdAt`.

#### 3.2. Xem danh sách lớp của Lecturer

- **Endpoint**: `GET /api/Lecturer/classes?lecturerId={GUID}`
- **Kết quả**: danh sách `ClassDto` mà giảng viên quản lý.

#### 3.3. Ghi danh sinh viên vào lớp

- **Endpoint**: `POST /api/Lecturer/classes/{classId}/enroll`
- **Body mẫu**:
  ```json
  {
    "studentId": "GUID-STUDENT-ID"
  }
  ```
- **Kết quả**:
  - `204 No Content` nếu thành công.
  - `409 Conflict` nếu Student đã trong lớp.

#### 3.4. Tạo thông báo cho lớp (Announcement)

- **Endpoint**: `POST /api/Lecturer/classes/{classId}/announcements`
- **Body mẫu**:
  ```json
  {
    "title": "New bone fracture cases",
    "content": "Please review the new fracture cases before next week."
  }
  ```
- **Kết quả**: `AnnouncementDto` với `id`, `classId`, `title`, `content`, `createdAt`.

#### 3.5. Tạo quiz cho lớp

- **Endpoint**: `POST /api/Lecturer/classes/{classId}/quizzes`
- **Body mẫu**:
  ```json
  {
    "title": "Long bone fractures quiz",
    "openTime": "2026-03-01T00:00:00Z",
    "closeTime": "2026-03-10T23:59:59Z",
    "timeLimit": 900,
    "passingScore": 60
  }
  ```
- **Kết quả**: `QuizDto` với `id`, `classId`, `title`, `openTime`, `closeTime`, `timeLimit`, `passingScore`.

#### 3.6. Xem thống kê học tập của lớp

- **Endpoint**: `GET /api/Lecturer/classes/{classId}/stats`
- **Kết quả**: `ClassStatsDto` với:
  - `totalStudents`, `totalCasesViewed`, `totalQuestionsAsked`, `avgQuizScore`.

---

### 4. Chức năng & API cho Student

#### 4.1. Xem danh sách ca bệnh

- **Endpoint**: `GET /api/Student/cases?studentId={GUID}`
- **Kết quả**: danh sách `CaseListItemDto`:
  - `id`, `title`, `description`, `difficulty`, `categoryName`, `thumbnailImageUrl`.

#### 4.2. Xem chi tiết ca bệnh (và ghi log xem ca)

- **Endpoint**: `GET /api/Student/cases/{caseId}?studentId={GUID}`
- **Hành vi**:
  - Lưu một `CaseViewLog` cho sinh viên đó.
  - Trả về `CaseDetailDto`:
    - Thông tin ca + danh sách ảnh (`images`: `id`, `imageUrl`, `modality`).

#### 4.3. Tạo annotation trên ảnh

- **Endpoint**: `POST /api/Student/annotations?studentId={GUID}`
- **Body mẫu**:
  ```json
  {
    "imageId": "GUID-IMAGE-ID",
    "label": "Suspicious fracture line",
    "coordinates": "{\"x\":100,\"y\":150,\"width\":80,\"height\":60}"
  }
  ```
- **Kết quả**: `AnnotationDto` với `id`, `imageId`, `label`, `coordinates`, `createdAt`.

#### 4.4. Gửi câu hỏi (Visual Q&A – phần lưu lịch sử)

- **Endpoint**: `POST /api/Student/questions?studentId={GUID}`
- **Body mẫu**:
  ```json
  {
    "caseId": "GUID-CASE-ID",
    "annotationId": "GUID-ANNOTATION-ID",
    "questionText": "What type of fracture does this lesion suggest?",
    "language": "en"
  }
  ```
- **Kết quả**: `StudentQuestionDto` (chỉ mới lưu câu hỏi; phần AI/RAG trả lời sẽ tích hợp sau).

#### 4.5. Xem lịch sử câu hỏi của sinh viên

- **Endpoint**: `GET /api/Student/questions?studentId={GUID}`
- **Kết quả**: danh sách `StudentQuestionHistoryItemDto`:
  - `id`, `caseId`, `questionText`, `createdAt`.

#### 4.6. Xem danh sách quiz được gán

- **Endpoint**: `GET /api/Student/quizzes?studentId={GUID}`
- **Kết quả**: danh sách `QuizListItemDto`:
  - `quizId`, `title`, `openTime`, `closeTime`, `timeLimit`, `passingScore`,
    `isCompleted`, `score`.

#### 4.7. Bắt đầu làm quiz

- **Endpoint**: `POST /api/Student/quizzes/{quizId}/start?studentId={GUID}`
- **Kết quả**: `QuizSessionDto`:
  - `attemptId`, `quizId`, `title`, danh sách `questions` (`questionId`, `questionText`, `type`, `caseId`).

#### 4.8. Nộp bài quiz

- **Endpoint**: `POST /api/Student/quizzes/submit?studentId={GUID}`
- **Body mẫu**:
  ```json
  {
    "attemptId": "GUID-ATTEMPT-ID",
    "answers": [
      {
        "questionId": "GUID-QUESTION-1",
        "studentAnswer": "A"
      },
      {
        "questionId": "GUID-QUESTION-2",
        "studentAnswer": "B"
      }
    ]
  }
  ```
- **Kết quả**: `QuizResultDto`:
  - `attemptId`, `quizId`, `score`, `passingScore`, `passed`.

#### 4.9. Xem tổng quan tiến độ học tập

- **Endpoint**: `GET /api/Student/progress?studentId={GUID}`
- **Kết quả**: `StudentProgressDto`:
  - `totalCasesViewed` – tổng số ca đã mở.
  - `totalQuestionsAsked` – tổng số câu hỏi gửi hệ thống.
  - `avgQuizScore` – điểm trung bình các quiz đã làm.

---

### 5. Gợi ý cách sử dụng tổng thể

1. **Lecturer**:
   - Đăng ký tài khoản với `roleName = "Lecturer"`, đăng nhập lấy token.
   - Dùng các API `Lecturer` để:
     - Tạo lớp → Ghi danh sinh viên → Tạo thông báo → Tạo quiz.
   - Dùng API thống kê lớp để theo dõi kết quả học tập.
2. **Student**:
   - Đăng ký với `roleName = "Student"`, đăng nhập lấy token.
   - Xem danh sách ca bệnh, mở ca để xem chi tiết, tạo annotation vùng nghi ngờ.
   - Gửi câu hỏi (có thể kèm `annotationId`) để lưu lịch sử hỏi-đáp.
   - Làm quiz được giảng viên giao (`quizzes` → `start` → `submit`).
   - Xem `progress` để tự đánh giá quá trình học.

