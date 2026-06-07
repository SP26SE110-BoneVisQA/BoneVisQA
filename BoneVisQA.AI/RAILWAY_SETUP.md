# Deploy BoneVisQA.AI lên Railway

Tài liệu này đi kèm code đã chuẩn bị sẵn (`Procfile`, `railway.toml`, `runtime.txt`, `requirements.txt`, CORS + PORT trong `app/main.py`).

## 1. Push code lên GitHub

```powershell
cd d:\DATN\BoneVisQA
git add BoneVisQA.AI/
git status
git commit -m "Prepare BoneVisQA.AI for Railway deploy"
git push origin main
```

Repo: `https://github.com/SP26SE110-BoneVisQA/BoneVisQA.git`

**Không** commit `BoneVisQA.AI/.env` (đã gitignore).

---

## 2. Tài khoản Railway

| Bước | Việc cần làm |
|------|----------------|
| 1 | Vào https://railway.app → **Login with GitHub** |
| 2 | (Khuyến nghị) **Account Settings → Billing** → nạp tối thiểu (~$5) để tăng RAM (tránh OOM khi load `torch` + embedding) |
| 3 | **Account Settings → GitHub** → cấp quyền repo `BoneVisQA` |

---

## 3. Tạo Project & Service

| Cài đặt | Giá trị |
|---------|---------|
| **New Project** | Deploy from GitHub repo |
| **Repository** | `SP26SE110-BoneVisQA/BoneVisQA` (hoặc fork của bạn) |
| **Branch** | `main` |
| **Root Directory** | `BoneVisQA.AI` ← **bắt buộc** (monorepo) |

Railway đọc tự động:

- `railway.toml` — start command + healthcheck `/health`
- `runtime.txt` — Python **3.12.8** (máy dev có thể 3.14; cloud dùng 3.12 ổn định hơn)
- `Procfile` — dự phòng nếu builder dùng Heroku-style

**Start command** (đã trong `railway.toml`, kiểm tra lại trong UI):

```bash
uvicorn app.main:app --host 0.0.0.0 --port $PORT
```

---

## 4. Biến môi trường (Variables)

Mở file local `BoneVisQA.AI/.env` và copy từng giá trị vào **Service → Variables**.

Mẫu tên biến (xem `railway.variables.template`):

| Variable | Bắt buộc | Mô tả |
|----------|----------|--------|
| `DATABASE_URL` | Có | Chuỗi Postgres Supabase (`?sslmode=require`) |
| `SUPABASE_URL` | Có | `https://xxxx.supabase.co` |
| `SUPABASE_SERVICE_KEY` | Có | Service role key (Settings → API trong Supabase) |
| `HUGGINGFACE_API_KEY` | Khuyến nghị | Token HF (Settings → Access Tokens) — **chỉ dùng cho auth download** |
| `IMAGE_EMBEDDING_MODEL` | Không | Repo HF dạng `org/model`, ví dụ `microsoft/BiomedCLIP-PubMedBERT_256-vit_base_patch16_224`. **Không** dán token vào đây |
| `TEXT_EMBEDDING_MODEL` | Không | `sentence-transformers/all-mpnet-base-v2` (giữ chất lượng embedding). **Không** dán token vào đây |
| `ENRICH_BATCH_SIZE` | Khuyến nghị | **`12`** — balanced (ổn định + nhanh hơn batch 8) |
| `ENRICH_METADATA_BATCH_SIZE` | Không | `64` — metadata rule-based, không load ML |
| `ENCODE_BATCH_SIZE` | Khuyến nghị | **`6`** — batch nội bộ khi `encode_texts()` |
| `TORCH_NUM_THREADS` | Khuyến nghị | `2` — giảm spike RAM |
| `OMP_NUM_THREADS` | Không | `2` |
| `MKL_NUM_THREADS` | Không | `2` |

**Không** tạo biến `PORT` — Railway tự inject.

### Profile balanced cho document indexing (mpnet, mặc định 12/6)

Copy từ `railway.variables.template`. Log `Started server process [1]` + reload `MPNetModel` giữa batch = OOM → hạ về **8/4**. Ổn định nhiều lần có thể thử **16/8**.

| Mức | ENRICH / ENCODE | Ghi chú |
|-----|-----------------|---------|
| An toàn | 8 / 4 | Khi vẫn 502 |
| **Balanced** | **12 / 6** | Mặc định khuyến nghị |
| Nhanh hơn | 16 / 8 | Chỉ sau khi 12/6 ổn |

Sau khi thêm Variables → **Redeploy** (Deployments → ⋮ → Redeploy).

---

## 5. Domain public

**Settings → Networking → Generate Domain**

Domain hiện tại: `https://bonevisqa-production.up.railway.app`

Kiểm tra:

```text
GET https://<your-domain>/health
```

Kỳ vọng: `{"status":"ok"}`

Ghi URL này — dùng cho Backend C# ở bước 6.

---

## 6. Cập nhật Backend C# trên Render

Python chỉ được **C# gọi**, không qua FE trực tiếp.

Trên **Render** → Web Service **BoneVisQA.API** → **Environment**:

| Key (Render) | Value |
|--------------|--------|
| `AiMicroservice__BaseUrl` | `https://bonevisqa-production.up.railway.app` (không có `/` cuối) |
| `AiMicroservice__EnrichBatchSize` | `12` (khớp Railway `ENRICH_BATCH_SIZE`) |
| `AiMicroservice__EnrichMetadataBatchSize` | `64` |
| `AiMicroservice__RequestTimeoutMinutes` | `30` (giữ nguyên — indexing lớn cần thời gian) |

Repo đã cấu hình sẵn trong `appsettings.json`. Trên Render chỉ cần set biến này nếu bạn muốn **ghi đè** (ví dụ đổi domain sau này):

```text
AiMicroservice__BaseUrl=https://bonevisqa-production.up.railway.app
```

Save → Render restart.

Trong repo, `appsettings.json` vẫn có `localhost:8000` cho dev; production **ưu tiên** biến môi trường Render.

---

## 7. Kiểm tra end-to-end

1. Railway `/health` → `ok`
2. Render logs: không còn lỗi kết nối tới `localhost:8000`
3. FE sinh viên: Case Library → **Ask with AI** hoặc upload DICOM

Luồng: **FE (Vercel) → C# (Render) → Python (Railway) → Postgres/Supabase**

---

## 8. Lỗi thường gặp

| Log / triệu chứng | Cách xử lý |
|-------------------|------------|
| `ModuleNotFoundError: app` | Sai **Root Directory** — phải là `BoneVisQA.AI` |
| Build/startup OOM (CUDA / 1GB) | `requirements.txt` dùng `--extra-index-url` PyTorch **CPU-only**; nạp Billing Developer (~$5) để RAM tới ~8GB |
| `HTTP 502` khi enrich embeddings | Log Railway: `Started server process` + reload `all-mpnet-base-v2` giữa batch → OOM. Hạ `ENRICH_BATCH_SIZE=8`, `ENCODE_BATCH_SIZE=4`; redeploy Railway **và** Render. C# retry 6 lần, chờ 20s×attempt khi 502 |
| Indexing chậm nhưng OK | Bình thường với mpnet — balanced 12/6 ~14–17 phút cho 335 chunks (bước 5) |
| `could not connect to server` (DB) | Kiểm tra `DATABASE_URL`, pooler host, password |
| HF 401 / model download fail | Thêm `HUGGINGFACE_API_KEY` |
| `Repo id must use alphanumeric chars` / ingest 502 / `Failed initial config/weights load from HF Hub` | **`IMAGE_EMBEDDING_MODEL` (hoặc `TEXT_EMBEDDING_MODEL`) đang bị set nhầm thành HF token.** Xóa biến đó hoặc set đúng repo id (`microsoft/BiomedCLIP-...`). Token chỉ đặt ở `HUGGINGFACE_API_KEY`. Redeploy Railway |
| Deploy healthcheck fails / service unavailable | Lifespan was blocking on model download (>10 min). Redeploy after fix: `/health` returns immediately; models load in background. Check `/health/ready` when `warmup=ready`. First DICOM ingest may wait until BiomedCLIP finishes loading |

Dán **20–30 dòng cuối** tab Deploy Logs nếu cần hỗ trợ thêm.

---

## 9. Checklist nhanh

- [ ] Push `BoneVisQA.AI` lên GitHub
- [ ] Railway: Root Directory = `BoneVisQA.AI`
- [ ] Variables đủ (từ `.env` local)
- [ ] Generate Domain + `/health` OK
- [ ] Render: `AiMicroservice__BaseUrl` = URL Railway
- [ ] Test Ask with AI trên UI
