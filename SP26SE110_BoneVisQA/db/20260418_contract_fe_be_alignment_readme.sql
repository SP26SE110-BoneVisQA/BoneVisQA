-- BoneVisQA: FE/BE contract alignment (2026-04-18)
-- Schema notes + sanity checks. Executable DDL for coordinates + target_url is included in
-- `20260416_supabase_be_sync_full.sql` (section 5, end of file).

-- 1) Visual QA thread ROI
--    - Session-level `roiBoundingBox` in API is derived from `qa_messages.coordinates` (latest User row with non-null JSON).
--    - Per-turn `questionCoordinates` maps the same `qa_messages.coordinates` on the user leg of each turn.
--    Supabase: `qa_messages.coordinates jsonb NULL` (see full sync script section 5)

-- 2) Notifications REST + SignalR
--    - Persisted: `notifications.target_url` (nullable text).
--    - Response DTO adds computed `route` (camelCase) = app-relative path; absolute URLs are normalized server-side.
--    Supabase: `notifications.target_url text NULL` (see full sync script section 5)

-- 3) Student recent activity (`GET .../recent-activity`)
--    - Uses `qa_messages` joined to `visual_qa_sessions`; returns `sessionId`, `targetUrl`, `activityType` = 'visual_qa'.

-- Optional Supabase / Postgres sanity checks (read-only):
-- SELECT column_name, data_type
-- FROM information_schema.columns
-- WHERE table_schema = 'public' AND table_name = 'qa_messages' AND column_name = 'coordinates';
-- SELECT column_name, data_type
-- FROM information_schema.columns
-- WHERE table_schema = 'public' AND table_name = 'notifications' AND column_name = 'target_url';
