# Local MySQL schema (development)

When `Database:Target` is **`Local`**, the API uses **MySQL 8** via Pomelo EF Core.

**Schema source of truth (local):** the `.sql` files in `database/mysql/migrations/`, not EF Core migrations.

## Layout

| File | Description |
|------|-------------|
| `migrations/001_initial.sql` | Core tables (mirrors PostgreSQL `database/migrations/001_initial.sql`) |
| `migrations/002_ai_chat_sessions.sql` | AI agent chat tables |
| `migrations/003_journal_tags.sql` | Journal tag table |
| `migrations/004_frame_package_external_id.sql` | `frame_playback_packages.ExternalId` |
| `migrations/014_password_reset_codes.sql` | OTP password reset (`password_reset_codes`) |
| `migrations/015_photo_user_tags_and_comments.sql` | User photo tags (`photo_user_tags`) and photo comments |
| `migrations/016_user_photo_tag_library.sql` | Per-user photo tag library (tags sheet) |
| `migrations/017_photo_albums.sql` | Photo albums (experience groups) |
| `migrations/018_photo_albums_backfill_note.sql` | Optional legacy backfill notes (no-op) |
| `schema.sql` | Full schema (greenfield apply) |
| `apply.sh` | Apply incremental migrations via `mysql` client |

When you change the schema, update **both** PostgreSQL (`database/migrations/`) and MySQL (`database/mysql/migrations/`) SQL, then update C# entities / EF configurations.

## Docker (recommended)

```bash
docker compose up -d
```

On a **fresh** volume, `database/mysql/schema.sql` is applied automatically via `docker-entrypoint-initdb.d`.

## Manual apply

```bash
docker compose up -d
./database/mysql/apply.sh
# or greenfield:
mysql -h 127.0.0.1 -P 3306 -u memoressa -pmemoressa memoressa < database/mysql/schema.sql
```

Environment variables for `apply.sh`:

| Variable | Default |
|----------|---------|
| `MYSQL_HOST` | `localhost` |
| `MYSQL_PORT` | `3306` |
| `MYSQL_USER` | `memoressa` |
| `MYSQL_PASSWORD` | `memoressa` |
| `MYSQL_DATABASE` | `memoressa` |

Then run the API:

```bash
dotnet run --project src/Memoressa.Api
```

Default connection (override via environment or user secrets):

```
Server=localhost;Port=3306;Database=memoressa;User=memoressa;Password=memoressa
```

## Point local API at RDS instead

Set in environment or user secrets (not committed):

```json
"Database": { "Target": "Rds" }
```

and configure `ConnectionStrings:PostgreSql` with your RDS endpoint + SSL. Use `database/apply.sh` for PostgreSQL schema.
