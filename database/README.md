# Memoressa PostgreSQL Schema

**Schema source of truth:** the `.sql` files in `database/migrations/`, not EF Core migrations.

EF Core (`MemoressaDbContext`) maps C# entities to these tables at runtime only. When you change the schema, edit or add SQL here, then update the Domain entities / EF configurations to match.

## Layout

| File | Description |
|------|-------------|
| `migrations/001_initial.sql` | Core tables (users, families, photos, memories, frame, …) |
| `migrations/002_ai_chat_sessions.sql` | AI agent chat tables |
| `migrations/003_journal_tags.sql` | Journal tag table |
| `migrations/004_frame_package_external_id.sql` | `frame_playback_packages.external_id` |
| `schema.sql` | Full schema (concatenation of all migrations, greenfield apply) |
| `apply.sh` | Apply incremental migrations to an existing database |

## Local setup

```bash
docker compose up -d
./database/apply.sh
```

Fresh Docker volumes also auto-run `database/migrations/*.sql` via `docker-entrypoint-initdb.d` on first boot.

## Manual apply

```bash
export PGHOST=localhost PGPORT=5432 PGUSER=memoressa PGPASSWORD=memoressa PGDATABASE=memoressa

# Greenfield (empty database)
psql -f database/schema.sql

# Or incremental
./database/apply.sh
```

## Production (RDS)

Run the same SQL against RDS (CI/CD or DBA workflow). Do **not** use `dotnet ef database update`.

## Adding a schema change

1. Add `database/migrations/00N_description.sql`
2. Append it to `database/schema.sql` (or regenerate with `cat migrations/*.sql > schema.sql`)
3. Update `Memoressa.Domain` entities + `Infrastructure/Configurations`
4. Deploy SQL before or with the API release
