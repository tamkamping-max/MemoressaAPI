# Memoressa API

REST API backend for the Memoressa family memory platform, built with ASP.NET Core 8 and **Amazon RDS PostgreSQL** (production).

## Architecture

```
EC2 REST API  ──►  RDS PostgreSQL  (schema: database/migrations/*.sql)
              ──►  Private S3        (photos; keys in RDS)
Kestrel :7286   ──►  WSS `/ws/devices/{deviceId}` handled in ASP.NET (same port as REST)
Go (optional)   ──►  `api/internal/` HTTP on REST API
```

The solution follows a layered architecture:

```
Memoressa.Api            → HTTP controllers, middleware, auth, Swagger
Memoressa.Application    → Business logic, DTOs, service interfaces
Memoressa.Infrastructure → EF Core mapping, MySQL (local) / PostgreSQL RDS (release), S3, JWT, email, AI orchestration
Memoressa.Domain         → Entities and enums
```

Requests flow through middleware (exception handling, internal API key validation), JWT authentication, controllers, application services, and infrastructure adapters.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Production:** RDS PostgreSQL + EC2 (or container) in the same VPC
- **Local dev (optional):** [Docker](https://www.docker.com/) for MySQL 8

## Production: RDS PostgreSQL

1. Create an **RDS PostgreSQL** instance and empty database `memoressa`.
2. Apply schema from this repo (see [`database/README.md`](database/README.md)):

```bash
export PGHOST=your-instance.xxxxx.region.rds.amazonaws.com
export PGPORT=5432
export PGUSER=memoressa
export PGPASSWORD='your-password'
export PGDATABASE=memoressa
export PGSSLMODE=require

psql -v ON_ERROR_STOP=1 -f database/schema.sql
```

3. Configure the API on EC2:

```bash
export Database__Target=Rds
export ConnectionStrings__PostgreSql="Host=your-instance.xxxxx.region.rds.amazonaws.com;Port=5432;Database=memoressa;Username=memoressa;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --project src/Memoressa.Api
```

Configure production via environment variables (see below). Schema is **not** applied at API startup — deploy SQL to RDS explicitly (CI/CD or `./database/apply.sh`).

## Quick Start (local dev only)

Local development uses **MySQL 8** (`Database:Target` = **`Local`**, typically via `Database__Target=Local` when `ASPNETCORE_ENVIRONMENT=Development`). Release uses **RDS PostgreSQL** (`Database:Target` = **`Rds`**).

1. Start local MySQL:

```bash
docker compose up -d
```

2. Apply MySQL schema (from repo root):

```bash
./database/mysql/apply.sh
```

   On a **fresh** `docker compose` volume, `database/mysql/schema.sql` is applied automatically via `docker-entrypoint-initdb.d`.

   For an empty database in one shot: `mysql ... < database/mysql/schema.sql`

3. Run the API:

```bash
dotnet run --project src/Memoressa.Api
```

4. Run with the **`https`** launch profile so REST and Swagger use your LAN HTTPS port (see [Kestrel (local LAN dev)](#kestrel-local-lan-dev)).

Local schema is defined by **SQL files** in `database/mysql/migrations/` (see `database/mysql/README.md`). EF Core maps entities only; the API does not run `Database.Migrate()` on startup.

## Configuration

Configuration is loaded from `src/Memoressa.Api/appsettings.json` and environment-specific overrides.

### Kestrel (local LAN dev)

Development URLs come from `Properties/launchSettings.json` (`applicationUrl` on the **`https`** profile):

| Endpoint | URL |
|----------|-----|
| HTTP | `http://192.168.1.131:5047` |
| HTTPS | `https://192.168.1.131:7286` |

Replace `192.168.1.131` with your machine’s LAN address. Trust the ASP.NET dev HTTPS certificate on phones/tablets (`dotnet dev-certs https --trust` on the dev machine, or install the cert on the device).

REST base URL for the mobile app: `https://<lan-ip>:7286/api/v1/...`

### WebSocket (same port as HTTPS)

REST and display-device WebSocket share **one TLS port** on Kestrel:

| | URL |
|---|-----|
| REST | `https://192.168.1.131:7286/api/v1/...` |
| WSS | `wss://192.168.1.131:7286/ws/devices/{deviceId}` |

`GET /ws/devices/{deviceId}` with a WebSocket upgrade is handled in **Memoressa.Api** (`DeviceWebSocketEndpoints` → `InternalRealtimeService`). On connect the device is marked online; pending frame commands are pushed about every 2s as `{"type":"commands","commands":[...]}`. Client messages: `{"type":"ping"}` → `{"type":"pong"}`, `{"type":"ack","commandId":"<guid>","success":true}`.

Optional Go sidecars can still use `api/internal/` with `X-Internal-Api-Key`; they do not receive proxied traffic from the WSS URL above.

### Login fails with “transient failure”

That message almost always means **the API cannot reach the database** (not wrong email/password).

1. Check the API console on startup:
   - `Database:Target=Local (MySQL)` → local dev; need MySQL running.
   - `Database:Target=Rds (PostgreSQL)` → release mode; needs a reachable RDS (or local Postgres on port 5432).
2. **Local dev:** `docker compose up -d`, then `./database/mysql/apply.sh`, and run with **`ASPNETCORE_ENVIRONMENT=Development`** (or the **`https`** launch profile). Set **`Database__Target=Local`** so the API uses MySQL (the repo `appsettings.json` defaults to **`Rds`**).
3. If you run without `Database__Target=Local`, the API tries PostgreSQL on `ConnectionStrings:PostgreSql`, which often produces the transient failure on login/register.

### Database

| Key | Values | Description |
|-----|--------|-------------|
| `Database:Target` | `Local` / `Rds` | **`Local`** → MySQL (local dev). **`Rds`** → PostgreSQL on Amazon RDS (release). |

### ConnectionStrings

| Key | Used when | Description |
|-----|-----------|-------------|
| `ConnectionStrings:MySql` | `Database:Target=Local` | MySQL 8 (Pomelo). Default: `Server=localhost;Port=3306;Database=memoressa;User=memoressa;Password=memoressa` |
| `ConnectionStrings:PostgreSql` | `Database:Target=Rds` | RDS PostgreSQL (Npgsql). Include `SSL Mode=Require;Trust Server Certificate=true` |

`appsettings.json` sets `Target: Rds` (production default). Override to **`Local`** for MySQL via `Database__Target=Local` (environment or local user secrets — not committed).

RDS must be reachable from the API host (security group: EC2 → RDS on port 5432). Store credentials in environment variables or AWS Secrets Manager, not in git.

### Jwt

| Key | Description |
|-----|-------------|
| `Jwt:Issuer` | JWT issuer |
| `Jwt:Audience` | JWT audience |
| `Jwt:SecretKey` | Signing key (use a long random value in production) |
| `Jwt:AccessTokenMinutes` | Access token lifetime |
| `Jwt:RefreshTokenDays` | Refresh token lifetime |

### AwsS3

| Key | Description |
|-----|-------------|
| `AwsS3:Region` | AWS region |
| `AwsS3:BucketName` | S3 bucket for uploads |
| `AwsS3:AccessKey` | AWS access key (optional if using IAM/instance profile) |
| `AwsS3:SecretKey` | AWS secret key |
| `AwsS3:ServiceUrl` | Custom S3-compatible endpoint (optional) |
| `AwsS3:KeyPrefix` | Object key prefix |
| `AwsS3:PresignedUrlExpiryMinutes` | Presigned **GET** for photos in API responses (default **15** minutes) |
| `AwsS3:UploadPresignedUrlExpiryMinutes` | Presigned **PUT** for uploads + **`sessionId` / `expiresAt`** (default **10080** = 7 days) |
| `AwsS3:DownloadPresignedUrlExpiryMinutes` | Presigned GET for **`GET /photos/{id}/download`** (default **60** minutes) |
| `AwsS3:AiPresignedUrlExpiryMinutes` | Presigned GET expiry for server-side AI (Grok vision) |

### Private S3 media access

The bucket is **private**. The database stores only object keys:

- `Photo.S3Key` — original photo/video
- `Photo.ThumbnailS3Key` — optional dedicated thumbnail object (when absent, thumbnail URLs use `S3Key`)

`RemoteUrl` / `ThumbnailUrl` columns are legacy and are **not written** on upload complete.

When the app calls photo endpoints (`GET /api/v1/photos`, timeline, upload complete, etc.), the API generates **short-lived presigned GET URLs** and returns them as:

- `remoteUrl` — full image
- `thumbnailUrl` / `thumbnailPath` — dedicated thumbnail object (`thumb.jpg` next to the original in S3)

### Server-side thumbnail generation

After `POST /api/v1/uploads/{sessionId}/complete`, the API:

1. Downloads the original from private S3
2. Generates a JPEG thumbnail (max edge **480px**, quality **82** — same as MemoressaApp)
3. Uploads it to `{same-folder}/thumb.jpg` and stores `ThumbnailS3Key`

| Media | Processor |
|-------|-----------|
| `image/*` | ImageSharp resize → JPEG |
| `video/*` | ffmpeg frame at 1s → ImageSharp resize (requires `ffmpeg` on EC2) |

Configure via `AwsS3:ThumbnailMaxEdgePixels`, `ThumbnailJpegQuality`, `ThumbnailMaxSourceBytes`, `FfmpegPath`.

### AwsSes (password reset OTP via **SMTP**)

Create **SMTP credentials** in the AWS SES console (IAM → SMTP user — not the same as S3 access keys).

| Key | Description |
|-----|-------------|
| `AwsSes:Region` | SES region; default SMTP host `email-smtp.{region}.amazonaws.com` when `SmtpHost` is empty |
| `AwsSes:SmtpHost` | Optional override (e.g. `email-smtp.us-east-1.amazonaws.com`) |
| `AwsSes:SmtpPort` | Default **587** (STARTTLS) |
| `AwsSes:SmtpUsername` | SES SMTP username |
| `AwsSes:SmtpPassword` | SES SMTP password |
| `AwsSes:FromEmail` | Verified sender address (required in production) |
| `AwsSes:FromDisplayName` | Display name (default `Memoressa`) |
| `AwsSes:ConfigurationSetName` | Optional; sent as SMTP header `X-SES-CONFIGURATION-SET` |

**Log says SMTP accepted but no email?** That only means SES SMTP returned success to the API. Common causes: message in **spam/junk**; account still in **SES sandbox** (verify both **From** and recipient **To** in SES console, or request production access); **`FromEmail` / domain not verified** in the same region as `AwsSes:Region`; SMTP credentials created in a **different region** than `SmtpHost`; corporate mailbox delay. Check SES → Account dashboard → Sending statistics / suppression list.

Migration **`014_password_reset_codes.sql`**. **Local MySQL:** run `database/mysql/migrations/014_password_reset_codes.sql` — **not** `database/migrations/` (that tree is **PostgreSQL** syntax).

### OAuth Apple

| Key | Description |
|-----|-------------|
| `OAuth:Apple:ClientId` | iOS **bundle id** (JWT `aud`) for Sign in with Apple |

Upload flow (photos only — **no video** as the main asset):

1. `POST /api/v1/uploads/start` with `fileSizeBytes` = **original still** size, plus **`originalFileName`** / **`originalContentType`** (e.g. `IMG_1234.HEIC`, `image/heic`). Optional **`privacyScope`**: `onlySelf` | `family` | `friends` | `friendsAndFamily` | `custom` (App may also send Memory Visibility aliases `private` → onlySelf, `specificMembers` → custom). Optional **`compressedUsesFullOriginal`: true** — skip separate compressed PUT; response omits `uploads.compressed`; on **complete**, `Photo.S3Key` (display/`remoteUrl`) points at the **full original** object (same key as `S3KeyFull`). Otherwise returns **three** presigned PUT URLs (`full`, `compressed`, `thumbnail`), optional **`livePhotoVideo`**, plus **`presignedUrlExpiryMinutes`** and **`expiresAt`** (UTC).
2. Client PUTs `full` with the **original** file (HEIC/JPEG/PNG), PUTs compressed + thumbnail JPEGs unless `compressedUsesFullOriginal` (then PUT **full + thumbnail** only). If the user chose Live Photo (`isLivePhoto: true`), also PUT the companion **`.mov`/`.mp4`** to `livePhotoVideo`.
3. `POST /api/v1/uploads/{sessionId}/complete` after S3 Head checks pass → creates `Photo`; optional JSON body **`{ "description", "location" }`** (e.g. EXIF/caption from App — omitted or blank → `null`). **`CloudStorageUsedBytes` increases by still original + Live video** (when uploaded). Compressed/thumbnail JPEGs are not counted toward the 1 GiB quota.
4. `GET /api/v1/storage/usage` → `{ usedBytes, limitBytes }`

**User choice:** set `isLivePhoto: false` (default) to upload **only** the still original; set `isLivePhoto: true` and pass `livePhotoVideoFileSizeBytes` to include the paired video in storage and quota.

Quota is checked at `start` (includes pending sessions). `complete` returns **409** if variants are missing; **413** if quota exceeded.

`PhotoDto` presigned GET: `remoteUrl` = compressed, `thumbnailUrl` = nail, `fullUrl` = stored original object (for preview only — use **`GET /photos/{id}/download`** to save the true original filename/type). **`uploadedBy`** is always the uploader user id; **`uploaderNickname`**, **`uploaderEmail`**, and nested **`uploader`** `{ id, nickname, email }` are included when the account has displayable nickname/email (omit when blank so the App can hide “Uploaded by”). **`isLivePhoto`** on the photo indicates the user opted in at upload; **`GET /photos/{id}/download`** returns the still presigned URL plus, when available, **`livePhotoVideoDownloadUrl`** for the companion `.mov`/`.mp4`. The API does **not** return a single Apple Live Photo bundle: the **MemoressaApp must download both files** and use platform APIs (e.g. iOS `PHAssetCreationRequest` with paired video and matching content identifiers in the original HEIC/MOV) to restore a Live Photo in the camera roll. Saving only `downloadUrl` always produces a **still photo**.

**`POST /api/v1/photos/today-memories`** resolves the caller’s family from the JWT (`sub` + `family_id` claim validated against `family_memberships`) and only returns photos that family (and the viewer may see, e.g. not another member’s `onlySelf` uploads).

**Photo user tags vs AI tags:** `PhotoDto.userTags` = user-selected tags (photo experience chips). `PhotoDto.aiTags` = AI-generated tags in `photo_ai_tags` only. **`PUT /api/v1/photos/{id}`** accepts **`userTags`** and/or legacy **`aiTags`** in the body as a **full replace** of user tags on **that photo only**; **`photo_ai_tags` is never modified** by this endpoint. JWT access tokens include a **`family_id`** claim (same as login `familyId`). **MemoressaApp** should bind the tag UI to **`userTags`** only (do not treat `aiTags` as user tags when `userTags` is present, even if empty).

**Photo user tag library (tags sheet — per user, reusable across photos):**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/photo-tags/library` | List the signed-in user’s saved custom tag labels |
| POST | `/api/v1/photo-tags/library` | Add a label to the library (`{ "tag": "..." }`; idempotent if the same tag already exists, case-insensitive) |
| DELETE | `/api/v1/photo-tags/library/{id}` | Remove a label from the **library picker only** — does **not** remove that string from `photo_user_tags` on photos already tagged |

Preset/built-in tags (e.g. client `defaultPhotoUserTagIds`) stay **client-only**. Library entries are **user-added** strings the App shows with a deletable **X** in the tags sheet. Removing a tag on a photo (experience / fullscreen chip **X**) is still **`PUT /api/v1/photos/{id}`** with a reduced `userTags` list — **never** library DELETE.

Migration **`016_user_photo_tag_library.sql`** (MySQL: `database/mysql/migrations/016_user_photo_tag_library.sql`). Requires **`015_photo_user_tags_and_comments.sql`** for per-photo tags.

**Photo albums (experience / MediaGroup):** Album-level `userTags`, `description`, `visibility`, `memberIds`, and comments. Photos keep file metadata and `aiTags`. **`POST /api/v1/photo-albums`** (alias **`POST /api/v1/photo-albums/ensure`**, same body/response) with `{ photoIds[] }` **find-or-create** by canonical photo-set fingerprint (sorted photo ids, SHA-256) within the JWT family. Response includes `created: true|false`. **`DELETE /api/v1/photo-albums/{id}`** removes the album row only (photos are not deleted). **`GET /api/v1/photos/{id}`** may include `albumId`, `albumUserTags`, `albumDescription` when the photo belongs to an album (most recently updated album wins). **`GET /api/v1/photo-albums`** returns paginated **card** items (`albumId`, `photoIds`, `userTags`, `description`, `coverPhotoId`). Migration **`017_photo_albums.sql`**. Optional legacy backfill notes: **`018_photo_albums_backfill_note.sql`**.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/v1/photo-albums` | Card list (`items`, `nextCursor`, `hasMore`) |
| POST | `/api/v1/photo-albums` | Create or find `{ photoIds, userTags?, description?, visibility?, memberIds? }` |
| POST | `/api/v1/photo-albums/ensure` | Same as POST `/` (find-or-create alias) |
| GET | `/api/v1/photo-albums/{id}` | Detail + optional `photos[]` summaries |
| PUT | `/api/v1/photo-albums/{id}` | Update tags, description, visibility, members, cover |
| DELETE | `/api/v1/photo-albums/{id}` | Delete album metadata only |
| PATCH | `/api/v1/photo-albums/{id}/photos` | `{ addPhotoIds?, removePhotoIds? }` |
| DELETE | `/api/v1/photo-albums/{id}/photos` | Body `{ removePhotoIds[] }` — unlink from album only (photos/S3 unchanged) |
| GET/POST/DELETE | `/api/v1/photo-albums/{albumId}/comments` | Same rules as photo comments (`isMine` on list); author delete |

**Photo comments (MemoressaApp photo experience):**

| Method | Path | Body |
|--------|------|------|
| GET | `/api/v1/photos/{photoId}/comments` | — |
| POST | `/api/v1/photos/{photoId}/comments` | `{ "message": "..." }` |
| DELETE | `/api/v1/photos/{photoId}/comments/{commentId}` | — (author only) |

Migration **`015_photo_user_tags_and_comments.sql`** (MySQL: `database/mysql/migrations/015_photo_user_tags_and_comments.sql`).

See **Photo user tag library** above for tags sheet persistence (`016`).

**Photo download / delete (batch):** **`GET /photos/{id}/download`** and **`POST /photos/download-batch`** return presigned URLs plus optional **`contentLength`** / **`livePhotoVideoContentLength`** (from DB or S3 HeadObject) for App byte progress. Live Photo still uses still + video URLs (App may split progress 50/50). **`POST /photos/delete-batch`** `{ photoIds[] }` → `{ deletedIds, failures[] }` per-id partial success (max **50** ids). **`DELETE /photos/{id}`** still deletes the photo entity and S3 objects — not the same as unlinking from a photo album.

For production at scale, you can swap presigned GET for **CloudFront signed URLs** inside `IPhotoUrlResolver` without changing the REST contract.

### Ai (xAI Grok)

LLM calls use xAI **`POST /v1/chat/completions`** (Chat Completions API) by default.

| Key | Description |
|-----|-------------|
| `Ai:GrokApiKey` | **xAI API key** ([console.x.ai](https://console.x.ai)). Also set env **`XAI_API_KEY`** if this is empty. Legacy **`Ai:OpenAiApiKey`** is still read when `GrokApiKey` is empty. |
| `Ai:GrokBaseUrl` | Default **`https://api.x.ai/v1`**. Legacy **`Ai:OpenAiBaseUrl`** is used when `GrokBaseUrl` is empty. |
| `Ai:ChatModel` | AI Agent chat (default **`grok-4.7`**) |
| `Ai:VisionModel` | Batch photo vision (default **`grok-4.7`**) |
| `Ai:AgentMaxHistoryMessages` | Multi-turn history sent to the LLM (default **4**) |
| `Ai:AgentMaxContextMemories` | Max photo candidates in RAG context (default **3**) |
| `Ai:AgentGrokSearchExpansion` | Extra Grok call to expand multilingual search keywords (default **false** — one Grok call per Agent chat) |
| `Ai:AgentMaxSearchKeywords` | Max Grok-expanded keywords merged into substring search (default **16**) |
| `Ai:AgentMaxSearchTermsForDb` | Max synonym strings per DB search; uses user term + one cross-locale synonym (default **2**) |
| `Ai:AgentSkipGrokForSimpleSearch` | Short tag-like queries use search-only replies (**0** xAI tokens; default **true**) |
| `Ai:AgentMaxCompletionTokens` | Cap on Grok completion tokens for Agent JSON (default **256**) |
| `Ai:EnableVisionBatch` | Enable batch vision processing |
| `Ai:MaxBatchSize` | Max photos per batch |

### InternalApi

| Key | Description |
|-----|-------------|
| `InternalApi:ApiKey` | Shared secret for `/api/internal/*` routes |
| `InternalApi:CommandPollBatchSize` | Max frame commands returned per poll |

### OAuth

| Key | Description |
|-----|-------------|
| `OAuth:Google:ClientId` | Google OAuth client ID |
| `OAuth:Google:ClientSecret` | Google OAuth client secret |
| `OAuth:Facebook:AppId` | Facebook app ID |
| `OAuth:Facebook:AppSecret` | Facebook app secret |

When Google/Facebook credentials are configured, ASP.NET Core external auth handlers are registered. OAuth login endpoints validate tokens via Google tokeninfo or Facebook Graph API when configured; otherwise MVP stub validation is used.

### Environment Variables

Any setting can be overridden with environment variables using `__` nesting, for example:

```bash
# Local MySQL
export Database__Target=Local
export ConnectionStrings__MySql="Server=localhost;Port=3306;Database=memoressa;User=memoressa;Password=memoressa"

# RDS (production)
export Database__Target=Rds
export ConnectionStrings__PostgreSql="Host=your-instance.xxxxx.region.rds.amazonaws.com;Port=5432;Database=memoressa;Username=memoressa;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
export Jwt__SecretKey="your-production-secret-key"
export InternalApi__ApiKey="your-internal-key"
export XAI_API_KEY="xai-..."
# or: export Ai__GrokApiKey="$XAI_API_KEY"
export Ai__ChatModel="grok-4.7"
export Ai__VisionModel="grok-4.7"
export Ai__GrokBaseUrl="https://api.x.ai/v1"
```

## Authentication

Most endpoints require a JWT Bearer token:

```
Authorization: Bearer <access_token>
```

Obtain tokens via `POST /api/v1/auth/login`, `POST /api/v1/auth/register`, or OAuth endpoints.

Internal endpoints under `/api/internal/*` do not use JWT. They require:

```
X-Internal-Api-Key: <InternalApi:ApiKey>
```

## API Endpoints

All public REST endpoints use the prefix `api/v1/`. Internal Go WebSocket integration uses `api/internal/`.

### Auth — `api/v1/auth`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/register` | Public | Register: `{ "email", "password", "nickname"? }` (no `confirmPassword`) |
| POST | `/login` | Public | Login with email/password |
| POST | `/refresh` | Public | Refresh access token |
| POST | `/logout` | JWT | Revoke refresh token |
| POST | `/password-reset/request` | Public | **Legacy** link-based reset (optional; App uses OTP below) |
| POST | `/password-reset/confirm` | Public | Legacy reset with `token` from email link |
| POST | `/password-reset/code/request` | Public | Send **8-digit OTP** (15 min) via **AWS SES**; `{ "email" }`. Unknown email → **404** `找不到此 email`. Rate limit: 1/min, 5/hour per user. Success **204** (never return code in JSON). |
| POST | `/password-reset/code/verify` | Public | `{ "email", "code" }` — valid → **204**; wrong/expired → **401** |
| POST | `/password-reset/code/confirm` | Public | `{ "email", "code", "newPassword", "confirmPassword" }` — updates password, invalidates code → **204** |
| POST | `/account/deletion/schedule` | JWT | Schedule account deletion |
| POST | `/account/deletion/cancel` | JWT | Cancel scheduled deletion |
| GET | `/me` | JWT | Get current user |
| GET | `/account/deletion/status` | JWT | Get deletion status |
| POST | `/oauth/google` | Public | Login/register with Google id token |
| POST | `/oauth/facebook` | Public | Login/register with Facebook access token |
| POST | `/oauth/apple` | Public | Sign in with Apple: `{ "identityToken" }` — validates Apple JWT (`iss`, `aud` = `OAuth:Apple:ClientId` bundle id). Response same as Google: `{ user, tokens, familyId }`. Dev: `identityToken` = `demo-apple-token`. |

### Memories — `api/v1/memories`

**MemoressaApp 的「回憶」分頁不走這組 API。** 首頁「今日回憶」與「回憶」頁皆使用 **`POST /api/v1/photos/today-memories`**（同一 use case、同一 `today_memories_cache`）。下列 endpoints 用於 **使用者手工建立的 memory 相簿**（`memories` 表）、相框、AI 搜尋上下文等。

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | User-created memory albums only (`POST /`). Excludes `IsAiGenerated`, `IsTodayHighlight`, and `type: aiMemory`. Same scope on `POST /filter` and `GET /years-ago-today`. |
| GET | `/ai-curated` | AI-curated memories |
| GET | `/today` | Today's memories |
| POST | `/today/regenerate` | Regenerate today highlight |
| GET | `/years-ago-today` | On-this-day memories |
| GET | `/{id}` | Get memory by ID |
| POST | `/` | Create memory |
| PUT | `/{id}` | Update memory |
| DELETE | `/{id}` | Delete memory |
| POST | `/filter` | Filter memories |

### Photos — `api/v1/photos`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List photos |
| GET | `/{id}` | Get photo by ID |
| GET | `/{id}/download` | Presigned GET for **true original** still (`downloadUrl`, `fileName`, `contentType`). When Live Photo was uploaded: `isLivePhoto`, `livePhotoVideoAvailable`, optional `livePhotoVideoDownloadUrl` + companion name/type (default 60 min expiry). Client must save **both** to restore Live Photo on iOS. |
| DELETE | `/{id}` | Delete photo: S3 + DB row; quota; **`memory_photos` cascade** for manual memory albums; **prune** deleted `photoId` from all family **`today_memories_cache`** (updates App 今日回憶 + 回憶頁 snapshot) |
| GET | `/by-date/{date}` | Photos by date |
| GET | `/by-member/{memberId}` | Photos by family member |
| PUT | `/{id}` | Update photo metadata |
| POST | `/{id}/hide` | Hide photo |
| GET | `/timeline` | Timeline photos (cursor pagination; default `limit=20`, max 50). Query: `?limit=20&cursor=...`. Response: `{ items, nextCursor, hasMore }` |
| POST | `/today-memories` | **MemoressaApp：首頁「今日回憶」+「回憶」頁**（同一 API、同一 per-family/per-day cache）。Lazy compose on first successful call each calendar day; later calls return `fromCache: true` unless empty-cache refresh (≥ 4 eligible photos). Body: optional `date`, `currentLocation`, travel, `occasions` — **only affects the first compose that day** (whichever screen calls first). Response: `{ items, strategy, referenceDate, fromCache }`. Does **not** create `memories` table rows. |

### Activities — `api/v1/activities`

MemoressaApp: upload settings picker, home carousel, create on upload confirm, edit in modal. All routes require JWT family scope.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/in-progress` | Link existing activity modal. **200** `{ data: { items: [ActivityAlbum...] } }` (empty `items: []`, never 404). Only `status=inProgress`, caller has access, creator account active. |
| GET | `/active-today?date=yyyy-MM-dd&limit=8` | Home carousel. **200** `{ data: { strategy, items: [{ sortRank, subtitle, activity, photos }] } }`. `date` defaults UTC today. Active on `date`: `inProgress` and `startDate ≤ date` and (`endDate` null or `date ≤ endDate`). Sort: creator tier first, then start-date proximity. `photos` up to 12 previews from uploads linked via `activityAlbumId`. |
| POST | `/` | Create activity. Body: `title` (required), `type` or `activityType` (travel \| wedding \| conference \| concert \| gathering \| **other**), `status`, `startDate`, optional `endDate`, `location`, `familyMemberIds`, `friendIds`, `agenda[]` (`title` required per item). **`creatorUserId`** defaults to JWT user if omitted. Unknown enum strings → **400**. |
| PUT | `/{activityId}` | Full update (same body as POST). `activityId` = external id (`act_...`). Returns updated DTO including `creatorUserId` and agenda ids. |
| POST | `/{activityId}/photos` | Attach `{ photoIds: [...] }` |

`POST /uploads/start` optional **`activityAlbumId`** (`act_...`): activity must exist, **`inProgress`**, caller **`CanUploadTo`**; links photo on **complete** (feeds `active-today` photos).

Response activity fields (camelCase): `id`, `title`, `type`, `activityType` (mirror of `type`), `status`, `startDate`, `endDate`, `location`, **`creatorUserId`**, `familyMemberIds`, `friendIds`, `agenda`, optional `coverPhotoId`.

Enums (JSON camelCase): `type` = travel \| wedding \| conference \| concert \| gathering \| **other**; `status` = inProgress \| completed \| cancelled.

Migration **`009_activity_albums.sql`**.

### Family Members — `api/v1/family-members`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List family members |
| GET | `/{id}` | Get member by ID |
| POST | `/` | Add family member |
| PUT | `/{id}` | Update member |
| DELETE | `/{id}` | Delete member |
| GET | `/by-generation/{generation}` | Members by generation |

### Family Moments — `api/v1/family-moments`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List family moments |
| POST | `/` | Create moment |
| PUT | `/{id}` | Update moment |

### Display Devices — `api/v1/display-devices`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List display devices |
| POST | `/` | Create device |
| POST | `/bind` | Bind device via QR code |
| PUT | `/{id}/rename` | Rename device |
| POST | `/{id}/unbind` | Unbind device |
| POST | `/{deviceId}/send-memory` | Send memory to frame |
| GET | `/qr-code` | Generate binding QR code |

### AI — `api/v1/ai`

| Method | Path | Description |
|--------|------|-------------|
| POST | `/analyze-photos` | Analyze photos with AI |
| GET | `/search?query=` | Semantic memory search |
| POST | `/playback` | Generate playback playlist |
| POST | `/memories` | Create AI-generated memory (`IsAiGenerated`; listed under `GET /ai-curated`, not **我的回憶** `GET /memories`) |
| POST | `/inferences/{photoId}/confirm?memberId=` | Confirm AI member inference |
| POST | `/inferences/{photoId}/reject` | Reject AI inference |

### AI Agent (Grok chat) — `api/v1/ai/agent`

Conversational assistant for the MemoressaApp top-left AI entry. Uses Grok Chat Completions with family memory search as RAG context.

**MemoressaApp:** On opening the AI screen, call **`GET /session`** to restore the latest transcript and pass **`sessionId`** on subsequent **`POST /chat`** calls. When **`sessionId`** is omitted on chat, the API reuses the latest session for that user/family (no new empty session per visit). Do not show preset example chips; history replaces an empty-state welcome when messages exist.

**Agent memory search (before each Grok call):** substring SQL on memories, photos, and photo albums (typically **2** DB term passes: user word + one cross-locale synonym). **Short tag queries** (e.g. `happy`, `開心`) with **`AgentSkipGrokForSimpleSearch: true`** (default) return templated replies with **no** xAI call. Longer or question-style messages use **one** **`chat/completions`** call. Set **`Ai:AgentGrokSearchExpansion`** to **true** for an extra Grok keyword-expansion call.

| Method | Path | Description |
|--------|------|-------------|
| POST | `/chat` | Send a message; returns natural-language reply + memory/photo links |
| GET | `/session` | **MemoressaApp on enter:** latest chat session for the user/family + full message history (`sessionId` null when none) |
| GET | `/sessions/{sessionId}/messages` | Reload a chat session history |

**POST `/chat` request**

```json
{
  "message": "找妈妈2018夏天的照片",
  "sessionId": "optional-uuid-for-multi-turn",
  "locale": "zh-TW"
}
```

**Response**

```json
{
  "sessionId": "uuid",
  "reply": "我找到了…",
  "memoryId": "uuid",
  "photoId": "uuid",
  "thumbnailUrl": "presigned-get-url",
  "matchReasonKeys": ["familyRelation", "semantic"],
  "relatedMemories": [ { "photoId": "...", "memoryId": "...", "photoAlbumId": "...", "title": "...", "thumbnailPath": "...", "matchReasons": [], "relevanceScore": 0.9 } ]
}
```

Requires **`Ai:GrokApiKey`** or **`XAI_API_KEY`**. Uses xAI Grok via Chat Completions (`response_format: json_object` for structured agent replies). When no key is configured, the API falls back to search-only templated replies (no LLM).

### Settings — `api/v1/settings`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/locale` | Get user locale |
| PUT | `/locale` | Set locale |
| GET | `/onboarding` | Get onboarding status |
| PUT | `/onboarding` | Set onboarding complete |
| GET | `/ai` | Get AI feature toggles |
| PUT | `/ai` | Update AI settings |

### Friends — `api/v1/friends`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List friends |
| POST | `/` | Add friend |
| PUT | `/{id}` | Update friend |
| DELETE | `/{id}` | Delete friend |

### Journal Tags — `api/v1/journal-tags`

User-scoped custom journal tags (built-in tags remain client-side). Returns only tags stored for the authenticated user.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List custom journal tags |
| POST | `/` | Create custom tag (`labelKey`, `colorArgb`) |
| PUT | `/{id}` | Update tag |
| DELETE | `/{id}` | Delete tag |

### Photo user tag library — `api/v1/photo-tags/library`

User-scoped reusable labels for the photo tags sheet (not journal tags). See Photos section for interaction with `PUT /photos/{id}` `userTags`.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List library entries `{ id, tag, createdAt }` |
| POST | `/` | Create library entry `{ tag }` |
| DELETE | `/{id}` | Remove from library (photos keep existing assignments) |

`FrameCommentDto` also includes `authorName` and `authorAvatarUrl` (populated from the comment author's profile).

### Shared Albums — `api/v1/shared-albums`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List shared albums |
| GET | `/external/{externalId}` | Get album by external ID |
| POST | `/` | Create shared album |

### Uploads — `api/v1/uploads`

| Method | Path | Description |
|--------|------|-------------|
| POST | `/start` | Photo upload session (3 presigned PUTs; rejects video) |
| GET | `/incomplete` | Pending sessions for current user (not expired) |
| POST | `/{sessionId}/complete` | Verify S3 variants exist, create photo, apply quota. Optional body: `{ "description"?, "location"? }` written on insert. |

### Storage — `api/v1/storage`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/usage` | `{ usedBytes, limitBytes }` per user (1 GiB default) |

### Notifications — `api/v1/notifications`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List notifications |
| POST | `/{id}/read` | Mark notification read |
| POST | `/read-all` | Mark all notifications read |

### Frame — `api/v1/frame`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/devices/{deviceId}/playback-packages` | List playback packages |
| POST | `/devices/{deviceId}/playback-packages` | Create playback package |
| POST | `/devices/{deviceId}/playback-packages/ensure` | Upsert package by `externalId` (maps client ids like `remote_pkg_*` to server GUID) |
| GET | `/playback-packages/{packageId}/comments` | List frame comments (requires server package GUID) |
| POST | `/playback-packages/{packageId}/comments` | Add frame comment |

`FramePlaybackPackage` stores optional `externalId` (unique per device). Comments always target the server `id` (GUID); the Flutter app calls **ensure** first when the local package id is not a GUID.

### Internal — `api/internal` (API key, no JWT)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/devices/status` | Update device online status |
| GET | `/devices/{deviceId}/commands` | Poll pending frame commands |
| POST | `/commands/{commandId}/ack` | Acknowledge command execution |
| GET | `/devices/{deviceId}/playback-packages` | Get device playback packages |

## Error Responses

Application errors return JSON:

```json
{ "error": "Human-readable message" }
```

Unhandled exceptions are caught by `ExceptionHandlingMiddleware` and returned as HTTP 500 with the same shape.

## Flutter App — Remote API Toggle

The Flutter client (`MemoressaApp/`) ships with a REST layer that is **off by default** (local mock data). Enable it at build time:

```bash
flutter run \
  --dart-define=USE_REMOTE_API=true \
  --dart-define=API_BASE_URL=http://10.0.2.2:5000
```

When enabled, these formerly mock areas call the REST API:

| Feature | Endpoints |
|---------|-----------|
| Auth / OAuth | `POST /api/v1/auth/login`, `/oauth/google`, `/oauth/facebook` |
| Friends | `GET/POST/PUT /api/v1/friends` |
| Journal tags | `GET/POST /api/v1/journal-tags` (+ built-in tags client-side) |
| Frame comments | `GET/POST /api/v1/frame/playback-packages/{guid}/comments` (+ `POST .../playback-packages/ensure` for `remote_pkg_*`) |
| Frame playback | `GET /api/v1/frame/devices/{deviceId}/playback-packages` |

OAuth still uses placeholder tokens (`demo-google-token`, `demo-facebook-token`) until native SDKs are wired; the API accepts these in dev when real Google/Facebook credentials are not configured.

## Build & Test

```bash
dotnet build
dotnet test
```

## Project Structure

```
/workspace
├── database/
│   ├── migrations/        # Incremental .sql schema for RDS PostgreSQL
│   ├── schema.sql         # Full schema (greenfield RDS apply)
│   ├── apply.sh           # Apply migrations to PostgreSQL
│   ├── mysql/
│   │   ├── migrations/    # Incremental .sql schema for local MySQL
│   │   ├── schema.sql     # Full schema (greenfield local apply)
│   │   ├── apply.sh       # Apply migrations to MySQL
│   │   └── README.md
│   └── README.md
├── docker-compose.yml
├── Memoressa.sln
├── src/
│   ├── Memoressa.Api/
│   ├── Memoressa.Application/
│   ├── Memoressa.Domain/
│   └── Memoressa.Infrastructure/
├── tests/
│   └── Memoressa.Api.Tests/
└── MemoressaApp/          # Flutter mobile app (separate client)
```

## Database schema

**RDS (release):** PostgreSQL schema in **`database/migrations/*.sql`**.

**Local (dev):** MySQL schema in **`database/mysql/migrations/*.sql`**.

| PostgreSQL (RDS) | MySQL (local) | Contents |
|------------------|---------------|----------|
| `database/migrations/001_initial.sql` | `database/mysql/migrations/001_initial.sql` | Users, families, photos, memories, frame, uploads, … |
| `002_ai_chat_sessions.sql` | `002_ai_chat_sessions.sql` | AI agent chat |
| `003_journal_tags.sql` | `003_journal_tags.sql` | Journal tags |
| `004_frame_package_external_id.sql` | `004_frame_package_external_id.sql` | Frame package `external_id` |
| `005_upload_variants_and_storage_quota.sql` | `005_upload_variants_and_storage_quota.sql` | Upload variant keys + user storage quota |
| `019_ai_chat_related_photos.sql` | `019_ai_chat_related_photos.sql` | AI agent assistant `RelatedPhotoIdsJson` (multi-photo hits) |

After changing schema, update **both** SQL trees, then `Memoressa.Domain` entities and `Infrastructure/Configurations`, then deploy SQL before/with the API.

```bash
./database/mysql/apply.sh    # local MySQL
./database/apply.sh          # RDS PostgreSQL
# or: mysql ... < database/mysql/schema.sql
# or: psql -f database/schema.sql
```
