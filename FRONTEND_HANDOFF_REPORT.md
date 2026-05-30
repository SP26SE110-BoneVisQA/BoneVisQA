# FRONTEND_HANDOFF_REPORT.md

**BoneVisQA Capstone — AI-to-AI Frontend Handoff (Code Freeze, Backend Readiness ~96%)**

> **Audience:** Frontend Cursor AI / React (or Next.js) developer.  
> **Backend stack:** .NET 8 API gateway + Python FastAPI AI microservice + Supabase PostgreSQL.  
> **Your mandate:** Rebuild the Student Visual QA experience only against the contracts below. Do not invent endpoints, fields, or error shapes.

**Base URL (local dev):** `http://localhost:5046` (or value from `VITE_API_BASE_URL` / env).  
**Auth header (all protected routes):** `Authorization: Bearer <JWT>`  
**JSON casing:** camelCase (ASP.NET Core default).  
**CORS:** Credentials required; origins must match API `Cors:AllowedOrigins` (e.g. `http://localhost:5173`).

---

## SECTION 1: Core Workflows & State Requirements

### 1.1 Authentication prerequisite (both flows)

Before any Student Visual QA call:

1. `POST /api/auths/login` with email/password.
2. Persist `token` (JWT), `userId`, `roles` from the response.
3. Attach `Authorization: Bearer {token}` on every subsequent request.
4. If `roles` does not include `"Student"`, do not mount the Student Visual QA UI.

---

### Flow A — Catalog Q&A (approved library cases)

**Goal:** Student browses expert-approved cases, opens a case, asks up to **3 AI turns** per Visual QA session.

| Step | Action | API |
|------|--------|-----|
| A1 | Load case library (optional filters) | `GET /api/student/cases/catalog?location=&lesionType=&difficulty=&q=` |
| A2 | Open case detail (images, expert summary) | `GET /api/student/cases/{caseId}` |
| A3 | Start or continue Visual QA chat | `POST /api/student/visual-qa/ask-json` |
| A3b | (Optional) Reload full thread | `GET /api/student/visual-qa/history/{sessionId}` |
| A4 | (Optional) List past catalog sessions | `GET /api/student/visual-qa/history/cases?limit=20&offset=0` |

**Sequence rules (strict):**

1. **A1 → A2:** User picks a case from catalog; navigate with `caseId`.
2. **A2 → A3 (first question):** Call `ask-json` with `caseId`, `questionText`, and optionally `imageId`, `annotationId`, `coordinates`. **Do not send `sessionId` on the first turn** — the server creates a new session.
3. **A3 (follow-up turns):** Include `sessionId` from the previous `ask-json` response. Keep the same `caseId`. Maximum **3 billable user turns** per session (enforced server-side).
4. **Image for viewer (left panel):** Use `primaryImageUrl` from case detail, or `images[].imageUrl` when multiple slices exist. If user draws an ROI, send `coordinates` as normalized JSON (see Section 2).

---

### Flow B — Personal Q&A (student DICOM upload)

**Goal:** Student uploads their own `.zip`/`.rar` DICOM study; backend ingests via Python, creates a personal case + Visual QA session; student chats immediately.

| Step | Action | API |
|------|--------|-----|
| B1 | Upload DICOM archive | `POST /api/student/visual-qa/upload-personal` (multipart) |
| B2 | Ask questions on ingested study | `POST /api/student/visual-qa/ask-json` |
| B2b | (Optional) Reload thread | `GET /api/student/visual-qa/history/{sessionId}` |
| B3 | (Optional) List personal sessions | `GET /api/student/visual-qa/history/personal?limit=20&offset=0` |

**Sequence rules (strict):**

1. **B1:** Upload **only** `.zip` or `.rar` (max **200 MB**). Show upload progress; block submit otherwise.
2. On **B1 success (`ingestOk: true`):** Persist **`sessionId`**, **`caseId`**, **`previewImageUrl`** from the response. Display `previewImageUrl` in the left image panel immediately.
3. **B2 (first question):** Call `ask-json` with `{ sessionId, caseId, questionText }` (and optional `coordinates`). The server hydrates image context from the session — clients must **not** send `imageUrl` in JSON (it is `[JsonIgnore]`).
4. **B2 (follow-ups):** Same `sessionId`; respect `capabilities.canAskNext` and turn limit **3**.
5. **Do not** call deprecated raster multipart `POST /ask` for new features — use `upload-personal` + `ask-json` only.

---

### 1.2 Client state you MUST persist

| State key | When set | Used for |
|-----------|----------|----------|
| `auth.token` | Login | `Authorization` header |
| `auth.userId` | Login | Profile / debugging |
| `auth.roles` | Login | Route guards |
| `visualQa.flow` | User chooses path | `"catalog"` \| `"personal"` (do not infer from API alone) |
| `visualQa.sessionId` | First `ask-json` or `upload-personal` | Follow-up questions, thread reload |
| `visualQa.caseId` | Case detail or upload response | `ask-json` body, access checks |
| `visualQa.previewImageUrl` | Case detail `primaryImageUrl` or upload `previewImageUrl` | Left-panel viewer |
| `visualQa.imageId` | User selects slice in multi-image case | Optional `ask-json` field |
| `visualQa.annotationId` | User picks catalog annotation | Optional `ask-json` field |
| `visualQa.coordinates` | User draws ROI bbox | Optional `ask-json` — normalized 0–1 JSON string |
| `visualQa.chatTurns[]` | Each `ask-json` OK response | Render chat from `latestTurn` + append; or hydrate from `GET history/{sessionId}` |
| `visualQa.capabilities` | Each `ask-json` response | Disable input when `canAskNext === false` |
| `visualQa.clientRequestId` | Per send (UUID) | Optional idempotency / optimistic UI correlation |

**Recommended store:** Zustand (see Section 4) with `persist` middleware for `auth.*` and session ids only — not full chat blobs unless needed for offline resume.

---

## SECTION 2: Exact API Contracts (The "Source of Truth")

### 2.1 Auth — Login

| Property | Value |
|----------|-------|
| **Method / Route** | `POST /api/auths/login` |
| **Auth** | None (`[AllowAnonymous]`) |
| **Content-Type** | `application/json` |

**Request body:**

```json
{
  "email": "student@university.edu",
  "password": "your-password"
}
```

**200 OK response (`AuthResultDto`):**

```json
{
  "success": true,
  "message": "Login successful.",
  "userId": "11111111-1111-1111-1111-111111111111",
  "fullName": "Nguyen Van A",
  "email": "student@university.edu",
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "requiresMedicalVerification": false,
  "roles": ["Student"]
}
```

**401 Unauthorized:** `{ "success": false, "message": "..." }` — show toast; stay on login.

---

### 2.2 Catalog — List cases

| Property | Value |
|----------|-------|
| **Method / Route** | `GET /api/student/cases/catalog` |
| **Auth** | `Bearer` + role **Student** |
| **Query (all optional)** | `location`, `lesionType`, `difficulty`, `q` (alias: `search`) |

**200 OK:** `CaseListItemDto[]`

```json
[
  {
    "id": "uuid",
    "title": "Distal radius fracture",
    "description": "...",
    "difficulty": "Intermediate",
    "categoryName": "Trauma",
    "categoryDisplay": "Trauma",
    "thumbnailUrl": "https://...signed-url...",
    "isApproved": true,
    "tags": ["wrist", "fracture"],
    "createdAt": "2026-05-01T00:00:00Z",
    "caseOrigin": "Created by Expert"
  }
]
```

**Note:** Only **approved** catalog cases (`isApproved: true`, no personal owner) are returned.

---

### 2.3 Catalog — Case detail

| Property | Value |
|----------|-------|
| **Method / Route** | `GET /api/student/cases/{caseId}` |
| **Auth** | `Bearer` + role **Student** |

**200 OK (`CaseDetailDto`):**

```json
{
  "id": "uuid",
  "title": "...",
  "description": "...",
  "difficulty": "Intermediate",
  "categoryName": "Trauma",
  "categoryDisplay": "Trauma",
  "expertSummary": "...",
  "keyFindings": "...",
  "primaryImageUrl": "https://...",
  "isApproved": true,
  "images": [
    {
      "id": "image-uuid",
      "imageUrl": "https://...",
      "modality": "XR",
      "roiBoundingBox": "{\"x\":0.1,\"y\":0.2,\"width\":0.3,\"height\":0.4}"
    }
  ],
  "createdAt": "2026-05-01T00:00:00Z",
  "caseOrigin": "Created by Expert"
}
```

**404:** `{ "message": "Medical case not found." }` — unapproved, wrong id, or inaccessible.

---

### 2.4 Personal — Upload DICOM study

| Property | Value |
|----------|-------|
| **Method / Route** | `POST /api/student/visual-qa/upload-personal` |
| **Auth** | `Bearer` + role **Student** |
| **Content-Type** | `multipart/form-data` |
| **Rate limit** | Policy `AiInteractionLimit` (429 if exceeded) |

**Form fields:**

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `file` | file | **Yes** | Extension **`.zip` or `.rar` only**; max **209715200 bytes (200 MB)** |
| `diagnosisText` | string | No | Optional student note sent to ingest pipeline |

**400 Bad Request** (client/ingest validation — corrupt zip, empty archive, bad format):

```json
{
  "sessionId": "00000000-0000-0000-0000-000000000000",
  "caseId": "00000000-0000-0000-0000-000000000000",
  "previewImageUrl": "",
  "ingestOk": false,
  "ingestError": "File is not a zip file"
}
```

**502 Bad Gateway** (AI ingest service down): same shape, `ingestOk: false`.

**200 OK (success):**

```json
{
  "sessionId": "33333333-3333-3333-3333-333333333333",
  "caseId": "44444444-4444-4444-4444-444444444444",
  "previewImageUrl": "https://.../preview.png",
  "ingestOk": true,
  "ingestError": null
}
```

**Immediately persist** `sessionId`, `caseId`, `previewImageUrl` and navigate to the split-screen workspace.

---

### 2.5 Visual QA — Ask JSON (primary chat endpoint)

| Property | Value |
|----------|-------|
| **Method / Route** | `POST /api/student/visual-qa/ask-json?locale=vi` |
| **Auth** | `Bearer` + role **Student** |
| **Content-Type** | `application/json` |
| **Headers (optional)** | `Accept-Language: vi-VN,en;q=0.8` |
| **Rate limit** | `AiInteractionLimit` |

**Query:**

| Param | Description |
|-------|-------------|
| `locale` | `vi` or `en` — influences Gemini response language (with Vietnamese question heuristic) |

**Request body (`VisualQARequestDto` — JSON-bound fields only):**

```json
{
  "questionText": "What fracture pattern is visible in the distal radius?",
  "coordinates": "{\"x\":0.12,\"y\":0.34,\"width\":0.40,\"height\":0.25}",
  "caseId": "44444444-4444-4444-4444-444444444444",
  "annotationId": null,
  "sessionId": "33333333-3333-3333-3333-333333333333",
  "imageId": null,
  "clientRequestId": "fe-opt-uuid-optional"
}
```

| Field | Required | Notes |
|-------|----------|-------|
| `questionText` | **Yes** | Non-empty |
| `caseId` | Flow A: **Yes**; Flow B: **Yes** (from upload) | |
| `sessionId` | Follow-ups: **Yes**; first catalog turn: omit | Invalid id → **404** |
| `coordinates` | No | Normalized bbox 0–1; also accepts `w`/`h` aliases server-side |
| `annotationId` | No | Catalog only — loads ROI/image from annotation |
| `imageId` | No | Disambiguates multi-image cases |
| `clientRequestId` | No | Idempotency / optimistic UI |

**Do NOT send:** `imageUrl`, `resolvedResponseLanguage` (server-only, `[JsonIgnore]`).

**200 OK (`VisualQaApiResponseDto`):**

```json
{
  "sessionId": "33333333-3333-3333-3333-333333333333",
  "caseId": "44444444-4444-4444-4444-444444444444",
  "isPersonalUpload": false,
  "diagnosis": "Distal radius buckle fracture",
  "findings": [
    "Dorsal angulation of the distal fragment",
    "Soft tissue swelling over the wrist"
  ],
  "differentialDiagnoses": [
    "Greenstick fracture",
    "Salter-Harris II"
  ],
  "reflectiveQuestions": [
    "Which view best demonstrates the cortical breach?",
    "What growth plate signs would you assess?"
  ],
  "citations": [
    {
      "chunkId": "uuid",
      "medicalCaseId": "uuid-or-null",
      "referenceUrl": "https://...",
      "pageNumber": 12,
      "startPage": null,
      "endPage": null,
      "sourceText": "...",
      "displayLabel": "Document §3.2",
      "pageLabel": "p. 12",
      "href": "https://...",
      "snippet": "...",
      "kind": "doc"
    }
  ],
  "capabilities": {
    "canAskNext": true,
    "isReadOnly": false,
    "canRequestReview": false,
    "turnsUsed": 1,
    "turnLimit": 3
  },
  "responseKind": "analysis",
  "policyReason": null,
  "clientRequestId": "fe-opt-uuid-optional",
  "reviewState": "none",
  "lastResponderRole": "assistant",
  "systemNotice": null,
  "latestTurn": {
    "sessionId": "33333333-3333-3333-3333-333333333333",
    "turnId": "uuid-string",
    "actorRole": "assistant",
    "userMessageId": "00000000-0000-0000-0000-000000000000",
    "assistantMessageId": "uuid-or-null",
    "userMessage": "What fracture pattern...",
    "questionCoordinates": "{\"x\":0.12,\"y\":0.34,\"width\":0.4,\"height\":0.25}",
    "questionText": "What fracture pattern...",
    "messageText": "Full assistant narrative...",
    "answerText": "Full assistant narrative...",
    "diagnosis": "Distal radius buckle fracture",
    "findings": ["..."],
    "differentialDiagnoses": ["..."],
    "reflectiveQuestions": ["..."],
    "citations": [],
    "createdAt": "2026-05-22T02:00:00Z",
    "responseKind": "analysis",
    "policyReason": null,
    "reviewState": "none",
    "lastResponderRole": "assistant",
    "isReviewTarget": false,
    "target_assistant_message_id": null
  }
}
```

**Field mapping for UI (right chat panel):**

| UI block | API source |
|----------|------------|
| Primary answer / diagnosis card | `diagnosis` (also `latestTurn.diagnosis`) |
| Bullet findings | `findings[]` |
| Differential list | `differentialDiagnoses[]` |
| Socratic prompts | `reflectiveQuestions[]` |
| Evidence chips / links | `citations[]` (`displayLabel`, `href`, `snippet`, `pageLabel`) |
| Turn counter | `capabilities.turnsUsed` / `capabilities.turnLimit` |
| Disable composer | `!capabilities.canAskNext` |

**Other Visual QA status codes (controller-handled, not always RFC 7807):**

| Code | Shape | When |
|------|-------|------|
| **400** | `{ message, systemNotice, capabilities, latestTurn }` | `TURN_LIMIT_EXCEEDED`, `SESSION_EXPIRED`, `SESSION_READ_ONLY`, `MISSING_QUESTION`, validation |
| **404** | `{ message }` | Invalid `sessionId`, case not found / not approved |
| **502** | `{ message }` | AI response format error |
| **503** | `{ message }` | AI/RAG `responseKind: "error"` or overload |
| **429** | plain / rate-limit body | Too many AI calls |

**Session blocked 400 example (`TURN_LIMIT_EXCEEDED`):**

```json
{
  "message": "You have used all question turns for this Visual QA session.",
  "systemNotice": "You have used all question turns for this Visual QA session.",
  "capabilities": {
    "canAskNext": false,
    "isReadOnly": false,
    "canRequestReview": false,
    "turnsUsed": 0,
    "turnLimit": 3
  },
  "latestTurn": {
    "turnId": "system:turn_limit_exceeded",
    "actorRole": "system",
    "userMessage": "",
    "questionText": "",
    "messageText": "You have used all question turns for this Visual QA session.",
    "responseKind": "system_notice",
    "policyReason": null
  }
}
```

Append `latestTurn` to chat as a **system_notice** bubble when present.

---

### 2.6 Visual QA — Thread reload (chat history)

| Property | Value |
|----------|-------|
| **Method / Route** | `GET /api/student/visual-qa/history/{sessionId}` |
| **Auth** | `Bearer` + role **Student** |

**200 OK (`VisualQaThreadDto`):**

```json
{
  "sessionId": "uuid",
  "sessionImageUrl": "https://...",
  "imageUrl": "https://...",
  "studyImageUrl": "https://...",
  "roiBoundingBox": "{...}",
  "caseId": "uuid",
  "imageId": "uuid-or-null",
  "turns": [ /* VisualQaTurnDto[] — same shape as latestTurn */ ],
  "capabilities": { "canAskNext": true, "turnsUsed": 2, "turnLimit": 3, "...": "..." },
  "reviewState": "none",
  "lastResponderRole": "assistant",
  "blockingNotice": null,
  "rejectionReason": null
}
```

Use `turns[]` to rebuild the chat timeline; use `sessionImageUrl` / `studyImageUrl` for the left viewer.

---

## SECTION 3: Strict Error Handling (RFC 7807)

### 3.1 Two error channels (you must handle both)

1. **Visual QA controller errors (expected):** JSON objects with `message`, often `systemNotice`, `capabilities`, `latestTurn` — **not** always `ProblemDetails`.
2. **Global unhandled exceptions:** RFC 7807 **`ProblemDetails`** from `IExceptionHandler` (`GlobalExceptionHandler`).

### 3.2 RFC 7807 `ProblemDetails` shape (unhandled exceptions)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Resource not found",
  "status": 404,
  "detail": "The requested resource was not found.",
  "instance": "/api/student/visual-qa/ask-json",
  "reason": "TURN_LIMIT_EXCEEDED"
}
```

| Field | Meaning |
|-------|---------|
| `status` | HTTP status (mirror of response code) |
| `title` | Short category (`Invalid request`, `Resource not found`, `Service unavailable`, `Server error`) |
| `detail` | Human-readable message (production-safe; dev may expose more) |
| `instance` | Request path |
| `reason` | Extension — machine hint when present (`timeout`, `ai_or_session_busy`, `TURN_LIMIT_EXCEEDED`, etc.) |

### 3.3 HTTP status → UI mapping (mandatory)

| Status | Meaning | Frontend action |
|--------|---------|-----------------|
| **400** | Validation, bad input, **session policy** | **Non-blocking Toast** (warning). If body has `systemNotice` or `latestTurn.responseKind === "system_notice"`, also inject system bubble in chat. Subcases: `TURN_LIMIT_EXCEEDED` → disable composer, show turn counter maxed; `SESSION_EXPIRED` / `SESSION_READ_ONLY` → lock session, offer "Start new study". |
| **401** | Missing/invalid JWT | Toast + redirect to login; clear auth store. |
| **404** | Unknown `sessionId`, `caseId`, or unapproved case | Toast (error): "Case or session not found." Navigate back to catalog if case detail fails. |
| **409** | Conflict (rare on student path) | Toast (warning) with `detail`. |
| **429** | Rate limit (`AiInteractionLimit`) | Toast: "Too many AI requests — wait a few seconds." Disable send briefly. |
| **502** | Bad gateway (ingest / AI format) | Toast (error); for upload, show `ingestError` inline on upload card. |
| **503** | AI/RAG busy, timeout, overload, concurrent session lock | Toast (info/warning): "AI is busy, try again." **Do not** clear chat. Optional retry button on last user message. |
| **500** | Unhandled server error | Toast (error) generic message; log `detail` only in dev. |

### 3.4 Toast notification rules (strict)

- Use a **global toast library** (e.g. sonner, react-hot-toast) — **non-blocking**, top-right, auto-dismiss 5s.
- **Never** use `window.alert` or blocking modals for API errors except unrecoverable auth logout.
- Map: 400/409 → warning; 404 → error; 503/429 → info/warning; 500/502 → error.
- For Visual QA **400 session blocks**, toast the `message` **and** render `systemNotice` inside the chat thread.

### 3.5 API client interceptor pattern (pseudo-code)

```typescript
async function apiFetch(path, options) {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${getToken()}`,
      "Accept-Language": getLocale(),
      ...options.headers,
    },
  });

  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    const msg = body.detail ?? body.message ?? body.title ?? res.statusText;
    toastByStatus(res.status, msg);
    throw new ApiError(res.status, body);
  }
  return res.json();
}
```

---

## SECTION 4: Frontend UI/UX Architectural Directives

### 4.1 Layout: Split-screen Medical Workspace (mandatory)

Build the Student Visual QA page as a **fixed split-screen workspace**:

```
┌─────────────────────────────┬──────────────────────────────┐
│  LEFT (~55%)                │  RIGHT (~45%)                │
│  Medical Image Viewer       │  Interactive AI Chat         │
│  - previewImageUrl /        │  - Message list (user +      │
│    primaryImageUrl          │    assistant + system)       │
│  - ROI overlay from         │  - Composer (disabled when   │
│    coordinates /            │    !capabilities.canAskNext) │
│    roiBoundingBox           │  - Turn badge: used/limit    │
│  - Zoom/pan (min)           │  - Citations as link chips   │
│  - Upload dropzone          │  - Diagnosis / findings /    │
│    (Flow B only)            │    differentials / reflective│
└─────────────────────────────┴──────────────────────────────┘
```

- **Flow A:** Left panel loads image from `GET /cases/{id}` (`primaryImageUrl` or selected `images[]`).
- **Flow B:** Left panel shows `previewImageUrl` after upload; show upload progress + ingest error inline.
- **Chat:** Render assistant content from `diagnosis`, `findings`, `differentialDiagnoses`, `reflectiveQuestions`, `citations` — not only raw `latestTurn.messageText`.

### 4.2 State management — Zustand (mandatory)

Use **Zustand** (with optional `persist` for auth + session ids) to manage:

```typescript
// Example slice structure — implement fully in FE project
interface VisualQaStore {
  flow: "catalog" | "personal" | null;
  sessionId: string | null;
  caseId: string | null;
  previewImageUrl: string | null;
  capabilities: Capabilities | null;
  turns: VisualQaTurn[];       // append from ask-json + hydrate from GET thread
  isAsking: boolean;             // lock composer during POST ask-json
  setFromUpload: (r: UploadResponse) => void;
  appendTurn: (r: AskJsonResponse) => void;
  hydrateThread: (t: ThreadDto) => void;
  resetSession: () => void;
}
```

**Rules:**

- Single source of truth for `sessionId` — never duplicate in component `useState` only.
- On each successful `ask-json`, **append** `latestTurn` to `turns` and update `capabilities`.
- On **400 session block**, still append system `latestTurn` if provided.
- Clear store on logout and when user explicitly starts a new case/upload.

### 4.3 UX constraints tied to backend

| Rule | Rationale |
|------|-----------|
| Max **3** questions per session | `turnLimit: 3` enforced server-side + Postgres advisory lock on concurrent asks |
| Disable send while `isAsking` | Prevent double-submit; backend serializes per `sessionId` |
| Show citation links externally | `citations[].href` / `referenceUrl` open in new tab |
| Locale toggle sends `?locale=vi\|en` + `Accept-Language` | Drives Vietnamese/English AI responses |
| Personal upload: only `.zip`/`.rar` | `StudyArchiveIngestHelper` validation |
| Do not implement `POST /ask` multipart for new UI | Deprecated path for raster; use `upload-personal` + `ask-json` |

### 4.4 Pages / routes (suggested)

| Route | Purpose |
|-------|---------|
| `/login` | Auth |
| `/student/cases` | Catalog grid (Flow A entry) |
| `/student/cases/:caseId` | Case detail + "Ask AI" → workspace |
| `/student/visual-qa/upload` | Personal upload (Flow B entry) |
| `/student/visual-qa/:sessionId` | Split workspace (both flows) |

### 4.5 Out of scope for this handoff

- Lecturer/Expert/Admin APIs  
- `POST /api/student/visual-qa/ask-stream` (SSE) — optional phase 2  
- `POST /api/student/visual-qa/ask` multipart — legacy  
- Direct Python AI URLs — **always** go through .NET gateway  

---

## Appendix: Quick reference card

| Endpoint | Method | Auth |
|----------|--------|------|
| `/api/auths/login` | POST | No |
| `/api/student/cases/catalog` | GET | Student |
| `/api/student/cases/{caseId}` | GET | Student |
| `/api/student/visual-qa/upload-personal` | POST multipart | Student |
| `/api/student/visual-qa/ask-json` | POST JSON | Student |
| `/api/student/visual-qa/history/{sessionId}` | GET | Student |
| `/api/student/visual-qa/history/personal` | GET | Student |

**Backend code freeze tag:** Enterprise readiness **~96%** — treat this document as authoritative for Student Visual QA integration.

---

*Generated from BoneVisQA .NET 8 API controllers, DTOs, and `GlobalExceptionHandler` — May 2026.*
