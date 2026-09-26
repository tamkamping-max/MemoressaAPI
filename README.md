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

Template: `src/Memoressa.Api/appsettings.Production.json.example`

Schema is **not** applied at API startup — deploy SQL to RDS explicitly (CI/CD or `./database/apply.sh`).

## Quick Start (local dev only)

Local development uses **MySQL 8** (`Database:Target` = **`Local`** in `appsettings.Development.json`). Release uses **RDS PostgreSQL** (`Database:Target` = **`Rds`**).

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

Development binds Kestrel in `appsettings.Development.json` (this wins over `launchSettings.json` `applicationUrl` when both are set):

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
2. **Local dev:** `docker compose up -d`, then `./database/mysql/apply.sh`, and run with **`ASPNETCORE_ENVIRONMENT=Development`** (or the **`https`** launch profile) so `Database:Target` stays **`Local`**.
3. If you run without the Development profile, `appsettings.json` defaults to **`Rds`** and the API tries PostgreSQL on `localhost:5432`, which often produces the transient failure on login/register.

### Database

| Key | Values | Description |
|-----|--------|-------------|
| `Database:Target` | `Local` / `Rds` | **`Local`** → MySQL (local dev). **`Rds`** → PostgreSQL on Amazon RDS (release). |

### ConnectionStrings

| Key | Used when | Description |
|-----|-----------|-------------|
| `ConnectionStrings:MySql` | `Database:Target=Local` | MySQL 8 (Pomelo). Default: `Server=localhost;Port=3306;Database=memoressa;User=memoressa;Password=memoressa` |
| `ConnectionStrings:PostgreSql` | `Database:Target=Rds` | RDS PostgreSQL (Npgsql). Include `SSL Mode=Require;Trust Server Certificate=true` |

`appsettings.json` sets `Target: Rds` (production default). `appsettings.Development.json` overrides to `Target: Local` for MySQL.

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
| `AwsS3:AiPresignedUrlExpiryMinutes` | Presigned GET expiry for server-side AI (OpenAI Vision) |

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

Upload flow (photos only — **no video** as the main asset):

1. `POST /api/v1/uploads/start` with `fileSizeBytes` = **original still** size, plus **`originalFileName`** / **`originalContentType`** (e.g. `IMG_1234.HEIC`, `image/heic`). Optional **`privacyScope`**: `onlySelf` | `family` | `friends` | `friendsAndFamily` | `custom` (App may also send Memory Visibility aliases `private` → onlySelf, `specificMembers` → custom). Optional **`compressedUsesFullOriginal`: true** — skip separate compressed PUT; response omits `uploads.compressed`; on **complete**, `Photo.S3Key` (display/`remoteUrl`) points at the **full original** object (same key as `S3KeyFull`). Otherwise returns **three** presigned PUT URLs (`full`, `compressed`, `thumbnail`), optional **`livePhotoVideo`**, plus **`presignedUrlExpiryMinutes`** and **`expiresAt`** (UTC).
2. Client PUTs `full` with the **original** file (HEIC/JPEG/PNG), PUTs compressed + thumbnail JPEGs unless `compressedUsesFullOriginal` (then PUT **full + thumbnail** only). If the user chose Live Photo (`isLivePhoto: true`), also PUT the companion **`.mov`/`.mp4`** to `livePhotoVideo`.
3. `POST /api/v1/uploads/{sessionId}/complete` after S3 Head checks pass → creates `Photo`; **`CloudStorageUsedBytes` increases by still original + Live video** (when uploaded). Compressed/thumbnail JPEGs are not counted toward the 1 GiB quota.
4. `GET /api/v1/storage/usage` → `{ usedBytes, limitBytes }`

**User choice:** set `isLivePhoto: false` (default) to upload **only** the still original; set `isLivePhoto: true` and pass `livePhotoVideoFileSizeBytes` to include the paired video in storage and quota.

Quota is checked at `start` (includes pending sessions). `complete` returns **409** if variants are missing; **413** if quota exceeded.

`PhotoDto` presigned GET: `remoteUrl` = compressed, `thumbnailUrl` = nail, `fullUrl` = stored original object (for preview only — use **`GET /photos/{id}/download`** to save the true original filename/type). **`isLivePhoto`** on the photo indicates the user opted in at upload; **`GET /photos/{id}/download`** returns the still presigned URL plus, when available, **`livePhotoVideoDownloadUrl`** for the companion `.mov`/`.mp4`. The API does **not** return a single Apple Live Photo bundle: the **MemoressaApp must download both files** and use platform APIs (e.g. iOS `PHAssetCreationRequest` with paired video and matching content identifiers in the original HEIC/MOV) to restore a Live Photo in the camera roll. Saving only `downloadUrl` always produces a **still photo**.

For production at scale, you can swap presigned GET for **CloudFront signed URLs** inside `IPhotoUrlResolver` without changing the REST contract.

### Ai

| Key | Description |
|-----|-------------|
| `Ai:OpenAiApiKey` | OpenAI API key |
| `Ai:ChatModel` | OpenAI model for AI Agent chat (default `gpt-4o-mini`) |
| `Ai:AgentMaxHistoryMessages` | Multi-turn history sent to OpenAI |
| `Ai:AgentMaxContextMemories` | Max memory candidates in RAG context |
| `Ai:OpenAiBaseUrl` | OpenAI-compatible base URL |
| `Ai:VisionModel` | Vision model name |
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
export Ai__OpenAiApiKey="sk-..."
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
| POST | `/register` | Public | Register a new account |
| POST | `/login` | Public | Login with email/password |
| POST | `/refresh` | Public | Refresh access token |
| POST | `/logout` | JWT | Revoke refresh token |
| POST | `/password-reset/request` | Public | Request password reset email |
| POST | `/password-reset/confirm` | Public | Reset password with token |
| POST | `/account/deletion/schedule` | JWT | Schedule account deletion |
| POST | `/account/deletion/cancel` | JWT | Cancel scheduled deletion |
| GET | `/me` | JWT | Get current user |
| GET | `/account/deletion/status` | JWT | Get deletion status |
| POST | `/oauth/google` | Public | Login/register with Google id token |
| POST | `/oauth/facebook` | Public | Login/register with Facebook access token |

### Memories — `api/v1/memories`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List all memories |
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
| DELETE | `/{id}` | Delete photo: remove S3 objects; delete DB row (cascades `memory_photos`, etc.); decrease uploader quota; **prune** deleted id from all `today_memories_cache` rows for the family; remove **我的回憶** entries that no longer contain any photo/video |
| GET | `/by-date/{date}` | Photos by date |
| GET | `/by-member/{memberId}` | Photos by family member |
| PUT | `/{id}` | Update photo metadata |
| POST | `/{id}/hide` | Hide photo |
| GET | `/timeline` | Timeline photos (cursor pagination; default `limit=20`, max 50). Query: `?limit=20&cursor=...`. Response: `{ items, nextCursor, hasMore }` |
| POST | `/today-memories` | **今日回憶** (lazy cache per family per calendar day). Body: optional `date`, location/travel/occasions — **used on first successful compose that day**; later calls return cached snapshot (`fromCache: true`). **Exception:** if the cached result is **empty** (`strategy: empty`) and the family later has **≥ 4 eligible photos**, the next call **recomputes** (drops stale empty cache, `fromCache: false`). Response: `{ items, strategy, referenceDate, fromCache }` |

### Activities — `api/v1/activities`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/in-progress` | Upload picker: `{ data: { items: [ActivityAlbum...] } }`, `status=inProgress`, user has access |
| GET | `/active-today?date=yyyy-MM-dd&limit=8` | Home carousel: `{ data: { strategy, items: [{ sortRank, subtitle, activity, photos }] } }`. Default `limit=8`, max 20. `strategy`: `empty` \| `singleActive` \| `multiActive`. Sort: creator first, then companion; then startDate proximity to `date`. |
| POST | `/` | Create activity (returns full DTO with `id` = `act_...`) |
| PUT | `/{activityId}` | Update activity (`activityId` = external id) |
| POST | `/{activityId}/photos` | Batch attach `{ photoIds: [...] }` |

`POST /uploads/start` optional **`activityAlbumId`** (`act_...`): validates in-progress + permission; links photo on **complete**.

Enums (JSON camelCase): `type` = travel \| wedding \| conference \| concert \| gathering; `status` = inProgress \| completed \| cancelled.

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
| POST | `/memories` | Create AI-generated memory |
| POST | `/inferences/{photoId}/confirm?memberId=` | Confirm AI member inference |
| POST | `/inferences/{photoId}/reject` | Reject AI inference |

### AI Agent (OpenAI chat) — `api/v1/ai/agent`

Conversational assistant for the MemoressaApp top-left AI entry. Uses OpenAI Chat Completions with family memory search as RAG context.

| Method | Path | Description |
|--------|------|-------------|
| POST | `/chat` | Send a message; returns natural-language reply + memory/photo links |
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
  "relatedMemories": [ { "memoryId": "...", "title": "...", "thumbnailPath": "...", "matchReasons": [], "relevanceScore": 0.9 } ]
}
```

Requires `Ai:OpenAiApiKey`. When the key is missing, the API falls back to search-only templated replies (no LLM).

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
| POST | `/{sessionId}/complete` | Verify S3 variants exist, create photo, apply quota |

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

After changing schema, update **both** SQL trees, then `Memoressa.Domain` entities and `Infrastructure/Configurations`, then deploy SQL before/with the API.

```bash
./database/mysql/apply.sh    # local MySQL
./database/apply.sh          # RDS PostgreSQL
# or: mysql ... < database/mysql/schema.sql
# or: psql -f database/schema.sql
```
