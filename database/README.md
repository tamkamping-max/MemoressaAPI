# Memoressa PostgreSQL Schema (RDS / release)

**Production database:** Amazon **RDS PostgreSQL** (`Database:Target` = **`Rds`**).  
**Local development database:** **MySQL 8** (`Database:Target` = **`Local`**) — schema in [`mysql/migrations/`](mysql/migrations/) and [`mysql/README.md`](mysql/README.md).

**Schema source of truth (RDS only):** the `.sql` files in `database/migrations/`, not EF Core.

EF Core (`MemoressaDbContext`) maps C# entities to these tables at runtime only. When you change the schema, edit or add SQL here, then update the Domain entities / EF configurations to match.

## Layout

| File | Description |
|------|-------------|
| `migrations/001_initial.sql` | Core tables (users, families, photos, memories, frame, …) |
| `migrations/002_ai_chat_sessions.sql` | AI agent chat tables |
| `migrations/003_journal_tags.sql` | Journal tag table |
| `migrations/004_frame_package_external_id.sql` | `frame_playback_packages.external_id` |
| `schema.sql` | Full schema (concatenation of all migrations, greenfield apply) |
| `apply.sh` | Apply incremental migrations via `psql` |

## RDS PostgreSQL (production)

### 1. Create RDS instance

- Engine: **PostgreSQL 16** (or 15+)
- Create an empty database, e.g. `memoressa`
- Place RDS in the **same VPC** as the EC2 REST API (or use appropriate peering / security groups)
- Security group: allow **inbound TCP 5432** from the EC2 security group (not `0.0.0.0/0` in production)

### 2. Apply schema to RDS

From a machine that can reach RDS (EC2 bastion, CI runner, or VPN):

```bash
export PGHOST=your-instance.xxxxx.region.rds.amazonaws.com
export PGPORT=5432
export PGUSER=memoressa
export PGPASSWORD='your-password'
export PGDATABASE=memoressa
export PGSSLMODE=require

# New empty database (recommended first deploy)
psql -v ON_ERROR_STOP=1 -f database/schema.sql

# Or apply incremental migrations in order
./database/apply.sh
```

`apply.sh` respects `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `PGDATABASE`, and `PGSSLMODE` (default `prefer`; use **`require`** for RDS).

### 3. Point the REST API at RDS

Set the connection string on EC2 (environment variable or secrets manager):

```bash
export Database__Target=Rds
export ConnectionStrings__PostgreSql="Host=your-instance.xxxxx.region.rds.amazonaws.com;Port=5432;Database=memoressa;Username=memoressa;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

Use environment variables for RDS connection strings in production (see root `README.md`).

When `Database:Target` is **`Rds`**, Npgsql uses `ConnectionStrings:PostgreSql`. The API **does not** auto-apply schema on startup; run SQL deploy separately.

### 4. Schema updates on RDS

1. Add `database/migrations/00N_description.sql`
2. Regenerate full dump: `cat database/migrations/*.sql > database/schema.sql`
3. Update C# entities / EF configurations
4. Run **only the new** migration file against RDS (or re-run `apply.sh` on a DB that already has prior objects — use idempotent SQL or manual review)

Do **not** use `dotnet ef database update`.

## Local development (optional)

Local dev uses **MySQL** via Docker (`docker compose up -d`), schema from **`database/mysql/migrations/*.sql`**:

```bash
docker compose up -d    # MySQL on :3306; fresh volume auto-applies mysql/schema.sql
./database/mysql/apply.sh   # existing volume / re-apply migrations
dotnet run --project src/Memoressa.Api
```

## Adding a schema change

1. Add `database/migrations/00N_description.sql`
2. Append to `database/schema.sql` (`cat database/migrations/*.sql > database/schema.sql`)
3. Update `Memoressa.Domain` entities + `Infrastructure/Configurations`
4. Apply SQL to **RDS** (and local Docker if used) before or with the API release
