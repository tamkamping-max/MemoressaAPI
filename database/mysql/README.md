# Local MySQL (development)

When `Database:Target` is **`Local`**, the API uses **MySQL 8** via Pomelo EF Core.

## Schema

- **Release (RDS):** PostgreSQL schema from `database/migrations/*.sql`
- **Local (MySQL):** schema is created automatically on first `dotnet run` in Development (`DatabaseBootstrap` → `EnsureCreated`)

Keep entity mappings aligned with the PostgreSQL SQL when you change the schema.

## Docker

```bash
docker compose up -d
dotnet run --project src/Memoressa.Api
```

Default connection (`appsettings.Development.json`):

```
Server=localhost;Port=3306;Database=memoressa;User=memoressa;Password=memoressa
```

## Switch to RDS from local machine

Set in `appsettings.Development.json` or environment:

```json
"Database": { "Target": "Rds" }
```

and configure `ConnectionStrings:PostgreSql` with your RDS endpoint + SSL.
