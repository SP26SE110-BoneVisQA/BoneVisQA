# BoneVisQA - Hướng dẫn Test API cho Lecturer
# API Base URL: http://localhost:5046

---

## 1. CẤU TRÚC DỮ LIỆU MẪU (SQL)

### 1.1. Tạo Users (Users - Tài khoản)

```sql
-- Lecturers (Giảng viên)
INSERT INTO users (id, full_name, email, password, is_active, created_at) VALUES
('11111111-1111-1111-1111-111111111111', 'Nguyen Van A', 'lecturer1@edu.vn', '$2a$10$...', true, NOW()),
('22222222-2222-2222-2222-222222222222', 'Tran Thi B', 'lecturer2@edu.vn', '$2a$10$...', true, NOW());

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
('r-1111-1111-1111-11111111111111', 'Admin', 'Quản trị viên', NOW()),
('r-2222-2222-2222-22222222222222', 'Lecturer', 'Giảng viên', NOW()),
('r-3333-3333-3333-33333333333333', 'Expert', 'Chuyên gia', NOW()),
('r-4444-4444-4444-44444444444444', 'Student', 'Sinh viên', NOW());
```

### 1.3. Gán Roles cho Users (UserRoles)

```sql
-- Gán role Lecturer cho giảng viên
INSERT INTO user_roles (id, user_id, role_id, assigned_at) VALUES
('ur-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'r-2222-2222-2222-22222222222222', NOW()),
('ur-2222-2222-2222-222222222222', '22222222-2222-2222-2222-222222222222', 'r-2222-2222-2222-22222222222222', NOW());

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
('mc-3333-3333-3333-333333333333', 'Trật khớp gối', 'Ca trật khớp gối do chấn thương thể thao', 'Thấp', 'c-1111-1111-1111-111111111111', true, true, NOW()),
('mc-4444-4444-4444-444444444444', 'Gãy xương chậu', 'Ca gãy xương chậu do ngã cao', 'Cao', 'c-3333-3333-3333-333333333333', false, true, NOW()),
('mc-5555-5555-5555-555555555555', 'Viêm khớp gối', 'Ca viêm khớp gối mãn tính', 'Trung bình', 'c-1111-1111-1111-111111111111', true, false, NOW());
```

### 1.6. Tạo AcademicClasses (Lớp học)

```sql
INSERT INTO academic_classes (id, class_name, semester, lecturer_id, created_at) VALUES
('ac-1111-1111-1111-111111111111', 'Xương Khớp A - 2024', '2024.1', '11111111-1111-1111-1111-111111111111', NOW()),
('ac-2222-2222-2222-222222222222', 'Chấn Thương B - 2024', '2024.1', '11111111-1111-1111-1111-111111111111', NOW());
```

### 1.7. Tạo ClassEnrollments (Đăng ký lớp)

```sql
INSERT INTO class_enrollments (id, class_id, student_id, enrolled_at) VALUES
('ce-1111-1111-1111-111111111111', 'ac-1111-1111-1111-111111111111', 'aaaaaaa1-1111-1111-1111-111111111111', NOW()),
('ce-2222-2222-2222-222222222222', 'ac-1111-1111-1111-111111111111', 'aaaaaaa2-2222-2222-2222-222222222222', NOW()),
('ce-3333-3333-3333-333333333333', 'ac-1111-1111-1111-111111111111', 'aaaaaaa3-3333-3333-3333-333333333333', NOW()),
('ce-4444-4444-4444-444444444444', 'ac-2222-2222-2222-222222222222', 'aaaaaaa1-1111-1111-1111-111111111111', NOW()),
('ce-5555-5555-5555-555555555555', 'ac-2222-2222-2222-222222222222', 'aaaaaaa4-4444-4444-4444-444444444444', NOW());
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

### 1.11. Tạo StudentQuestions (Câu hỏi của sinh viên)

```sql
INSERT INTO student_questions (id, student_id, case_id, question_text, language, created_at) VALUES
('sq-1111-1111-1111-111111111111', 'aaaaaaa1-1111-1111-1111-111111111111', 'mc-1111-1111-1111-111111111111', 'Tại sao xương đùi dễ gãy ở vị trí 1/3 giữa?', 'vi', NOW()),
('sq-2222-2222-2222-222222222222', 'aaaaaaa2-2222-2222-2222-222222222222', 'mc-2222-2222-2222-222222222222', 'Phương pháp điều trị thoái hóa cột sống hiệu quả nhất?', 'vi', NOW());
```

---

## 2. HƯỚNG DẪN TEST API BẰNG POSTMAN

### 2.1. Cấu hình Postman

```
Base URL: http://localhost:5046
Content-Type: application/json
```

### 2.2. CÁC BƯỚC TEST CHO LECTURER

---

#### Bước 1: Đăng ký/Đăng nhập tài khoản Lecturer

**POST** `http://localhost:5046/api/auths/register`

```json
{
    "fullName": "Nguyen Van A",
    "email": "lecturer1@edu.vn",
    "password": "Password123!",
    "role": "Lecturer"
}
```

Hoặc **POST** `http://localhost:5046/api/auths/login`

```json
{
    "email": "lecturer1@edu.vn",
    "password": "Password123!"
}
```

**Response mẫu:**
```json
{
    "success": true,
    "userId": "11111111-1111-1111-1111-111111111111",
    "email": "lecturer1@edu.vn",
    "fullName": "Nguyen Van A",
    "roles": ["Lecturer"],
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

> Lưu ý: Copy `token` từ response để sử dụng cho các request tiếp theo.

---

#### Bước 2: Cấu hình Authorization cho Lecturer

Trong Postman, chọn tab **Authorization**:
- Type: **Bearer Token**
- Token: Paste token từ bước đăng nhập

---

#### Bước 3: Tạo Lớp học (Create Class)

**POST** `http://localhost:5046/api/lecturers/classes`

```json
{
    "className": "Xương Khớp A - 2024",
    "semester": "2024.1"
}
```

**Expected Response (200 OK):**
```json
{
    "id": "ac-1111-1111-1111-111111111111",
    "className": "Xương Khớp A - 2024",
    "semester": "2024.1",
    "lecturerId": "11111111-1111-1111-1111-111111111111",
    "createdAt": "2024-01-15T10:30:00Z"
}
```

---

#### Bước 4: Xem danh sách lớp của Lecturer

**GET** `http://localhost:5046/api/lecturers/classes?lecturerId=11111111-1111-1111-1111-111111111111`

**Expected Response (200 OK):**
```json
[
    {
        "id": "ac-1111-1111-1111-111111111111",
        "className": "Xương Khớp A - 2024",
        "semester": "2024.1",
        "lecturerId": "11111111-1111-1111-1111-111111111111"
    }
]
```

---

#### Bước 5: Thêm sinh viên vào lớp (Enroll Student)

**POST** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/enroll`

```json
{
    "studentId": "aaaaaaa1-1111-1111-1111-111111111111"
}
```

**Expected Response (204 No Content)**

---

#### Bước 6: Thêm nhiều sinh viên cùng lúc (Enroll Many)

**POST** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/enrollmany`

```json
{
    "studentIds": [
        "aaaaaaa2-2222-2222-2222-222222222222",
        "aaaaaaa3-3333-3333-3333-333333333333"
    ]
}
```

**Expected Response (200 OK):**
```json
[
    {
        "studentId": "aaaaaaa2-2222-2222-2222-222222222222",
        "fullName": "Pham Van D",
        "email": "student2@edu.vn",
        "enrolledAt": "2024-01-15T10:35:00Z"
    },
    {
        "studentId": "aaaaaaa3-3333-3333-3333-333333333333",
        "fullName": "Hoang Van E",
        "email": "student3@edu.vn",
        "enrolledAt": "2024-01-15T10:35:00Z"
    }
]
```

---

#### Bước 7: Xem danh sách sinh viên trong lớp

**GET** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/students`

**Expected Response (200 OK):**
```json
[
    {
        "studentId": "aaaaaaa1-1111-1111-1111-111111111111",
        "fullName": "Le Thi C",
        "email": "student1@edu.vn",
        "enrolledAt": "2024-01-15T10:32:00Z"
    },
    {
        "studentId": "aaaaaaa2-2222-2222-2222-222222222222",
        "fullName": "Pham Van D",
        "email": "student2@edu.vn",
        "enrolledAt": "2024-01-15T10:35:00Z"
    }
]
```

---

#### Bước 8: Xem sinh viên chưa tham gia lớp (Available Students)

**GET** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/students/available`

**Expected Response (200 OK):**
```json
[
    {
        "studentId": "aaaaaaa4-4444-4444-4444-444444444444",
        "fullName": "Dao Thi F",
        "email": "student4@edu.vn"
    }
]
```

---

#### Bước 9: Xóa sinh viên khỏi lớp

**DELETE** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/students/aaaaaaa2-2222-2222-2222-222222222222`

**Expected Response (204 No Content)**

---

#### Bước 10: Tạo Thông báo (Create Announcement)

**POST** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/announcements`

```json
{
    "title": "Bài giảng tuần 1",
    "content": "Chào mừng các sinh viên đến với lớp Xương Khớp A"
}
```

**Expected Response (200 OK):**
```json
{
    "id": "an-1111-1111-1111-111111111111",
    "classId": "ac-1111-1111-1111-111111111111",
    "title": "Bài giảng tuần 1",
    "content": "Chào mừng các sinh viên đến với lớp Xương Khớp A",
    "createdAt": "2024-01-15T10:40:00Z"
}
```

---

#### Bước 11: Xem thông báo của lớp

**GET** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/announcements`

---

#### Bước 12: Tạo Quiz (Bài kiểm tra)

**POST** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/quizzes`

```json
{
    "title": "Kiểm tra Xương khớp",
    "description": "Bài kiểm tra chương 1",
    "timeLimitMinutes": 30
}
```

---

#### Bước 13: Thêm câu hỏi vào Quiz

**POST** `http://localhost:5046/api/lecturers/quizzes/qz-1111-1111-1111-111111111111/questions`

```json
{
    "caseId": "mc-1111-1111-1111-111111111111",
    "questionText": "Xương đùi gãy tại vị trí nào là phổ biến nhất?",
    "questionType": "multiple_choice",
    "correctAnswer": "1/3 giữa xương đùi",
    "options": ["1/3 gần xương đùi", "1/3 giữa xương đùi", "1/3 xa xương đùi", "Vùng trên lồi cầu"]
}
```

---

#### Bước 14: Xem danh sách câu hỏi trong Quiz

**GET** `http://localhost:5046/api/lecturers/quizzes/qz-1111-1111-1111-111111111111/questions`

---

#### Bước 15: Cập nhật câu hỏi

**PUT** `http://localhost:5046/api/lecturers/quizzes/questions/qq-1111-1111-1111-111111111111`

```json
{
    "questionText": "Xương đùi gãy tại vị trí nào là phổ biến nhất (đã chỉnh sửa)?",
    "options": ["1/3 gần xương đùi", "1/3 giữa xương đùi", "1/3 xa xương đùi", "Vùng trên lồi cầu", "Đầu xương đùi"]
}
```

**Expected Response (204 No Content)**

---

#### Bước 16: Xóa câu hỏi

**DELETE** `http://localhost:5046/api/lecturers/quizzes/questions/qq-1111-1111-1111-111111111111`

**Expected Response (204 No Content)**

---

#### Bước 17: Xem tất cả Cases

**GET** `http://localhost:5046/api/lecturers/cases`

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
        "isActive": true,
        "createdAt": "2024-01-10T08:00:00Z"
    }
]
```

---

#### Bước 18: Gán Cases cho Lớp

**POST** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/cases`

```json
{
    "caseIds": [
        "mc-1111-1111-1111-111111111111",
        "mc-2222-2222-2222-222222222222"
    ]
}
```

---

#### Bước 19: Duyệt Case (Approve Case)

**PUT** `http://localhost:5046/api/lecturers/cases/mc-4444-4444-4444-444444444444/approve`

```json
{
    "isApproved": true
}
```

**Expected Response (204 No Content)**

---

#### Bước 20: Xem câu hỏi của sinh viên trong lớp

**GET** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/questions`

**Expected Response (200 OK):**
```json
[
    {
        "id": "sq-1111-1111-1111-111111111111",
        "studentId": "aaaaaaa1-1111-1111-1111-111111111111",
        "studentName": "Le Thi C",
        "caseId": "mc-1111-1111-1111-111111111111",
        "caseTitle": "Gãy xương đùi",
        "questionText": "Tại sao xương đùi dễ gãy ở vị trí 1/3 giữa?",
        "createdAt": "2024-01-15T09:00:00Z"
    }
]
```

**Filter theo caseId:**
`?caseId=mc-1111-1111-1111-111111111111`

**Filter theo studentId:**
`?studentId=aaaaaaa1-1111-1111-1111-111111111111`

---

#### Bước 21: Xem Thống kê lớp học

**GET** `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/stats`

**Expected Response (200 OK):**
```json
{
    "totalStudents": 4,
    "activeStudents": 3,
    "totalCases": 2,
    "totalQuestions": 5,
    "pendingQuestions": 2,
    "avgQuizScore": 85.5
}
```

---

## 3. CÁC TRƯỜNG HỢP TEST LỖI

### 3.1. Test thêm sinh viên đã có trong lớp

**Request:** POST `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/enroll`

```json
{
    "studentId": "aaaaaaa1-1111-1111-1111-111111111111"
}
```

**Expected:** `409 Conflict`

```json
{
    "message": "Student đã có trong lớp này."
}
```

---

### 3.2. Test xóa sinh viên không tồn tại trong lớp

**Request:** DELETE `http://localhost:5046/api/lecturers/classes/ac-1111-1111-1111-111111111111/students/zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz`

**Expected:** `404 Not Found`

```json
{
    "message": "Student không tồn tại trong lớp này."
}
```

---

### 3.3. Test cập nhật case không tồn tại

**Request:** PUT `http://localhost:5046/api/lecturers/cases/zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz/approve`

```json
{
    "isApproved": true
}
```

**Expected:** `404 Not Found`

```json
{
    "message": "Case không tồn tại."
}
```

---

## 4. CREDENTIALS MẪU ĐỂ TEST

### Lecturer Accounts:
| Email | Password | Role |
|-------|----------|------|
| lecturer1@edu.vn | Password123! | Lecturer |
| lecturer2@edu.vn | Password123! | Lecturer |

---

## 5. MẸO TEST

1. **Luôn lưu token sau khi đăng nhập** - Sử dụng Postman Environment để lưu token và sử dụng lại cho các request khác.

2. **Sử dụng Swagger UI** - Truy cập http://localhost:5046/swagger để xem và test trực tiếp các API endpoints.

3. **Kiểm tra database** - Nếu có quyền truy cập Supabase dashboard, kiểm tra dữ liệu đã được tạo đúng chưa.

4. **Test theo thứ tự** - Nên test theo thứ tự các bước đã liệt kê ở trên vì có một số API phụ thuộc vào dữ liệu từ API trước.

5. **Dọn dẹp dữ liệu test** - Sau khi test xong, nên xóa các dữ liệu test để tránh ảnh hưởng đến các lần test sau.
