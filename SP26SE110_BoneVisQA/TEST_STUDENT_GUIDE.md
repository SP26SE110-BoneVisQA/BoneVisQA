# BoneVisQA - Hướng dẫn Test API cho Student
# API Base URL: http://localhost:5046

---

## 1. CẤU TRÚC DỮ LIỆU MẪU (SQL)

### 1.1. Tạo Users (Users - Tài khoản)

```sql
-- Students (Sinh viên)
INSERT INTO users (id, full_name, email, password, school_cohort, is_active, created_at) VALUES
('aaaaaaa1-1111-1111-1111-111111111111', 'Le Thi C', 'student1@edu.vn', '$2a$10$...', 'K2024', true, NOW()),
('aaaaaaa2-2222-2222-2222-222222222222', 'Pham Van D', 'student2@edu.vn', '$2a$10$...', 'K2024', true, NOW()),
('aaaaaaa3-3333-3333-3333-333333333333', 'Hoang Van E', 'student3@edu.vn', '$2a$10$...', 'K2023', true, NOW()),
('aaaaaaa4-4444-4444-4444-444444444444', 'Dao Thi F', 'student4@edu.vn', '$2a$10$...', 'K2023', true, NOW());
```

### 1.2. Tạo Roles (Vai trò)

```sql
INSERT INTO roles (id, name, description, created_at) VALUES
('r-4444-4444-4444-44444444444444', 'Student', 'Sinh viên', NOW());
```

### 1.3. Gán Roles cho Users (UserRoles)

```sql
-- Gán role Student cho sinh viên
INSERT INTO user_roles (id, user_id, role_id, assigned_at) VALUES
('ur-3333-3333-3333-333333333333', 'aaaaaaa1-1111-1111-1111-111111111111', 'r-4444-4444-4444-44444444444444', NOW()),
('ur-4444-4444-4444-444444444444', 'aaaaaaa2-2222-2222-2222-222222222222', 'r-4444-4444-4444-44444444444444', NOW()),
('ur-5555-5555-5555-555555555555', 'aaaaaaa3-3333-3333-3333-333333333333', 'r-4444-4444-4444-44444444444444', NOW()),
('ur-6666-6666-6666-666666666666', 'aaaaaaa4-4444-4444-4444-444444444444', 'r-4444-4444-4444-44444444444444', NOW());
```

### 1.4. Tạo Categories (Danh mục ca bệnh)

```sql
INSERT INTO categories (id, name, description) VALUES
('c-1111-1111-1111-111111111111', 'Xương khớp', 'Các ca bệnh về xương và khớp'),
('c-2222-2222-2222-222222222222', 'Cột sống', 'Các ca bệnh về cột sống'),
('c-3333-3333-3333-333333333333', 'Chấn thương', 'Các ca bệnh chấn thương');
```

### 1.5. Tạo MedicalCases (Ca bệnh)

```sql
INSERT INTO medical_cases (id, title, description, difficulty, category_id, is_approved, is_active, created_at) VALUES
('mc-1111-1111-1111-111111111111', 'Gãy xương đùi', 'Ca gãy xương đùi do tai nạn giao thông', 'Cao', 'c-1111-1111-1111-111111111111', true, true, NOW()),
('mc-2222-2222-2222-222222222222', 'Thoái hóa cột sống', 'Ca thoái hóa cột sống ở người cao tuổi', 'Trung bình', 'c-2222-2222-2222-222222222222', true, true, NOW()),
('mc-3333-3333-3333-333333333333', 'Trật khớp gối', 'Ca trật khớp gối do chấn thương thể thao', 'Thấp', 'c-1111-1111-1111-111111111111', true, true, NOW());
```

### 1.6. Tạo AcademicClasses (Lớp học)

```sql
INSERT INTO academic_classes (id, class_name, semester, lecturer_id, created_at) VALUES
('ac-1111-1111-1111-111111111111', 'Xương Khớp A - 2024', '2024.1', '11111111-1111-1111-1111-111111111111', NOW());
```

### 1.7. Tạo ClassEnrollments (Đăng ký lớp)

```sql
INSERT INTO class_enrollments (id, class_id, student_id, enrolled_at) VALUES
('ce-1111-1111-1111-111111111111', 'ac-1111-1111-1111-111111111111', 'aaaaaaa1-1111-1111-1111-111111111111', NOW()),
('ce-2222-2222-2222-222222222222', 'ac-1111-1111-1111-111111111111', 'aaaaaaa2-2222-2222-2222-222222222222', NOW());
```

### 1.8. Tạo Announcements (Thông báo)

```sql
INSERT INTO announcements (id, class_id, title, content, created_at) VALUES
('an-1111-1111-1111-111111111111', 'ac-1111-1111-1111-111111111111', 'Bài giảng tuần 1', 'Chào mừng các sinh viên đến với lớp Xương Khớp A', NOW()),
('an-2222-2222-2222-222222222222', 'ac-1111-1111-1111-111111111111', 'Lịch thi giữa kỳ', 'Kỳ thi giữa kỳ sẽ diễn ra vào tuần 8', NOW());
```

### 1.9. Tạo Quizzes (Bài kiểm tra)

```sql
INSERT INTO quizzes (id, title, description, time_limit_minutes, is_published, created_at) VALUES
('qz-1111-1111-1111-111111111111', 'Kiểm tra Xương khớp', 'Bài kiểm tra chương 1', 30, true, NOW());

INSERT INTO class_quizzes (id, class_id, quiz_id, assigned_at) VALUES
('cq-1111-1111-1111-111111111111', 'ac-1111-1111-1111-111111111111', 'qz-1111-1111-1111-111111111111', NOW());
```

### 1.10. Tạo QuizQuestions (Câu hỏi trắc nghiệm)

```sql
INSERT INTO quiz_questions (id, quiz_id, case_id, question_text, question_type, correct_answer, options, created_at) VALUES
('qq-1111-1111-1111-111111111111', 'qz-1111-1111-1111-111111111111', 'mc-1111-1111-1111-111111111111',
 'Xương đùi gãy tại vị trí nào là phổ biến nhất?',
 'multiple_choice',
 '1/3 giữa xương đùi',
 '["1/3 gần xương đùi","1/3 giữa xương đùi","1/3 xa xương đùi","Vùng trên lồi cầu"]',
 NOW());
```

---

## 2. HƯỚNG DẪN TEST API BẰNG POSTMAN

### 2.1. Cấu hình Postman

```
Base URL: http://localhost:5046
Content-Type: application/json
```

### 2.2. CÁC BƯỚC TEST CHO STUDENT

---

#### Bước 1: Đăng nhập Student

**POST** `http://localhost:5046/api/auths/login`

```json
{
    "email": "student1@edu.vn",
    "password": "Password123!"
}
```

**Response mẫu:**
```json
{
    "success": true,
    "userId": "aaaaaaa1-1111-1111-1111-111111111111",
    "email": "student1@edu.vn",
    "fullName": "Le Thi C",
    "roles": ["Student"],
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

> Lưu ý: Copy `token` từ response để sử dụng cho các request tiếp theo.

---

#### Bước 2: Cấu hình Authorization cho Student

Trong Postman:
- Type: **Bearer Token**
- Token: Paste token từ bước đăng nhập

---

#### Bước 3: Xem danh sách Cases (đã duyệt và active)

**GET** `http://localhost:5046/api/students/cases?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
[
    {
        "id": "mc-1111-1111-1111-111111111111",
        "title": "Gãy xương đùi",
        "description": "Ca gãy xương đùi do tai nạn giao thông",
        "difficulty": "Cao",
        "categoryName": "Xương khớp",
        "isApproved": true,
        "thumbnailImageUrl": "https://example.com/xray1.jpg",
        "tags": ["gãy xương", "tai nạn"]
    },
    {
        "id": "mc-2222-2222-2222-222222222222",
        "title": "Thoái hóa cột sống",
        "description": "Ca thoái hóa cột sống ở người cao tuổi",
        "difficulty": "Trung bình",
        "categoryName": "Cột sống",
        "isApproved": true,
        "thumbnailImageUrl": "https://example.com/xray2.jpg",
        "tags": ["thoái hóa", "người già"]
    }
]
```

---

#### Bước 4: Lọc Cases theo điều kiện

**GET** `http://localhost:5046/api/students/cases/filter?studentId=aaaaaaa1-1111-1111-1111-111111111111&categoryId=c-1111-1111-1111-111111111111&difficulty=Cao`

**Các tham số filter:**
| Tham số | Mô tả | Ví dụ |
|---------|-------|-------|
| `categoryId` | Lọc theo danh mục | `c-1111-1111-1111-111111111111` |
| `difficulty` | Lọc theo độ khó | `Thấp`, `Trung bình`, `Cao` |
| `location` | Lọc theo vị trí | `Hà Nội`, `TP.HCM` |
| `lessonType` | Lọc theo loại bài học | `Lý thuyết`, `Thực hành` |

---

#### Bước 5: Xem Chi tiết Case

**GET** `http://localhost:5046/api/students/cases/mc-1111-1111-1111-111111111111?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
{
    "id": "mc-1111-1111-1111-111111111111",
    "title": "Gãy xương đùi",
    "description": "Ca gãy xương đùi do tai nạn giao thông",
    "difficulty": "Cao",
    "categoryName": "Xương khớp",
    "isApproved": true,
    "images": [
        {
            "id": "img-1111-1111-1111-111111111111",
            "imageUrl": "https://example.com/xray1.jpg",
            "modality": "X-quang"
        }
    ]
}
```

---

#### Bước 6: Tạo Annotation (Đánh dấu)

**POST** `http://localhost:5046/api/students/annotations?studentId=aaaaaaa1-1111-1111-1111-111111111111`

```json
{
    "caseId": "mc-1111-1111-1111-111111111111",
    "annotationType": "highlight",
    "description": "Vùng gãy xương",
    "coordinates": "{\"x\":100,\"y\":200}",
    "imageUrl": "https://example.com/annotation1.png"
}
```

**Expected Response (200 OK):**
```json
{
    "id": "ca-1111-1111-1111-111111111111",
    "caseId": "mc-1111-1111-1111-111111111111",
    "studentId": "aaaaaaa1-1111-1111-1111-111111111111",
    "annotationType": "highlight",
    "description": "Vùng gãy xương",
    "coordinates": "{\"x\":100,\"y\":200}",
    "createdAt": "2024-01-15T11:00:00Z"
}
```

---

#### Bước 7: Đặt câu hỏi về Case

**POST** `http://localhost:5046/api/students/questions?studentId=aaaaaaa1-1111-1111-1111-111111111111`

```json
{
    "caseId": "mc-1111-1111-1111-111111111111",
    "questionText": "Tại sao xương đùi dễ gãy ở vị trí 1/3 giữa?",
    "language": "vi",
    "customImageUrl": null,
    "customCoordinates": null
}
```

**Expected Response (200 OK):**
```json
{
    "id": "sq-new-1111-1111-1111-111111111111",
    "studentId": "aaaaaaa1-1111-1111-1111-111111111111",
    "caseId": "mc-1111-1111-1111-111111111111",
    "questionText": "Tại sao xương đùi dễ gãy ở vị trí 1/3 giữa?",
    "language": "vi",
    "createdAt": "2024-01-15T11:05:00Z"
}
```

---

#### Bước 8: Xem Lịch sử câu hỏi

**GET** `http://localhost:5046/api/students/questions?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
[
    {
        "id": "sq-1111-1111-1111-111111111111",
        "caseId": "mc-1111-1111-1111-111111111111",
        "caseTitle": "Gãy xương đùi",
        "questionText": "Tại sao xương đùi dễ gãy ở vị trí 1/3 giữa?",
        "answerText": "Vì đây là vị trí có moment uốn lớn nhất...",
        "answeredAt": "2024-01-16T14:00:00Z",
        "createdAt": "2024-01-15T09:00:00Z"
    }
]
```

---

#### Bước 9: Xem Thông báo

**GET** `http://localhost:5046/api/students/announcements?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
[
    {
        "id": "an-1111-1111-1111-111111111111",
        "classId": "ac-1111-1111-1111-111111111111",
        "className": "Xương Khớp A - 2024",
        "title": "Bài giảng tuần 1",
        "content": "Chào mừng các sinh viên đến với lớp Xương Khớp A",
        "createdAt": "2024-01-15T10:40:00Z"
    }
]
```

---

#### Bước 10: Xem danh sách Quiz

**GET** `http://localhost:5046/api/students/quizzes?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
[
    {
        "id": "qz-1111-1111-1111-111111111111",
        "title": "Kiểm tra Xương khớp",
        "description": "Bài kiểm tra chương 1",
        "timeLimitMinutes": 30,
        "questionCount": 5,
        "dueDate": "2024-01-30T23:59:59Z",
        "status": "available"
    }
]
```

---

#### Bước 11: Bắt đầu làm Quiz

**POST** `http://localhost:5046/api/students/quizzes/qz-1111-1111-1111-111111111111/start?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
{
    "attemptId": "at-1111-1111-1111-111111111111",
    "quizId": "qz-1111-1111-1111-111111111111",
    "questions": [
        {
            "id": "qq-1111-1111-1111-111111111111",
            "questionText": "Xương đùi gãy tại vị trí nào là phổ biến nhất?",
            "options": ["1/3 gần xương đùi", "1/3 giữa xương đùi", "1/3 xa xương đùi", "Vùng trên lồi cầu"]
        }
    ],
    "startedAt": "2024-01-15T11:10:00Z",
    "expiresAt": "2024-01-15T11:40:00Z"
}
```

---

#### Bước 12: Nộp Quiz

**POST** `http://localhost:5046/api/students/quizzes/submit?studentId=aaaaaaa1-1111-1111-1111-111111111111`

```json
{
    "attemptId": "at-1111-1111-1111-111111111111",
    "quizId": "qz-1111-1111-1111-111111111111",
    "answers": [
        {
            "questionId": "qq-1111-1111-1111-111111111111",
            "answer": "1/3 giữa xương đùi"
        }
    ]
}
```

**Expected Response (200 OK):**
```json
{
    "attemptId": "at-1111-1111-1111-111111111111",
    "totalQuestions": 5,
    "correctAnswers": 4,
    "score": 80.0,
    "passed": true,
    "submittedAt": "2024-01-15T11:25:00Z"
}
```

---

#### Bước 13: Xem Tiến độ học tập

**GET** `http://localhost:5046/api/students/progress?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
{
    "studentId": "aaaaaaa1-1111-1111-1111-111111111111",
    "totalCasesViewed": 10,
    "totalQuestionsAsked": 5,
    "totalQuestionsAnswered": 4,
    "quizzesCompleted": 3,
    "averageQuizScore": 85.5,
    "recentActivity": [
        {
            "type": "case_view",
            "caseId": "mc-1111-1111-1111-111111111111",
            "caseTitle": "Gãy xương đùi",
            "timestamp": "2024-01-15T11:00:00Z"
        }
    ]
}
```

---

## 3. CÁC TRƯỜNG HỢP TEST LỖI

### 3.1. Test trường hợp không đăng nhập

**Request:** GET `http://localhost:5046/api/students/cases`

**Expected:** `401 Unauthorized`

```json
{
    "message": "Không xác thực được người dùng"
}
```

---

### 3.2. Test đăng nhập với sai mật khẩu

**Request:** POST `http://localhost:5046/api/auths/login`

```json
{
    "email": "student1@edu.vn",
    "password": "WrongPassword!"
}
```

**Expected:** `401 Unauthorized`

```json
{
    "success": false,
    "message": "Email hoặc mật khẩu không đúng"
}
```

---

### 3.3. Test xem chi tiết case không tồn tại

**Request:** GET `http://localhost:5046/api/students/cases/zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz?studentId=aaaaaaa1-1111-1111-1111-111111111111`

**Expected:** `404 Not Found`

---

## 4. CREDENTIALS MẪU ĐỂ TEST

### Student Accounts:
| Email | Password | Role | Cohort |
|-------|----------|------|--------|
| student1@edu.vn | Password123! | Student | K2024 |
| student2@edu.vn | Password123! | Student | K2024 |
| student3@edu.vn | Password123! | Student | K2023 |
| student4@edu.vn | Password123! | Student | K2023 |

---

## 5. MẸO TEST

1. **Luôn lưu token sau khi đăng nhập** - Sử dụng Postman Environment để lưu token và sử dụng lại cho các request khác.

2. **Sử dụng Swagger UI** - Truy cập http://localhost:5046/swagger để xem và test trực tiếp các API endpoints.

3. **Kiểm tra database** - Nếu có quyền truy cập Supabase dashboard, kiểm tra dữ liệu đã được tạo đúng chưa.

4. **Test theo thứ tự** - Nên test theo thứ tự các bước đã liệt kê ở trên vì có một số API phụ thuộc vào dữ liệu từ API trước.

5. **Dọn dẹp dữ liệu test** - Sau khi test xong, nên xóa các dữ liệu test để tránh ảnh hưởng đến các lần test sau.
