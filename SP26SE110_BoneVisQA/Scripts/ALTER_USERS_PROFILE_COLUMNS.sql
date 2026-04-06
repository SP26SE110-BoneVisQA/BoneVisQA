-- ============================================================
-- BoneVisQA – thêm cột thông tin cá nhân vào bảng users
-- Chạy trên PostgreSQL
-- ============================================================

ALTER TABLE public.users
  ADD COLUMN IF NOT EXISTS date_of_birth     DATE,
  ADD COLUMN IF NOT EXISTS phone_number      VARCHAR(256),
  ADD COLUMN IF NOT EXISTS gender            VARCHAR(32),
  ADD COLUMN IF NOT EXISTS student_id        VARCHAR(256),
  ADD COLUMN IF NOT EXISTS class_code        VARCHAR(256),
  ADD COLUMN IF NOT EXISTS address           TEXT,
  ADD COLUMN IF NOT EXISTS bio              TEXT,
  ADD COLUMN IF NOT EXISTS emergency_contact VARCHAR(256);

-- Khóa cũ (nếu bảng đã có index trùng)
-- DROP INDEX IF EXISTS idx_users_is_active;

-- Index cho tìm kiếm nhanh
CREATE INDEX IF NOT EXISTS idx_users_date_of_birth ON public.users (date_of_birth);
CREATE INDEX IF NOT EXISTS idx_users_gender        ON public.users (gender);
CREATE INDEX IF NOT EXISTS idx_users_class_code    ON public.users (class_code);

COMMENT ON COLUMN public.users.date_of_birth      IS 'Ngày sinh';
COMMENT ON COLUMN public.users.phone_number       IS 'Số điện thoại';
COMMENT ON COLUMN public.users.gender             IS 'Giới tính: Male, Female, Other, Prefer not to say';
COMMENT ON COLUMN public.users.student_id          IS 'Mã sinh viên / mã nhân sự';
COMMENT ON COLUMN public.users.class_code         IS 'Mã lớp học';
COMMENT ON COLUMN public.users.address            IS 'Địa chỉ';
COMMENT ON COLUMN public.users.bio                IS 'Giới thiệu bản thân';
COMMENT ON COLUMN public.users.emergency_contact IS 'Liên hệ khẩn cấp (tên & SĐT)';
