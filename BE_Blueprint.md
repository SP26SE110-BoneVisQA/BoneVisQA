# BoneVisQA — API & Logic Blueprint (Core Flows)

Authoritative reference for five critical flows: controllers, DTO contracts, service classes, and PostgreSQL tables. Routes assume API base URL prefix `api`.

**JSON:** API responses/requests use the default ASP.NET Core **camelCase** property names unless a DTO uses `[JsonPropertyName]` (e.g. `answerId` / `imageUrl` on lecturer triage DTOs).

---

## 1. Admin upload document (knowledge base → chunking → embeddings)

### API endpoint

| Method | Route | Auth | Notes |
|--------|-------|------|--------|
| `POST` | `/api/admin/documents/upload` | `Admin` | `multipart/form-data`; single `File` **or** multiple `Files`; max **100MB** per file; **PDF only**. |

**Orchestration:** `AdminDocumentsController.Upload` → if batch (`Files` or multiple files) → `IDocumentProcessingService.UploadDocumentsAsync`; else → `IDocumentService.UploadDocumentAsync`.

**Related (same controller, not the primary “upload” flow):** `POST /api/admin/documents/{id}/reindex` triggers `DocumentService.TriggerReindexAsync` (re-runs ingestion).

### Core DTOs (FE / multipart)

**Request — `DocumentUploadRequest`** (`BoneVisQA.API/Controllers/Admin/AdminDocumentsController.cs`)

| Field | Type | Notes |
|-------|------|--------|
| `File` | `IFormFile?` | Legacy single PDF |
| `Files` | `List<IFormFile>?` | Batch PDFs |
| `Title` | `string` | Batch: combined with filename when multiple |
| `CategoryId` | `Guid?` | FK to categories |
| `TagIds` | `List<Guid>` | Many-to-many via `document_tags` |

**Internal metadata — `DocumentUploadDto`** (`BoneVisQA.Services/Interfaces/IDocumentService.cs`)

| Field | Type |
|-------|------|
| `Title` | `string` |
| `CategoryId` | `Guid?` |
| `TagIds` | `List<Guid>` |

**Response — single file**

- `201 Created` + `DocumentDto` (`Location` from `GetById`).

**`DocumentDto`**

| Field | Type |
|-------|------|
| `Id` | `Guid` |
| `Title` | `string` |
| `FilePath` | `string?` (Supabase public URL) |
| `CategoryId` | `Guid?` |
| `IndexingStatus` | `string` (API-normalized: `Processing` / `Completed` / `Failed`) |
| `IndexingProgress` | `int` |
| `ContentHash` | `string?` |
| `Version` | `int` |
| `IsOutdated` | `bool` |
| `CreatedAt` | `DateTime?` |

**Response — batch**

- `200 OK` → `IReadOnlyList<DocumentUploadResultItemDto>`

**`DocumentUploadResultItemDto`**

| Field | Type |
|-------|------|
| `FileName` | `string` |
| `Success` | `bool` |
| `Error` | `string?` |
| `Document` | `DocumentDto?` |

### Service / logic

| Component | Responsibility |
|-----------|----------------|
| **`DocumentService`** (`BoneVisQA.Services/Services/DocumentService.cs`) | SHA-256 **content dedup** (`documents.content_hash`); uploads PDF to Supabase bucket `knowledge_base` / path `documents`; inserts `documents` with `IndexingStatus = "Processing"`; inserts **`document_tags`**; queues **`IngestDocumentInBackgroundAsync`** via `Task.Run` + new DI scope. |
| **`DocumentProcessingService`** (`BoneVisQA.Services/Services/DocumentUpload/DocumentProcessingService.cs`) | Per-file loop calling `DocumentService.UploadDocumentAsync` with derived titles. |
| **`IPdfProcessingService`** | `DownloadAndExtractPdfTextAsync(fileUrl)` — text extraction. |
| **`IEmbeddingService`** | `EmbedTextAsync` per chunk — **768-dim** vectors stored on chunks. |
| **Chunking** | `SplitTextRecursively` in `DocumentService`: target ~**800** chars, overlap **150**; removes prior chunks for doc on reindex. |

### Database tables (read / write)

| Table | Operation |
|-------|-----------|
| **`documents`** | INSERT (metadata, `file_path`, `indexing_status`, `indexing_progress`, `content_hash`, `version`, …); UPDATE during ingestion (status/progress) and reindex. |
| **`document_tags`** | INSERT (when `TagIds` provided). |
| **`document_chunks`** | DELETE existing by `doc_id` on reingest; INSERT rows with `content`, `chunk_order`, **`embedding`** (`vector(768)`). |

---

## 2. Student Visual QA (X-ray + optional bbox → RAG → Gemini → Q/A record)

### API endpoints

| Method | Route | Auth | Consumes |
|--------|-------|------|----------|
| `POST` | `/api/student/visual-qa/ask` | `Authorize` (JWT) | `multipart/form-data` (max **5MB** image) |
| `POST` | `/api/student/visual-qa/ask-json` | `Authorize` | `application/json` |

**Controller:** `VisualQAController` (`BoneVisQA.API/Controllers/VisualQAController.cs`).

**`ask` pipeline:** validate image → `ISupabaseStorageService.UploadFileAsync` → bucket `student_uploads` / `images/{studentId}` → build `VisualQARequestDto` → `IStudentService.CreateVisualQAQuestionAsync` → `IVisualQaAiService.RunPipelineAsync` → `IStudentService.SaveVisualQAAnswerAsync`.

**`ask-json`:** same except body is `VisualQARequestDto`; `ImageUrl` must be on configured **Supabase** host (SSRF guard).

### Core DTOs

**Multipart form — `VisualQAFileUploadRequest`** (controller-local)

| Field | Type | Notes |
|-------|------|--------|
| `QuestionText` | `string` | Required |
| `CustomImage` | `IFormFile?` | Required for `ask` |
| `Coordinates` | `string?` | Normalized bbox JSON `{"x","y","width","height"}` (0–1); aliases `w`/`h` accepted downstream |

**JSON body / internal — `VisualQARequestDto`** (`BoneVisQA.Services/Models/VisualQA/VisualQADtos.cs`)

| Field | Type | Notes |
|-------|------|--------|
| `QuestionText` | `string` | Required |
| `ImageUrl` | `string?` | Set by `ask` after upload; required for `ask-json` path |
| `Coordinates` | `string?` | Persisted to `student_questions.custom_coordinates` (jsonb) |
| `CaseId` | `Guid?` | `null` = personal Visual QA |
| `AnnotationId` | `Guid?` | If set with case flow: coords + image from `case_annotations` / `medical_images` |
| `Language` | `string?` | Normalized in `StudentService` |

**Response — `VisualQAResponseDto`**

| Field | Type | Notes |
|-------|------|--------|
| `AnswerText` | `string?` | |
| `SuggestedDiagnosis` | `string?` | Maps to `case_answers.structured_diagnosis` on save |
| `DifferentialDiagnoses` | `string?` | |
| `KeyImagingFindings` | `string?` | |
| `ReflectiveQuestions` | `string?` | |
| `AiConfidenceScore` | `double?` | Pipeline sets **max cosine similarity** of top RAG chunks; persisted on `case_answers.ai_confidence_score` |
| `ErrorMessage` | `string?` | Client hint when AI fails; not persisted |
| `Citations` | `List<CitationItemDto>` | |

**`CitationItemDto`**

| Field | Type |
|-------|------|
| `ChunkId` | `Guid` |
| `ReferenceUrl` | `string?` |
| `PageNumber` | `int?` (often `chunk_order + 1`) |
| `SourceText` | `string?` |

**Created question handle — `StudentQuestionDto`** (`BoneVisQA.Services/Models/Student/QuestionDtos.cs`)

| Field | Type | Notes |
|-------|------|--------|
| `Id` | `Guid` | Question id |
| `CaseId` | `Guid` | **`Guid.Empty` when personal** (`CaseId` null in DB) |
| `StudentId` | `Guid` | |
| `AnnotationId` | `Guid?` | |
| `QuestionText` | `string` | |
| `CreatedAt` | `DateTime?` | |

### Service / logic

| Component | Responsibility |
|-----------|----------------|
| **`StudentService`** (`BoneVisQA.Services/Services/Student/StudentService.cs`) | **`CreateVisualQAQuestionAsync`:** `BoundingBoxParser` for coordinates; optional load annotation + image; INSERT **`student_questions`**. **`SaveVisualQAAnswerAsync`:** INSERT **`case_answers`** with status from `ClassifyVisualQAAnswerStatus`; INSERT **`citations`** linking answer → `document_chunks`. |
| **`VisualQaAiService`** (`BoneVisQA.Services/Services/VisualQaAiService.cs`) | **`RunPipelineAsync`:** optional ROI via **`IImageProcessingService.DrawAnnotationOverlayAsBase64JpegAsync`**; **`BuildRagEmbeddingQuery`** (ROI / image hints); **`IEmbeddingService.EmbedTextAsync`**; vector search on **`document_chunks`** (top 5, cosine distance), optional filter by **`medical_cases.category_id`** when `CaseId` set; similarity gate (**0.72**); **`IGeminiService.GenerateMedicalAnswerAsync`** with prompt + base64 image; merge Gemini citation IDs with chunk metadata; set `AiConfidenceScore`. |
| **`GeminiService`** | Model call + JSON parsing into `VisualQAResponseDto` (incl. SEPS fields, citations). |
| **`ISupabaseStorageService`** | Student image upload (`ask`). |

### Database tables

| Table | Operation |
|-------|-----------|
| **`student_questions`** | INSERT (`student_id`, optional `case_id` / `annotation_id`, `question_text`, `language`, `custom_image_url`, `custom_coordinates`). |
| **`case_answers`** | INSERT (`question_id`, answer fields, `ai_confidence_score`, `status`, `generated_at`, …). |
| **`citations`** | INSERT (`answer_id`, `chunk_id`, `similarity_score`; often `0` from Visual QA save path). |
| **`document_chunks`**, **`documents`** | **Read** (RAG retrieval; join/include `Doc` for category filter and citation URLs). |
| **`medical_cases`**, **`case_annotations`**, **`medical_images`** | **Read** when `AnnotationId` / `CaseId` used in question creation or category-scoped RAG. |

---

## 3. Lecturer triage & escalate (`answerId`)

### API endpoints

| Method | Route | Auth | Role |
|--------|-------|------|------|
| `GET` | `/api/lecturer/triage?classId={guid}` | `Authorize` | `Lecturer` |
| `PUT` | `/api/lecturer/reviews/{answerId}/escalate` | `Authorize` | `Lecturer` |
| `POST` | `/api/lecturer/triage/{answerId}/escalate` | `Authorize` | `Lecturer` (legacy alias) |
| `PUT` | `/api/lecturer/triage/{answerId}/escalate` | `Authorize` | `Lecturer` (legacy alias) |

**Triage list:** `LecturersController` → `ILecturerService.GetTriageListAsync`.

**Escalate:** `LecturerReviewsController` / `LecturerTriageController` → **`ILecturerTriageService.EscalateAnswerAsync`**.

### Core DTOs

**Triage row — `LecturerTriageRowDto`** (`BoneVisQA.Services/Models/Lecturer/LecturerWorkflowDtos.cs`)

JSON: `answerId` (required for escalate).

| Field | Type | Notes |
|-------|------|--------|
| `answerId` | `Guid` | **`case_answers.id`** |
| `questionId` | `Guid` | |
| `studentId` | `Guid` | |
| `studentName` / `studentEmail` | `string` / `string?` | |
| `classId` / `className` | `Guid` / `string` | From query `classId` |
| `caseId` / `caseTitle` | `Guid?` / `string?` | |
| `thumbnailUrl` / `imageUrl` | `string?` | Personal `custom_image_url` or first case image |
| `questionText` | `string` | |
| `answerText` | `string?` | |
| `status` | `string` | From `case_answers.status` |
| `aiConfidenceScore` | `double?` | |
| `askedAt` | `DateTime?` | Question `created_at` |
| `isEscalated` | `bool` | |
| `escalatedByName` / `escalatedAt` | `string?` / `DateTime?` | |

**Escalate body — `EscalateAnswerRequestDto`** (optional)

| Field | Type |
|-------|------|
| `reviewNote` | `string?` |

**Escalate response — `EscalatedAnswerDto`**

| Field | Type |
|-------|------|
| `answerId`, `questionId`, `studentId` | `Guid` |
| `studentName`, `studentEmail` | `string` |
| `caseId` | `Guid?` |
| `caseTitle`, `questionText` | `string` |
| `currentAnswerText`, `structuredDiagnosis`, `differentialDiagnoses` | `string?` |
| `status` | `string` (e.g. `EscalatedToExpert`) |
| `escalatedById`, `escalatedAt` | `Guid?`, `DateTime?` |
| `aiConfidenceScore` | `double?` |
| `classId`, `className` | `Guid?`, `string` |
| `reviewNote` | `string?` |

### Service / logic

| Component | Responsibility |
|-----------|----------------|
| **`LecturerService.GetTriageListAsync`** | Loads class; enrollments → student ids; query **`case_answers`** with `Include(Question→Student, Question→Case→MedicalImages)`; filters: answers for class students AND (escalated **or** not terminal statuses AND low/null AI confidence per `LecturerTriageThresholds`); maps `answerId` = `CaseAnswer.Id`. |
| **`LecturerTriageService.EscalateAnswerAsync`** | Loads answer + question; verifies **`class_enrollments`** where **`academic_classes.lecturer_id`** = caller; requires class **`expert_id`**; validates status via **`CaseAnswerStatuses.CanEscalateFromLecturer`**; sets **`EscalatedToExpert`**, `escalated_by_id`, `escalated_at`; optional **`expert_reviews`** row (note, action `Escalated`). |

### Database tables

| Table | Operation |
|-------|-----------|
| **`academic_classes`** | **Read** (triage class metadata). |
| **`class_enrollments`** | **Read** (student set for class; lecturer/expert linkage). |
| **`case_answers`** | **Read** (triage); **UPDATE** (escalation fields, status). |
| **`student_questions`** | **Read** (via includes). |
| **`medical_cases`**, **`medical_images`** | **Read** (thumbnail resolution). |
| **`users`** | **Read** (student names; escalator names). |
| **`expert_reviews`** | **INSERT/UPDATE** when `reviewNote` provided on escalate. |

---

## 4. Expert review (escalated queue scoped to expert’s classes → approve / edit)

### API endpoints

| Method | Route | Auth | Role |
|--------|-------|------|------|
| `GET` | `/api/expert/reviews/escalated` | `Authorize` | `Expert` |
| `GET` | `/api/expert/reviews/case-answer` | `Authorize` | `Expert` | Same data as `escalated` (delegates to same service method). |
| `POST` | `/api/expert/reviews/{answerId}/resolve` | `Authorize` | `Expert` |
| `POST` | `/api/expert/reviews/{answerId}/approve` | `Authorize` | `Expert` | Same handler as `resolve`. |
| `PUT` | `/api/expert/reviews/{answerId}/approve` | `Authorize` | `Expert` | Same handler as `resolve`. |

**Controller:** `ExpertReviewsController` → **`IExpertReviewService`**.

### Core DTOs

**Queue item — `ExpertEscalatedAnswerDto`** (`BoneVisQA.Services/Models/Expert/ReviewDtos.cs`)

| Field | Type | Notes |
|-------|------|--------|
| `answerId` … `reviewNote` | (same conceptual shape as triage + expert context) | Includes **`citations`**: `List<ExpertCitationDto>` |
| `citations[]` | `ExpertCitationDto` | `chunkId`, `sourceText`, `referenceUrl`, `pageNumber` |

**Resolve / approve body — `ResolveEscalatedAnswerRequestDto`**

| Field | Type |
|-------|------|
| `answerText` | `string` |
| `structuredDiagnosis` | `string?` |
| `differentialDiagnoses` | `string?` |
| `keyImagingFindings` | `string?` |
| `reflectiveQuestions` | `string?` |
| `reviewNote` | `string?` |

**Response:** `ExpertEscalatedAnswerDto` (updated row).

### Service / logic

| Component | Responsibility |
|-----------|----------------|
| **`ExpertReviewService.GetEscalatedAnswersAsync`** | **`QueryExpertScopedEscalatedQueue`:** `case_answers.status` in (`EscalatedToExpert`, `Escalated`) AND EXISTS **`class_enrollments`** + **`academic_classes.expert_id`** = expert AND student = question’s student. **Includes:** Question→Student, Question→Case, Citations→Chunk→Doc, ExpertReviews. Batched enrollments for `classId` / `className`. |
| **`ResolveEscalatedAnswerAsync`** | Load answer with same includes; verify enrollment for **this** expert; reject if already **`ExpertApproved`** or not escalated; **UPDATE** `case_answers` (text fields, `reviewed_by_id`, `reviewed_at`, `status = ExpertApproved`); **UPSERT** `expert_reviews`; notify student; **`IRagExpertAnswerIndexingSignal.NotifyExpertApprovedForFutureIndexingAsync`**. |

### Database tables

| Table | Operation |
|-------|-----------|
| **`case_answers`** | **Read** (queue, resolve); **UPDATE** (final answer + status + review metadata). |
| **`student_questions`** | **Read** (via navigation). |
| **`class_enrollments`**, **`academic_classes`** | **Read** (expert scope). |
| **`citations`**, **`document_chunks`**, **`documents`** | **Read** (evidence on queue). |
| **`expert_reviews`** | **Read** / **INSERT** / **UPDATE**. |
| **`users`** | **Read** (student profile on DTOs). |

---

## 5. Expert create case (JSON: image URLs + bounding boxes)

### API endpoint

| Method | Route | Auth | Role | Consumes |
|--------|-------|------|------|----------|
| `POST` | `/api/expert/cases` | `Authorize` | `Expert` | `application/json` |

**Controller:** `ExpertController.CreateCase` → **`IMedicalCaseService.CreateMedicalCaseWithImagesJsonAsync`**.

Expert id from JWT `ClaimTypes.NameIdentifier`.

**Response (`Ok`):** anonymous object:

```json
{
  "message": "Medical case created successfully",
  "caseId": "<guid>",
  "result": { /* CreateMedicalCaseResponseDTO */ }
}
```

### Core DTOs

**Body — `CreateExpertMedicalCaseJsonRequest`** (`BoneVisQA.Services/Models/Expert/MedicalCaseDTO.cs`)

| Field | Type |
|-------|------|
| `title` | `string` |
| `description` | `string` |
| `difficulty` | `string?` |
| `categoryId` | `Guid?` |
| `suggestedDiagnosis` | `string?` |
| `reflectiveQuestions` | `string?` |
| `keyFindings` | `string?` |
| `tagIds` | `List<Guid>?` |
| `medicalImages` | `List<CreateExpertMedicalCaseImageJson>?` |

**`CreateExpertMedicalCaseImageJson`**

| Field | Type | Notes |
|-------|------|--------|
| `imageUrl` | `string` | Public URL (**FE uploads to Supabase first**); blank skipped |
| `modality` | `string?` | |
| `annotations` | `List<CreateAnnotationDTO>?` | |

**`CreateAnnotationDTO`**

| Field | Type |
|-------|------|
| `label` | `string` |
| `coordinates` | `string?` | Bbox JSON (stored in `case_annotations.coordinates` jsonb) |

**`result` shape — `CreateMedicalCaseResponseDTO`**

| Field | Type |
|-------|------|
| `id` | `Guid` |
| `expertName` | `string?` |
| `title`, `description` | `string` |
| `difficulty`, `categoryName` | `string?` |
| `isApproved`, `isActive` | `bool?` |
| `suggestedDiagnosis`, `reflectiveQuestions`, `keyFindings` | `string?` |
| `createdAt` | `DateTime?` |

### Service / logic

| Component | Responsibility |
|-----------|----------------|
| **`MedicalCaseService.CreateMedicalCaseWithImagesJsonAsync`** | Maps to **`CreateMedicalCaseRequestDTO`** with `CreatedByExpertId` = JWT expert → **`CreateMedicalCaseAsync`** (INSERT **`medical_cases`**, default `IsApproved`/`IsActive` true); foreach image INSERT **`medical_images`**; foreach annotation INSERT **`case_annotations`**; **`ApplyCaseTagIdsAsync`** for **`case_tags`**. |

### Database tables

| Table | Operation |
|-------|-----------|
| **`medical_cases`** | INSERT |
| **`medical_images`** | INSERT (`case_id`, `image_url`, `modality`, …) |
| **`case_annotations`** | INSERT (`image_id`, `label`, `coordinates`) |
| **`case_tags`** | INSERT (valid `tag_id` only) |
| **`categories`**, **`tags`**, **`users`** | **Read** (validation / display names in response) |

---

## Cross-cutting constants (do not break DB checks)

- **`case_answers.status`** is constrained in PostgreSQL (`case_answers_status_check`). Application constants: **`BoneVisQA.Services/Constants/CaseAnswerStatuses.cs`** (used by Visual QA classification, triage filters, escalation, expert resolve).
- **Embeddings:** `document_chunks.embedding` is **`vector(768)`**; RAG and ingestion must stay aligned with **`IEmbeddingService`** output size.

---

*Generated from codebase scan: controllers under `BoneVisQA.API/Controllers`, services under `BoneVisQA.Services`, entities under `BoneVisQA.Repositories/Models`.*
