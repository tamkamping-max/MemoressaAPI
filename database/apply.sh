#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MIGRATIONS="$ROOT/database/migrations"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-memoressa}"
PGPASSWORD="${PGPASSWORD:-memoressa}"
PGDATABASE="${PGDATABASE:-memoressa}"
PGSSLMODE="${PGSSLMODE:-prefer}"
export PGPASSWORD PGSSLMODE

echo "Applying Memoressa SQL migrations to ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE} (sslmode=${PGSSLMODE})"
echo "For RDS, set PGSSLMODE=require and PGHOST to your *.rds.amazonaws.com endpoint."

for file in "$MIGRATIONS"/*.sql; do
  echo "-> $(basename "$file")"
  psql -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -f "$file"
done

echo "Done."
