# Memoressa API

REST API backend for the Memoressa family memory platform, built with ASP.NET Core 8 and PostgreSQL.

## Architecture

The solution follows a layered architecture:

```
Memoressa.Api            → HTTP controllers, middleware, auth, Swagger
Memoressa.Application    → Business logic, DTOs, service interfaces
Memoressa.Infrastructure → EF Core mapping, PostgreSQL, S3, JWT, email, AI orchestration
Memoressa.Domain         → Entities and enums
```

Requests flow through middleware (exception handling, internal API key validation), JWT authentication, controllers, application services, and infrastructure adapters.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) (for local PostgreSQL)

## Quick Start

1. Start PostgreSQL:

```bash
docker compose up -d
```

2. Apply database schema (from repo root):

```bash
./database/apply.sh
```

   On a **fresh** `docker compose` volume, SQL under `database/migrations/` is also applied automatically via `docker-entrypoint-initdb.d`.

   For an empty database in one shot: `psql -f database/schema.sql`

3. Run the API:

```bash
dotnet run --project src/Memoressa.Api
```

4. Open Swagger UI: `https://localhost:7xxx/swagger` (port shown in console output)

Schema is defined by **SQL files** in `database/migrations/` (see `database/README.md`). EF Core maps entities only; the API does not run `Database.Migrate()` on startup.

## Configuration

Configuration is loaded from `src/Memoressa.Api/appsettings.json` and environment-specific overrides.

### ConnectionStrings

| Key | Description | Default |
|-----|-------------|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | `Host=localhost;Port=5432;Database=memoressa;Username=memoressa;Password=memoressa` |

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
| `AwsS3:PresignedUrlExpiryMinutes` | Presigned GET/PUT URL expiry for API responses |
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

If thumbnail generation fails, upload still succeeds; presigned thumbnail URLs fall back to the full object until a retry (`complete` idempotency will attempt generation again).

Upload flow:

1. `POST /api/v1/uploads/start` → presigned **PUT** URL + `s3Key`
2. Client uploads directly to S3
3. `POST /api/v1/uploads/{sessionId}/complete` → creates `Photo` with `S3Key` only, response includes presigned GET URLs

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
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=memoressa;Username=memoressa;Password=memoressa"
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
| GET | `/by-date/{date}` | Photos by date |
| GET | `/by-member/{memberId}` | Photos by family member |
| PUT | `/{id}` | Update photo metadata |
| POST | `/{id}/hide` | Hide photo |
| GET | `/timeline` | Timeline photos |

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
| POST | `/start` | Start S3 presigned upload session |
| POST | `/{sessionId}/complete` | Complete upload and create photo |

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
│   ├── migrations/        # Incremental .sql schema (source of truth)
│   ├── schema.sql         # Full schema (greenfield)
│   ├── apply.sh           # Apply migrations to PostgreSQL
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

PostgreSQL schema lives in **`database/migrations/*.sql`**, not in EF Core migrations.

| File | Contents |
|------|----------|
| `001_initial.sql` | Users, families, photos, memories, frame, uploads, … |
| `002_ai_chat_sessions.sql` | AI agent chat |
| `003_journal_tags.sql` | Journal tags |
| `004_frame_package_external_id.sql` | Frame package `external_id` |

After changing SQL, update `Memoressa.Domain` entities and `Infrastructure/Configurations` to match, then deploy SQL before/with the API.

```bash
./database/apply.sh
# or: psql -f database/schema.sql
```

Do **not** use `dotnet ef migrations add` / `dotnet ef database update`.
