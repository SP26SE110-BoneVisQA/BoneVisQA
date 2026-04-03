# Quiz Time Gate — 3 Phương Án Xử Lý

## Tổng quan

Database có 2 bảng cùng có field thời gian:

| Bảng | Field | Ý nghĩa |
|------|-------|---------|
| `quizzes` | `open_time`, `close_time`, `time_limit`, `passing_score` | Quiz tồn tại (global/toàn cục) |
| `class_quiz_sessions` | `open_time`, `close_time`, `time_limit_minutes`, `passing_score` | Quiz **trong lớp X** có thể làm khi nào |

**Hiện tại (sau khi sửa):** Dùng **Phương án 1** — `class_quiz_sessions` là GATE duy nhất.

---

## 3 Phương án

### Phương án 1: Chỉ `class_quiz_sessions` làm GATE (ĐANG DÙNG ✅)

**Logic:** Student làm được quiz khi `session.open <= now <= session.close`

**Ví dụ:**
```
Expert tạo Quiz "Toán 1", open=2026-04-01, close=2026-04-30 (quizzes) ← BỎ QUA
Lecturer gán Quiz "Toán 1" vào Lớp A, open=2026-04-10, close=2026-04-20 (sessions)
Lecturer gán Quiz "Toán 1" vào Lớp B, open=2026-04-05, close=2026-04-25 (sessions)

→ Student lớp A: chỉ làm được 10/04 → 20/04
→ Student lớp B: chỉ làm được 05/04 → 25/04
```

**Ưu điểm:**
- Mỗi lớp có thời gian riêng cho cùng 1 quiz
- Đơn giản, dễ hiểu
- `QuizListItemDto` trả về `ClassId`, `ClassName`, thời gian từ `class_quiz_sessions`

**Nhược điểm:**
- Muốn quiz có thời gian mặc định → lecturer phải tự nhập

**File cần sửa nếu đổi sang phương án khác:**
- `StudentService.GetAvailableQuizzesAsync` — map thời gian từ `class_quiz_sessions`
- `QuizListItemDto` — có `ClassId`, `ClassName`
- `LecturerService.AssignQuizToClassAsync` — KHÔNG copy thời gian từ `quizzes` (đã fix)

---

### Phương án 2: Chỉ `quizzes` làm GATE

**Logic:** Student làm được quiz khi `quiz.open_time <= now <= quiz.close_time`

**Ví dụ:**
```
Expert tạo Quiz "Toán 1", open=2026-04-10, close=2026-04-20 (quizzes)
Lecturer gán Quiz "Toán 1" vào Lớp A (sessions - chỉ gán, không đặt thời gian riêng)
Lecturer gán Quiz "Toán 1" vào Lớp B (sessions - chỉ gán, không đặt thời gian riêng)

→ Tất cả student: chỉ làm được 10/04 → 20/04 (theo quiz gốc)
→ Field open/close trên sessions: BỎ QUA (set NULL)
```

**Ưu điểm:**
- Thời gian quiz toàn cục, không phân biệt lớp
- Expert kiểm soát hoàn toàn thời gian

**Nhược điểm:**
- Lecturer không thể "thu hẹp" cửa sổ thời gian cho từng lớp
- Nếu muốn Lớp A mở 1 tiếng, Lớp B mở 2 tiếng → KHÔNG LÀM ĐƯỢC

**File cần sửa nếu đổi sang phương án 2:**
- `StudentRepository.GetQuizzesWithSessionForStudentAsync` — query từ `quizzes`, filter `quizzes.open_time <= now <= quizzes.close_time`
- `QuizListItemDto` — BỎ `ClassId`, `ClassName` (hoặc vẫn giữ nếu cần biết quiz thuộc lớp nào)
- `GetAvailableQuizzesAsync` — map `TimeLimit`, `PassingScore` từ `quiz` thay vì `session`

---

### Phương án 3: Cả 2 bảng (AND — BẮT BUỘC thỏa cả 2)

**Logic:** Student làm được quiz khi thỏa **ĐỒNG THỜI** cả 2:
- `quizzes.open_time <= now <= quizzes.close_time`
- `sessions.open_time <= now <= sessions.close_time`

**Ví dụ:**
```
Expert tạo Quiz "Toán 1", open=2026-04-01, close=2026-04-30 (quizzes)
Lecturer gán Quiz "Toán 1" vào Lớp A, open=2026-04-10, close=2026-04-20 (sessions)

→ Student lớp A: phải thỏa CẢ 2 điều kiện:
    ✓ quizzes.open <= now <= quizzes.close  (01/04 -> 30/04) → luôn thỏa
    ✓ sessions.open <= now <= sessions.close (10/04 -> 20/04) → chỉ thỏa trong khoảng này
→ Kết quả: chỉ làm được 10/04 -> 20/04 (nhỏ hơn của sessions)
```

**Ưu điểm:**
- Kiểm soát chặt chẽ nhất — cả expert và lecturer đều kiểm soát thời gian

**Nhược điểm:**
- Phức tạp hơn
- Nếu expert đặt close=30/04, lecturer đặt close=20/04 → kết quả là 20/04 (hẹp hơn luôn thắng)
- Nếu expert đặt close=10/04, lecturer đặt close=20/04 → KHÔNG LÀM ĐƯỢC (expert thu hẹp quá)
- Dễ conflict nếu expert và lecturer không phối hợp

**File cần sửa nếu đổi sang phương án 3:**
- `StudentRepository.GetQuizzesWithSessionForStudentAsync` — filter CẢ 2 bảng
- `QuizListItemDto` — vẫn có thể hiển thị cả 2 bộ thời gian (`GlobalOpenTime`, `GlobalCloseTime`, `ClassOpenTime`, `ClassCloseTime`)
- `GetAvailableQuizzesAsync` — map cả 2 bộ thời gian

---

## Bảng so sánh

| Tiêu chí | Phương án 1 | Phương án 2 | Phương án 3 |
|-----------|:---:|:---:|:---:|
| Độ phức tạp code | ⭐⭐ | ⭐ | ⭐⭐⭐ |
| Linh hoạt (mỗi lớp thời gian khác nhau) | ✅ | ❌ | ✅ |
| Dễ debug | ✅ | ✅ | ❌ |
| Expert kiểm soát | ❌ | ✅ | ✅ |
| Lecturer kiểm soát | ✅ | ❌ | ✅ |
| Khuyến nghị | **✅ YES** | ❌ No | ⚠️ Cân nhắc |

---

## File đã sửa theo Phương án 1

| File | Thay đổi |
|------|---------|
| `QuizListItemDto` | Thêm `ClassId`, `ClassName` |
| `QuizWithSessionDto` | Tạo mới — DTO chứa quiz + session + class |
| `IStudentRepository` | Thêm `GetQuizzesWithSessionForStudentAsync` |
| `StudentRepository` | Implement query từ `class_quiz_sessions` |
| `StudentService.GetAvailableQuizzesAsync` | Map từ session data |
| `LecturerService.AssignQuizToClassAsync` | KHÔNG copy thời gian từ `quizzes` |

---

## Cách chuyển đổi giữa các phương án

### Chuyển sang Phương án 2:
1. Sửa `GetQuizzesWithSessionForStudentAsync` → query từ `quizzes`, filter `quizzes.open_time <= utcNow <= quizzes.close_time`
2. Bỏ field `ClassId`, `ClassName` khỏi `QuizListItemDto` (hoặc giữ nếu cần)
3. Sửa `GetAvailableQuizzesAsync` → map `TimeLimit`, `PassingScore` từ `quiz`
4. Sửa `LecturerService.AssignQuizToClassAsync` → copy thời gian từ `quizzes`

### Chuyển sang Phương án 3:
1. Sửa `GetQuizzesWithSessionForStudentAsync` → filter CẢ 2: `quizzes.open_time <= utcNow <= quizzes.close_time` AND `sessions.open_time <= utcNow <= sessions.close_time`
2. Mở rộng `QuizListItemDto` → thêm `GlobalOpenTime`, `GlobalCloseTime` từ `quizzes`
3. Sửa `GetAvailableQuizzesAsync` → map cả 2 bộ thời gian
4. Sửa `LecturerService.AssignQuizToClassAsync` → KHÔNG cần sửa (vẫn copy thời gian từ quizzes làm mặc định)
