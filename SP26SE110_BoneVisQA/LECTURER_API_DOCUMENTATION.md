# Tài liệu API dành cho Giảng viên (Lecturer)

## Base URL
```
/api/lecturers
```

## Authentication
- Role required: `Lecturer`
- Sử dụng JWT token với role "Lecturer"

---

## 1. Quản lý Lớp học (Classes)

### 1.1 Tạo lớp học mới
**POST** `/api/lecturers/classes`

| Thông tin | Chi tiết |
|-----------|----------|
| Body | `CreateClassRequestDto` |
| Response | `ClassDto` |

**Request Body:**
```json
{
  "className": "string",
  "description": "string"
}
```

**Response:**
```json
{
  "id": "guid",
  "className": "string",
  "description": "string",
  "lecturerId": "guid",
  "createdAt": "datetime"
}
```

---

### 1.2 Lấy danh sách lớp của giảng viên
**GET** `/api/lecturers/classes`

| Thông tin | Chi tiết |
|-----------|----------|
| Query Param | `lecturerId` (guid) |
| Response | `List<ClassDto>` |

---

## 2. Quản lý Sinh viên (Students)

### 2.1 Thêm một sinh viên vào lớp
**POST** `/api/lecturers/classes/{classId}/enroll`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Body | `EnrollStudentRequestDto` |
| Response | 204 No Content |
| Error | 409 Conflict (nếu sinh viên đã có trong lớp) |

**Request Body:**
```json
{
  "studentId": "guid"
}
```

---

### 2.2 Thêm nhiều sinh viên vào lớp
**POST** `/api/lecturers/classes/{classId}/enrollmany`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Body | `EnrollStudentsRequestDto` |
| Response | `List<StudentEnrollmentDto>` |

**Request Body:**
```json
{
  "studentIds": ["guid1", "guid2", "guid3"]
}
```

---

### 2.3 Xóa sinh viên khỏi lớp
**DELETE** `/api/lecturers/classes/{classId}/students/{studentId}`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid), `studentId` (guid) |
| Response | 204 No Content |
| Error | 404 Not Found |

---

### 2.4 Lấy danh sách sinh viên trong lớp
**GET** `/api/lecturers/classes/{classId}/students`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Response | `List<StudentEnrollmentDto>` |

---

### 2.5 Lấy danh sách sinh viên chưa tham gia lớp
**GET** `/api/lecturers/classes/{classId}/students/available`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Response | `List<StudentEnrollmentDto>` |

---

## 3. Thông báo (Announcements)

### 3.1 Tạo thông báo cho lớp
**POST** `/api/lecturers/classes/{classId}/announcements`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Body | `CreateAnnouncementRequestDto` |
| Response | `AnnouncementDto` |

**Request Body:**
```json
{
  "title": "string",
  "content": "string"
}
```

---

### 3.2 Lấy danh sách thông báo của lớp
**GET** `/api/lecturers/classes/{classId}/announcements`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Response | `List<AnnouncementDto>` |

---

## 4. Quiz

### 4.1 Tạo quiz mới
**POST** `/api/lecturers/classes/{classId}/quizzes`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Body | `CreateQuizRequestDto` |
| Response | `QuizDto` |

**Request Body:**
```json
{
  "title": "string",
  "timeLimit": 30,
  "openTime": "2024-01-01T00:00:00Z",
  "closeTime": "2024-01-31T23:59:59Z",
  "passingScore": 60
}
```

---

### 4.2 Thêm câu hỏi vào quiz
**POST** `/api/lecturers/quizzes/{quizId}/questions`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `quizId` (guid) |
| Body | `CreateQuizQuestionRequestDto` |
| Response | `QuizQuestionDto` |

**Request Body:**
```json
{
  "questionText": "string",
  "type": "string",
  "correctAnswer": "string",
  "caseId": "guid"
}
```

---

### 4.3 Lấy danh sách câu hỏi của quiz
**GET** `/api/lecturers/quizzes/{quizId}/questions`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `quizId` (guid) |
| Response | `List<QuizQuestionDto>` |

---

### 4.4 Cập nhật câu hỏi
**PUT** `/api/lecturers/quizzes/questions/{questionId}`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `questionId` (guid) |
| Body | `UpdateQuizQuestionRequestDto` |
| Response | 204 No Content |
| Error | 404 Not Found |

**Request Body:**
```json
{
  "questionText": "string",
  "type": "string",
  "correctAnswer": "string"
}
```

---

### 4.5 Xóa câu hỏi
**DELETE** `/api/lecturers/quizzes/questions/{questionId}`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `questionId` (guid) |
| Response | 204 No Content |
| Error | 404 Not Found |

---

## 5. Case Y khoa (Medical Cases)

### 5.1 Lấy danh sách tất cả case
**GET** `/api/lecturers/cases`

| Thông tin | Chi tiết |
|-----------|----------|
| Response | `List<CaseDto>` |

---

### 5.2 Gán case cho lớp học
**POST** `/api/lecturers/classes/{classId}/cases`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Body | `AssignCasesToClassRequestDto` |
| Response | `List<CaseDto>` |

**Request Body:**
```json
{
  "caseIds": ["guid1", "guid2", "guid3"]
}
```

---

### 5.3 Phê duyệt/bác bỏ case
**PUT** `/api/lecturers/cases/{caseId}/approve`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `caseId` (guid) |
| Body | `ApproveCaseRequestDto` |
| Response | 204 No Content |
| Error | 404 Not Found |

**Request Body:**
```json
{
  "isApproved": true,
  "rejectionReason": "string"
}
```

---

## 6. Câu hỏi của Sinh viên

### 6.1 Lấy câu hỏi của sinh viên
**GET** `/api/lecturers/classes/{classId}/questions`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Query Param | `caseId` (guid, optional), `studentId` (guid, optional) |
| Response | `List<LectStudentQuestionDto>` |

---

## 7. Thống kê (Statistics)

### 7.1 Lấy thống kê của lớp
**GET** `/api/lecturers/classes/{classId}/stats`

| Thông tin | Chi tiết |
|-----------|----------|
| Path Param | `classId` (guid) |
| Response | `ClassStatsDto` |

---

## Tổng hợp API Endpoints

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| POST | `/classes` | Tạo lớp học |
| GET | `/classes` | Danh sách lớp |
| POST | `/classes/{classId}/enroll` | Thêm 1 sinh viên |
| POST | `/classes/{classId}/enrollmany` | Thêm nhiều sinh viên |
| DELETE | `/classes/{classId}/students/{studentId}` | Xóa sinh viên |
| GET | `/classes/{classId}/students` | DS sinh viên trong lớp |
| GET | `/classes/{classId}/students/available` | DS sinh viên chưa enroll |
| POST | `/classes/{classId}/announcements` | Tạo thông báo |
| GET | `/classes/{classId}/announcements` | DS thông báo |
| POST | `/classes/{classId}/quizzes` | Tạo quiz |
| GET | `/classes/{classId}/stats` | Thống kê lớp |
| POST | `/quizzes/{quizId}/questions` | Thêm câu hỏi |
| GET | `/quizzes/{quizId}/questions` | DS câu hỏi |
| PUT | `/quizzes/questions/{questionId}` | Cập nhật câu hỏi |
| DELETE | `/quizzes/questions/{questionId}` | Xóa câu hỏi |
| GET | `/cases` | DS tất cả case |
| POST | `/classes/{classId}/cases` | Gán case cho lớp |
| PUT | `/cases/{caseId}/approve` | Phê duyệt case |
| GET | `/classes/{classId}/questions` | DS câu hỏi sinh viên |
