# PostgreSQL Quickstart

## Step 1: Connect with psql

Connect to the database using the psql client:

```bash
docker compose -f services/postgres/docker-compose.yml exec postgres psql -U devuser -d devdb
```

## Step 2: Create a database

Once connected, create a new database for your project:

```sql
CREATE DATABASE myproject;
\c myproject
```

## Step 3: Connection string

Use this string in your application config:

```
postgresql://devuser:devpass@localhost:5432/devdb
```