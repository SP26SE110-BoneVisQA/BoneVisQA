# VisualQA Functionality - Lecture Notes

## Overview
Tính năng VisualQA (Visual Question Answering) cho phép sinh viên đặt câu hỏi về hình ảnh y khoa và nhận câu trả lời tự động từ hệ thống AI.

## Architecture

### 1. Interface - IStudentService
Hai phương thức mới được định nghĩa trong interface:

```csharp
// Tạo câu hỏi VisualQA
Task<StudentQuestionDto> CreateVisualQAQuestionAsync(Guid studentId, VisualQARequestDto request);

// Lưu câu trả lời VisualQA  
Task SaveVisualQAAnswerAsync(Guid questionId, VisualQAResponseDto response);
```

### 2. DTOs

**VisualQARequestDto** - Request từ client:
- `StudentId`: ID của sinh viên
- `QuestionText`: Nội dung câu hỏi
- `ImageUrl`: URL hình ảnh (tùy chọn)
- `Coordinates`: Tọa độ vùng quan tâm trên hình ảnh (tùy chọn)
- `CaseId`: ID case y khoa (tùy chọn)
- `AnnotationId`: ID annotation (tùy chọn)
- `Language`: Ngôn ngữ (vi/en)

**VisualQAResponseDto** - Response từ AI:
- `AnswerText`: Câu trả lời văn bản
- `SuggestedDiagnosis`: Chẩn đoán đề xuất
- `DifferentialDiagnoses`: Các chẩn đoán phân biệt
- `Citations`: Danh sách trích dẫn tham khảo

### 3. Repository Layer

**IStudentRepository** - Thêm 2 phương thức:
```csharp
Task<CaseAnswer> CreateCaseAnswerAsync(CaseAnswer answer);
Task AddCitationsAsync(IEnumerable<Citation> citations);
```

### 4. Service Implementation - StudentService

**CreateVisualQAQuestionAsync:**
1. Nhận request từ client
2. Normalize ngôn ngữ (vi/en)
3. Tạo entity `StudentQuestion` mới
4. Lưu vào database qua repository
5. Trả về `StudentQuestionDto`

**SaveVisualQAAnswerAsync:**
1. Nhận questionId và response từ AI
2. Tạo entity `CaseAnswer` với:
   - AnswerText: câu trả lời
   - StructuredDiagnosis: chẩn đoán đề xuất
   - DifferentialDiagnoses: chẩn đoán phân biệt
   - Status: "answered"
3. Lưu answer vào database
4. Nếu có citations, tạo và lưu các entity `Citation`

## Database Entities Used

### StudentQuestion
- Lưu trữ câu hỏi của sinh viên
- Liên kết với Student (sinh viên), MedicalCase (case y khoa), CaseAnnotation

### CaseAnswer  
- Lưu trữ câu trả lời cho từng câu hỏi
- Liên kết với StudentQuestion
- Chứa: answer_text, structured_diagnosis, differential_diagnoses

### Citation
- Lưu trữ các trích dẫn tham khảo
- Liên kết với CaseAnswer và DocumentChunk
- Chứa: chunk_id, similarity_score

## Flow hoạt động

```
┌─────────────┐     VisualQARequestDto      ┌─────────────────┐
│   Client    │ ──────────────────────────► │ StudentService  │
└─────────────┘                              └────────┬────────┘
                                                      │
                                                      ▼
                                              ┌─────────────────┐
                                              │ StudentQuestion │
                                              │    (created)    │
                                              └────────┬────────┘
                                                       │
                                    AI Processing       │
                                                       ▼
                                              ┌─────────────────┐
                                              │  SaveVisualQA   │
                                              │    AnswerAsync  │
                                              └────────┬────────┘
                                                       │
                        ┌──────────────────────────────┼──────────────────────┐
                        ▼                              ▼                      ▼
               ┌─────────────────┐         ┌─────────────────┐    ┌─────────────────┐
               │   CaseAnswer    │         │     Citation   │    │    Citation     │
               │   (answer +     │         │    (chunk 1)    │    │    (chunk 2)    │
               │   diagnosis)    │         └─────────────────┘    └─────────────────┘
               └─────────────────┘
```

## Summary
Tính năng VisualQA mở rộng từ hệ thống Q&A hiện tại, cho phép sinh viên:
- Đặt câu hỏi về hình ảnh y khoa cụ thể
- Nhận câu trả lời tự động với chẩn đoán đề xuất
- Xem các chẩn đoán phân biệt
- Tham khảo các tài liệu nguồn (citations)
