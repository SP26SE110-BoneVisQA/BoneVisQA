## Lecturer API Notes

### 1. Chạy API & đăng nhập cơ bản

- Chạy backend: `dotnet run --project BoneVisQA.API`
- Mở Swagger: truy cập `https://localhost:5001/swagger` (hoặc URL console in ra).

#### Đăng ký Lecturer

- `POST /api/Auths/register`
  ```json
  {
    "fullName": "Dr. Lecturer",
    "email": "lecturer@example.com",
    "password": "P@ssw0rd!",
    "schoolCohort": "Faculty of Medicine",
    "roleName": "Lecturer"
  }
  ```

#### Đăng nhập Lecturer

- `POST /api/Auths/login`
  ```json
  {
    "email": "lecturer@example.com",
    "password": "P@ssw0rd!"
  }
  ```
- Lấy `token` trong response, dùng cho các request sau:
  - Header: `Authorization: Bearer <token>`

---

### 2. Chức năng chính cho Lecturer (LecturerController)

#### 2.1. Tạo lớp học

- `POST /api/Lecturer/classes`
  ```json
  {
    "className": "Orthopedics - Class A 2026",
    "semester": "Spring 2026",
    "lecturerId": "GUID-LECTURER-ID"
  }
  ```
- Kết quả: `ClassDto` (`id`, `className`, `semester`, `lecturerId`, `createdAt`).

#### 2.2. Xem danh sách lớp của giảng viên

- `GET /api/Lecturer/classes?lecturerId={GUID-LECTURER-ID}`
- Kết quả: danh sách `ClassDto`.

#### 2.3. Ghi danh một sinh viên vào lớp

- `POST /api/Lecturer/classes/{classId}/enroll`
  ```json
  {
    "studentId": "GUID-STUDENT-ID"
  }
  ```
- Kết quả:
  - `204 No Content` nếu thành công.
  - `409 Conflict` nếu sinh viên đã tồn tại trong lớp.

#### 2.4. Ghi danh nhiều sinh viên cùng lúc

- `POST /api/Lecturer/classes/{classId}/enrollmany`
  ```json
  {
    "studentIds": [
      "GUID-STUDENT-ID-1",
      "GUID-STUDENT-ID-2",
      "GUID-STUDENT-ID-3"
    ]
  }
  ```
- Kết quả: danh sách `StudentEnrollmentDto` của các sinh viên trong lớp sau khi thêm.

#### 2.5. Xóa sinh viên khỏi lớp

- `DELETE /api/Lecturer/classes/{classId}/students/{studentId}`
- Kết quả:
  - `204 No Content` nếu thành công.
  - `404 Not Found` nếu sinh viên không tồn tại trong lớp.

#### 2.6. Xem danh sách sinh viên trong lớp

- `GET /api/Lecturer/classes/{classId}/students`
- Kết quả: danh sách `StudentEnrollmentDto`:
  ```json
  [
    {
      "enrollmentId": "GUID-ENROLLMENT-ID",
      "studentId": "GUID-STUDENT-ID",
      "studentName": "Nguyen Van A",
      "studentEmail": "student1@example.com",
      "studentCode": "SE171589",
      "enrolledAt": "2026-03-01T10:00:00Z"
    }
  ]
  ```

#### 2.7. Xem danh sách sinh viên chưa được ghi danh

- `GET /api/Lecturer/classes/{classId}/students/available`
- Kết quả: danh sách `StudentEnrollmentDto` các sinh viên chưa thuộc lớp này.

#### 2.8. Tạo thông báo cho lớp

- `POST /api/Lecturer/classes/{classId}/announcements`
  ```json
  {
    "title": "New bone fracture cases",
    "content": "Please review the new fracture cases before next week."
  }
  ```
- Kết quả: `AnnouncementDto` (`id`, `classId`, `title`, `content`, `createdAt`).

#### 2.9. Tạo quiz cho lớp

- `POST /api/Lecturer/classes/{classId}/quizzes`
  ```json
  {
    "title": "Long bone fractures quiz",
    "openTime": "2026-03-01T00:00:00Z",
    "closeTime": "2026-03-10T23:59:59Z",
    "timeLimit": 900,
    "passingScore": 60
  }
  ```
- Kết quả: `QuizDto` (`id`, `classId`, `title`, `openTime`, `closeTime`, `timeLimit`, `passingScore`).

#### 2.10. Xem thống kê học tập của lớp

- `GET /api/Lecturer/classes/{classId}/stats`
- Kết quả: `ClassStatsDto`:
  ```json
  {
    "classId": "GUID-CLASS-ID",
    "totalStudents": 30,
    "totalCasesViewed": 120,
    "totalQuestionsAsked": 45,
    "avgQuizScore": 72.5
  }
  ```

---

### 3. Flow mẫu cho Lecturer

1. Đăng ký tài khoản Lecturer → đăng nhập lấy JWT.
2. Dùng JWT gọi:
   - Tạo lớp (`/api/Lecturer/classes`).
   - Xem danh sách sinh viên chưa ghi danh (`/api/Lecturer/classes/{classId}/students/available`).
   - Ghi danh sinh viên (`/api/Lecturer/classes/{classId}/enroll` hoặc `/enrollmany`).
   - Xem danh sách sinh viên trong lớp (`/api/Lecturer/classes/{classId}/students`).
   - Xóa sinh viên khỏi lớp nếu cần (`DELETE .../students/{studentId}`).
   - Tạo quiz cho lớp (`/api/Lecturer/classes/{classId}/quizzes`).
   - Gửi thông báo cho lớp (`/api/Lecturer/classes/{classId}/announcements`).
3. Theo dõi kết quả học tập qua:
   - `/api/Lecturer/classes/{classId}/stats`.

