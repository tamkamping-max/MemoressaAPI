#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
MIGRATIONS="$ROOT/database/mysql/migrations"

MYSQL_HOST="${MYSQL_HOST:-localhost}"
MYSQL_PORT="${MYSQL_PORT:-3306}"
MYSQL_USER="${MYSQL_USER:-memoressa}"
MYSQL_PASSWORD="${MYSQL_PASSWORD:-memoressa}"
MYSQL_DATABASE="${MYSQL_DATABASE:-memoressa}"

echo "Applying Memoressa MySQL migrations to ${MYSQL_USER}@${MYSQL_HOST}:${MYSQL_PORT}/${MYSQL_DATABASE}"

for file in "$MIGRATIONS"/*.sql; do
  echo "-> $(basename "$file")"
  mysql \
    --host="$MYSQL_HOST" \
    --port="$MYSQL_PORT" \
    --user="$MYSQL_USER" \
    --password="$MYSQL_PASSWORD" \
    --database="$MYSQL_DATABASE" \
    --default-character-set=utf8mb4 \
    < "$file"
done

echo "Done."
