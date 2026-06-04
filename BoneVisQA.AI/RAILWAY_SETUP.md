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
| `HUGGINGFACE_API_KEY` | Khuyến nghị | Token HF (Settings → Access Tokens) |
| `IMAGE_EMBEDDING_MODEL` | Không | Mặc định BiomedCLIP trong code |
| `TEXT_EMBEDDING_MODEL` | Không | Mặc định `all-mpnet-base-v2` |

**Không** tạo biến `PORT` — Railway tự inject.

Sau khi thêm Variables → **Redeploy** (Deployments → ⋮ → Redeploy).

---

## 5. Domain public

**Settings → Networking → Generate Domain**

Ví dụ: `https://bonevisqa-ai-production.up.railway.app`

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
| `AiMicroservice__BaseUrl` | `https://<railway-domain>` (không có `/` cuối) |

Ví dụ:

```text
AiMicroservice__BaseUrl=https://bonevisqa-ai-production.up.railway.app
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
| Build OOM / process killed | Billing/RAM; deploy lại sau khi nạp |
| `could not connect to server` (DB) | Kiểm tra `DATABASE_URL`, pooler host, password |
| HF 401 / model download fail | Thêm `HUGGINGFACE_API_KEY` |
| Deploy chậm 10+ phút lần đầu | Bình thường — tải model embedding |

Dán **20–30 dòng cuối** tab Deploy Logs nếu cần hỗ trợ thêm.

---

## 9. Checklist nhanh

- [ ] Push `BoneVisQA.AI` lên GitHub
- [ ] Railway: Root Directory = `BoneVisQA.AI`
- [ ] Variables đủ (từ `.env` local)
- [ ] Generate Domain + `/health` OK
- [ ] Render: `AiMicroservice__BaseUrl` = URL Railway
- [ ] Test Ask with AI trên UI
