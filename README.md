# BoneVisQA Backend API

## Mục lục

- [Giới thiệu](#giới-thiệu)
- [Công nghệ sử dụng](#công-nghệ-sử-dụng)
- [Cấu trúc Project](#cấu-trúc-project)
- [Kiến trúc hệ thống](#kiến-trúc-hệ-thống)
- [Tính năng chính](#tính-năng-chính)
- [Cài đặt & Chạy](#cài-đặt--chạy)
- [API Endpoints](#api-endpoints)
- [Cấu hình](#cấu-hình)
- [Docker Deployment](#docker-deployment)
- [Bảo mật](#bảo-mật)

---

## Giới thiệu

**BoneVisQA** là hệ thống Interactive VQA (Visual Question Answering) dành cho giáo dục y khoa về bệnh lý xương. Hệ thống cho phép sinh viên y khoa tương tác với hình ảnh y khoa (X-quang, MRI, CT scan) thông qua câu hỏi-trả lời được hỗ trợ bởi AI.

Backend được xây dựng trên **.NET 8.0** với kiến trúc phân lớp (layered architecture) sử dụng **Clean Architecture**.

---

## Công nghệ sử dụng

### Core Technologies
| Technology | Version | Mô tả |
|------------|---------|--------|
| .NET | 8.0 | Runtime và SDK |
| ASP.NET Core | 8.0 | Web API Framework |
| Entity Framework Core | 8.0.5 | ORM |

### Databases & Storage
| Technology | Mô tả |
|------------|--------|
| PostgreSQL | Database chính |
| pgvector | Vector database cho semantic search |
| Supabase | File storage (hình ảnh, PDF) |

### AI/ML Services
| Service | Mô tả |
|---------|--------|
| Google Gemini | Visual QA (multimodal AI) |
| HuggingFace | Sentence embeddings |

### Authentication & Security
| Technology | Mô tả |
|------------|--------|
| JWT | Token-based authentication |
| Google OAuth 2.0 | Social login |
| bcrypt | Password hashing |

### Other Dependencies
| Package | Mô tả |
|---------|--------|
| Swashbuckle | Swagger/OpenAPI documentation |
| SignalR | Real-time notifications |
| SendGrid/Gmail SMTP | Email services |
| SixLabors.ImageSharp | Image processing |
| UglyToad.PdfPig | PDF text extraction |
| Polly | HTTP resilience policies |

---

## Cấu trúc Project

```
BoneVisQA/
└── SP26SE110_BoneVisQA/
    ├── BoneVisQA.API/              # Presentation Layer
    │   ├── Controllers/            # API Controllers
    │   ├── Middleware/             # Custom middleware
    │   ├── ExceptionHandling/       # Global exception handler
    │   ├── Hubs/                   # SignalR hubs
    │   ├── Policies/               # HTTP policies
    │   ├── Services/               # API-level services
    │   ├── Program.cs              # Application entry point
    │   └── Dockerfile
    │
    ├── BoneVisQA.Services/          # Business Logic Layer
    │   ├── Interfaces/             # Service interfaces
    │   │   ├── Admin/
    │   │   ├── Expert/
    │   │   └── ...
    │   ├── Models/                 # DTOs
    │   │   ├── Admin/
    │   │   ├── Expert/
    │   │   ├── Lecturer/
    │   │   └── Student/
    │   └── Services/               # Service implementations
    │       ├── Admin/
    │       ├── Analytics/
    │       ├── Auth/
    │       ├── AiQuizServices/
    │       ├── DocumentUpload/
    │       ├── Expert/
    │       ├── Lecturer/
    │       ├── QuizExtensions/
    │       ├── Rag/
    │       ├── Storage/
    │       └── Student/
    │
    └── BoneVisQA.Repositories/      # Data Access Layer
        ├── DBContext/               # EF Core DbContext
        ├── Interfaces/              # Repository interfaces
        ├── Models/                  # Entity models
        ├── Services/                # Repository implementations
        ├── UnitOfWork/              # Unit of Work pattern
        └── Migrations/              # EF Core migrations
```

---

## Kiến trúc hệ thống

### Layer Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                        │
│                 (BoneVisQA.API - Controllers)                │
│  - API Routes                                               │
│  - Request/Response DTOs                                   │
│  - Authentication/Authorization                            │
│  - Rate Limiting                                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   BUSINESS LOGIC LAYER                       │
│                  (BoneVisQA.Services)                        │
│  - Business Rules                                           │
│  - AI Integration (Gemini, HuggingFace)                     │
│  - Quiz Generation & Management                             │
│  - Document Processing (RAG)                               │
│  - Analytics & Statistics                                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    DATA ACCESS LAYER                         │
│                (BoneVisQA.Repositories)                       │
│  - Entity Framework Core                                   │
│  - Repository Pattern                                       │
│  - Unit of Work                                             │
│  - PostgreSQL + pgvector                                    │
└─────────────────────────────────────────────────────────────┘
```

### Multi-Role Architecture

```
                    ┌─────────────┐
                    │   Client    │
                    │  (Frontend) │
                    └──────┬──────┘
                           │ JWT Token
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    BoneVisQA.API                             │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐         │
│  │  Admin  │ │Lecturer │ │  Expert │ │ Student │         │
│  │Controller│ │Controller│ │Controller│ │Controller│       │
│  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘         │
└───────┼────────────┼────────────┼────────────┼─────────────┘
        │            │            │            │
        ▼            ▼            ▼            ▼
┌─────────────────────────────────────────────────────────────┐
│                    Service Layer                             │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐         │
│  │  Admin  │ │Lecturer │ │  Expert │ │ Student │         │
│  │ Service │ │ Service │ │ Service │ │ Service │         │
│  └────┬────┘ └────┬────┘ └────┬────┘ └────┬────┘         │
└───────┼────────────┼────────────┼────────────┼─────────────┘
        │            │            │            │
        └────────────┴─────┬──────┴────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                  Repository Layer                           │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              BoneVisQADbContext                      │    │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────┐               │    │
│  │  │PostgreSQL│ │ pgvector│ │Supabase │               │    │
│  │  │         │ │(Vectors) │ │(Files)  │               │    │
│  │  └─────────┘ └─────────┘ └─────────┘               │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## Tính năng chính

### 1. Xác thực & Ủy quyền (Authentication & Authorization)

- **JWT Token** - Xác thực stateless
- **Google OAuth 2.0** - Đăng nhập bằng Google
- **Role-based Access Control** - 4 vai trò: Admin, Lecturer, Expert, Student
- **Password Reset** - Quên mật khẩu qua email

### 2. Visual Question Answering (VQA)

- **AI-powered Analysis** - Sử dụng Google Gemini cho multimodal AI
- **Image Upload** - Hỗ trợ upload hình ảnh y khoa
- **Session Management** - Quản lý phiên hỏi đáp
- **Rate Limiting** - Giới hạn 10 requests/phút cho AI interactions
- **Citation Generation** - Tạo trích dẫn từ tài liệu

### 3. Quiz System

- **AI Quiz Generation** - Tạo quiz tự động từ nội dung
- **Spaced Repetition** - Học lặp lại ngắt quãng
- **Adaptive Learning** - Điều chỉnh độ khó theo năng lực
- **Quiz Review** - Ôn tập sau khi làm bài

### 4. Quản lý Lớp học (Class Management)

- **CRUD Operations** - Tạo, đọc, cập nhật, xóa lớp
- **Student Enrollment** - Đăng ký sinh viên vào lớp
- **Lecturer Assignment** - Phân công giảng viên
- **Expert Assignment** - Phân công chuyên gia

### 5. Document Processing (RAG)

- **PDF Upload** - Tải lên tài liệu PDF
- **Text Extraction** - Trích xuất text từ PDF
- **Chunking** - Chia nhỏ tài liệu
- **Vector Embedding** - Tạo vector embeddings
- **Semantic Search** - Tìm kiếm ngữ nghĩa

### 6. Medical Case Management

- **Case Library** - Thư viện ca bệnh
- **Image Annotations** - Chú thích hình ảnh
- **Expert Review** - Chuyên gia phê duyệt
- **Classification** - Phân loại bệnh lý

### 7. Real-time Features

- **SignalR Notifications** - Thông báo real-time
- **Progress Tracking** - Theo dõi tiến độ học tập
- **Live Updates** - Cập nhật trực tiếp

### 8. Analytics & Reporting

- **Learning Analytics** - Phân tích học tập
- **Class Statistics** - Thống kê lớp học
- **Student Progress** - Tiến độ sinh viên
- **Export Reports** - Xuất báo cáo Excel

---

API sẽ chạy tại:
- **Development**: `https://localhost:5047`
- **Swagger UI**: `https://localhost:5047/swagger`

---

## API Endpoints

### Authentication

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| POST | `/api/auths/register` | Đăng ký tài khoản mới |
| POST | `/api/auths/login` | Đăng nhập |
| POST | `/api/auths/forgot-password` | Quên mật khẩu |
| POST | `/api/auths/reset-password` | Đặt lại mật khẩu |
| POST | `/api/auths/logout` | Đăng xuất |

### Admin

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/admin/class-dashboard` | Dashboard quản lý lớp |
| GET | `/api/admin/class-dashboard/summary` | Tổng hợp thống kê |
| POST | `/api/admin/class-dashboard` | Tạo lớp mới |
| PUT | `/api/admin/class-dashboard` | Cập nhật lớp |
| DELETE | `/api/admin/class-dashboard/{id}` | Xóa lớp |
| POST | `/api/admin/class-dashboard/{id}/assign-lecturer` | Gán giảng viên |
| POST | `/api/admin/class-dashboard/{id}/assign-expert` | Gán chuyên gia |
| GET | `/api/admin/users` | Quản lý người dùng |
| GET | `/api/admin/documents` | Quản lý tài liệu |
| GET | `/api/admin/reports` | Báo cáo hệ thống |

### Lecturer

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/lecturers/classes` | Danh sách lớp |
| POST | `/api/lecturers/classes` | Tạo lớp mới |
| POST | `/api/lecturers/classes/{id}/enroll` | Thêm sinh viên |
| POST | `/api/lecturers/classes/{id}/enrollmany` | Thêm nhiều sinh viên |
| DELETE | `/api/lecturers/classes/{id}/students/{studentId}` | Xóa sinh viên |
| GET | `/api/lecturers/classes/{id}/students` | DS sinh viên |
| POST | `/api/lecturers/classes/{id}/announcements` | Tạo thông báo |
| GET | `/api/lecturers/classes/{id}/stats` | Thống kê lớp |
| POST | `/api/lecturers/quizzes/{id}/questions` | Thêm câu hỏi |
| GET | `/api/lecturers/dashboard` | Dashboard giảng viên |
| GET | `/api/lecturers/reports` | Báo cáo |
| GET | `/api/lecturers/gradebook` | Bảng điểm |
| GET | `/api/lecturers/notifications` | Thông báo |
| POST | `/api/lecturers/assignments` | Phân công Expert |

### Expert

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/expert/dashboard` | Dashboard chuyên gia |
| GET | `/api/expert/reviews` | Danh sách phê duyệt |
| PUT | `/api/expert/reviews/{id}` | Phê duyệt case |
| GET | `/api/expert/teaching-objectives` | Mục tiêu giảng dạy |
| POST | `/api/expert/teaching-objectives` | Tạo mục tiêu |
| GET | `/api/expert/specialties` | Chuyên môn |
| POST | `/api/expert/quizs` | Quản lý quiz |

### Student

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/students/profile` | Hồ sơ cá nhân |
| GET | `/api/students/classes` | Lớp học đã tham gia |
| GET | `/api/students/cases` | Ca bệnh được giao |
| POST | `/api/student/visual-qa/ask` | Hỏi đáp với hình ảnh |
| GET | `/api/students/quizzes` | Danh sách quiz |
| POST | `/api/students/quizzes/{id}/submit` | Nộp bài quiz |
| GET | `/api/students/progress` | Tiến độ học tập |
| GET | `/api/students/questions` | Câu hỏi đã hỏi |
| GET | `/api/students/announcements` | Thông báo |
| GET | `/api/students/learning` | Học tập cá nhân |

### Visual QA

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| POST | `/api/student/visual-qa/ask` | Hỏi AI về hình ảnh |
| GET | `/api/student/visual-qa/sessions` | Lịch sử phiên |
| GET | `/api/student/visual-qa/sessions/{id}` | Chi tiết phiên |
| DELETE | `/api/student/visual-qa/sessions/{id}` | Xóa phiên |

### Case Management

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/cases` | Danh sách ca bệnh |
| GET | `/api/cases/{id}` | Chi tiết ca bệnh |
| POST | `/api/cases` | Tạo ca bệnh mới |
| PUT | `/api/cases/{id}` | Cập nhật ca bệnh |
| DELETE | `/api/cases/{id}` | Xóa ca bệnh |
| GET | `/api/cases/{id}/annotations` | Chú thích |

### Quiz

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/quiz-extensions/review/{quizId}` | Ôn tập quiz |
| GET | `/api/quiz-extensions/spaced-repetition` | Học lặp lại |
| GET | `/api/quiz-extensions/adaptive/{quizId}` | Quiz thích nghi |

### Analytics

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/analytics/overview` | Tổng quan |
| GET | `/api/analytics/student/{id}` | Phân tích sinh viên |
| GET | `/api/analytics/class/{id}` | Phân tích lớp |
| GET | `/api/lecturer-analytics/dashboard` | Dashboard giảng viên |

### Other

| Method | Endpoint | Mô tả |
|--------|----------|--------|
| GET | `/api/search` | Tìm kiếm |
| GET | `/api/notifications` | Thông báo |
| GET | `/api/profile` | Hồ sơ người dùng |
| PUT | `/api/profile` | Cập nhật hồ sơ |
| GET | `/api/common/classifications` | Phân loại chung |
| GET | `/api/qa` | Q&A system |

---

## Cấu hình



### Environment Variables

| Variable | Mô tả |
|----------|--------|
| `ASPNETCORE_ENVIRONMENT` | Development/Production |
| `ASPNETCORE_URLS` | URLs để bind |
| `ConnectionStrings__SupabaseDb` | Database connection |
| `Jwt__Key` | JWT secret key |
| `Gemini__ApiKeys__0` | Gemini API key |
| `HuggingFace__ApiKey` | HuggingFace API key |

---





## Bảo mật

### Authentication Flow

```
┌──────────┐    1. Login     ┌──────────┐    2. Validate    ┌──────────┐
│  Client  │ ──────────────► │   API    │ ────────────────► │  Database│
└──────────┘                 └──────────┘                  └──────────┘
                                  │
                                  │ 3. Generate JWT
                                  ▼
                             ┌──────────┐
                             │   JWT    │
                             │  Token   │
                             └────┬─────┘
                                  │
                                  │ 4. Return token
                                  ▼
                             ┌──────────┐
                             │  Client  │
                             └──────────┘
                                  │
                                  │ 5. Use token in Authorization header
                                  ▼
                             ┌──────────┐
                             │Protected │
                             │ Endpoints│
                             └──────────┘
```

### Rate Limiting

- **AI Interactions**: 10 requests/phút/user
- **General API**: No strict limit (configurable)
- **File Upload**: 100MB max per file

### Security Headers

- CORS configured for allowed origins
- HTTPS redirection in production
- JWT token validation on every protected request

---

## API Documentation

Swagger UI available at `/swagger` endpoint when running in Development mode.

### OpenAPI Spec

```yaml
openapi: 3.0.0
info:
  title: BoneVisQA API
  version: v1
  description: Interactive VQA System for Medical Education
```

---

## License

Copyright © 2026 SP26SE110 - BoneVisQA Project
