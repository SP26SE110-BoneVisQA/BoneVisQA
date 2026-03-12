# BoneVisQA - Ghi chú các thay đổi (Update Notes)

## Ngày cập nhật: 10/03/2026

---

## 1. Database Changes

### MedicalCase.cs
- Thêm field `IsApproved` (bool, default: false) - để duyệt case trước khi hiển thị cho sinh viên
- Thêm field `IsActive` (bool, default: true) - để active/deactive case

---

## 2. Student Features (New APIs)

### 2.1. Filter Cases
- **Endpoint:** `GET /api/student/cases/filter`
- **Query Parameters:**
  - `studentId` (required)
  - `CategoryId` (optional)
  - `Difficulty` (optional): basic, intermediate, advanced
  - `Location` (optional): long bones, spine, knee, hip, shoulder, wrist, ankle
  - `LessonType` (optional): fracture, dislocation, degenerative disease, bone tumor, osteomyelitis
- **Function:** Lọc cases theo category, difficulty, location, lesion type

### 2.2. View Announcements
- **Endpoint:** `GET /api/student/announcements`
- **Query Parameters:**
  - `studentId` (required)
- **Function:** Lấy danh sách thông báo từ các lớp mà sinh viên đã đăng ký

### 2.3. Updated Get Cases
- **Endpoint:** `GET /api/student/cases`
- **Changes:** Chỉ lấy các case có `IsApproved = true` và `IsActive = true`
- **Response:** Thêm các fields `IsApproved`, `Tags`

---

## 3. Lecturer Features (New APIs)

### 3.1. Quiz Question Management
- **Add Question:** `POST /api/lecturer/quizzes/{quizId}/questions`
  - Body: `{ QuizId, CaseId, QuestionText, Type, CorrectAnswer, Options }`
- **Get Questions:** `GET /api/lecturer/quizzes/{quizId}/questions`
- **Update Question:** `PUT /api/lecturer/quizzes/questions/{questionId}`
  - Body: `{ QuestionText, Type, CorrectAnswer, Options }`
- **Delete Question:** `DELETE /api/lecturer/quizzes/questions/{questionId}`

### 3.2. Case Management
- **Get All Cases:** `GET /api/lecturer/cases`
  - Trả về tất cả cases (bao gồm cả chưa duyệt)
- **Assign Cases to Class:** `POST /api/lecturer/classes/{classId}/cases`
  - Body: `{ CaseIds: [] }`
- **Approve Case:** `PUT /api/lecturer/cases/{caseId}/approve`
  - Body: `{ IsApproved: true/false }`

### 3.3. Student Questions Management
- **Get Student Questions:** `GET /api/lecturer/classes/{classId}/questions`
  - Query Parameters:
    - `caseId` (optional)
    - `studentId` (optional)
  - Function: Xem câu hỏi của sinh viên trong lớp, có thể lọc theo case hoặc student

### 3.4. Announcements
- **Get Class Announcements:** `GET /api/lecturer/classes/{classId}/announcements`

---

## 4. Các Files được tạo mới

1. `BoneVisQA.Services\Models\Student\AnnouncementDtos.cs`
   - `CaseFilterRequestDto`
   - `AnnouncementDto`

2. `BoneVisQA.Services\Models\Lecturer\CaseManagementDto.cs`
   - `CaseDto`
   - `AssignCasesToClassRequestDto`
   - `ApproveCaseRequestDto`
   - `StudentQuestionDto`

3. `BoneVisQA.Repositories\Models\CaseFilter.cs`
   - `CaseFilter` (internal model for filtering)

---

## 5. Các Files được cập nhật

1. **Repositories:**
   - `MedicalCase.cs` - thêm IsApproved, IsActive
   - `IStudentRepository.cs` - thêm GetFilteredCasesAsync, GetAnnouncementsForStudentAsync
   - `StudentRepository.cs` - implement các method mới

2. **Services:**
   - `IStudentService.cs` - thêm GetFilteredCasesAsync, GetAnnouncementsAsync
   - `StudentService.cs` - implement các method mới
   - `ILecturerService.cs` - thêm quiz question management, case management, student questions
   - `LecturerService.cs` - implement các method mới
   - `CaseDtos.cs` (Student) - thêm IsApproved, Tags

3. **Controllers:**
   - `StudentController.cs` - thêm endpoints filter cases, announcements
   - `LecturerController.cs` - thêm quiz questions, case management, student questions

---

## 6. Chưa implement (RAG-related)

- Nhận câu trả lời từ AI/RAG
- Xem citations/references
- Expert Review workflow (xem/sửa câu trả lời)

---

## 7. Flow hoạt động

### Student Flow:
1. Login → Lấy danh sách cases (`GET /api/student/cases`)
2. Filter cases theo category/difficulty/location (`GET /api/student/cases/filter`)
3. Xem chi tiết case → Tạo annotation → Đặt câu hỏi
4. Làm quiz → Xem kết quả
5. Xem tiến độ học tập (`GET /api/student/progress`)
6. Xem thông báo (`GET /api/student/announcements`)

### Lecturer Flow:
1. Tạo class → Thêm sinh viên vào lớp
2. Tạo quiz → Thêm câu hỏi vào quiz
3. Xem/duyệt cases → Approve cases
4. Gán cases cho lớp
5. Xem câu hỏi của sinh viên
6. Tạo thông báo cho lớp
7. Xem thống kê lớp học
