# HomeSuite

HomeSuite is split into a Vue/Vite frontend and a .NET 8 backend.

## Structure

- `backend/`: .NET solution and application code
- `frontend/`: Vue 3 + Vite frontend
- `postgres/`: old local Docker helper for development
- `deploy.sh`: legacy manual deploy script
- `migrate.sh`: legacy manual migration script

## Current Server Model

The production runtime is now primarily modeled from the NixOS server config in the separate `NixOS-Config` repository.

On the server host, HomeSuite is managed through the `homesuite` server module and currently provides:

- a `homesuite-backend` systemd service
- a local PostgreSQL database managed by NixOS
- release directories under `/var/lib/homesuite`
- a manual deploy command for backend + frontend builds
- a manual migration command for Entity Framework updates
- nginx serving the frontend and proxying `/api/` to the backend

## Server Paths

- source checkout: `/home/jakob/homesuite`
- runtime root: `/var/lib/homesuite`
- backend releases: `/var/lib/homesuite/backend/releases`
- frontend releases: `/var/lib/homesuite/frontend/releases`
- active backend symlink: `/var/lib/homesuite/backend/current`
- active frontend symlink: `/var/lib/homesuite/frontend/current`

## NixOS Runtime Flow

`nixos-rebuild` does not build the app automatically.

It only prepares the runtime:

- service users and directories
- PostgreSQL database and user
- systemd service definitions
- nginx integration
- deploy and migration helper commands

Build and rollout stay explicit.

## Deploy Flow

Build and publish a new backend/frontend release on the server:

```bash
sudo systemctl start homesuite-deploy
```

This does the following:

1. builds the backend with `dotnet publish`
2. builds the frontend with `npm ci` and `npm run build`
3. creates timestamped release directories
4. updates the `current` symlinks
5. restarts `homesuite-backend`
6. reloads nginx

You can inspect the run with:

```bash
journalctl -u homesuite-deploy -n 100 --no-pager
systemctl status homesuite-backend --no-pager
```

## Migration Flow

Run Entity Framework migrations separately:

```bash
sudo systemctl start homesuite-migrate
```

Useful checks:

```bash
journalctl -u homesuite-migrate -n 100 --no-pager
systemctl status postgresql --no-pager
```

## Database

The server runtime uses local PostgreSQL managed by NixOS.

Current defaults in the NixOS module are:

- database: `homesuite`
- user: `homesuite`
- port: `5432`

The backend receives its connection string from the NixOS module by default. An optional environment file can still override runtime values if needed.

## Development Notes

The old scripts `deploy.sh` and `migrate.sh` still exist as legacy helpers, but the intended server path is now the NixOS-managed flow.

For local development you can still use the project layout directly from the checkout. The `postgres/` folder currently reflects the older Docker-based local DB helper and is no longer the authoritative production setup.

## Typical Server Operations

Apply infrastructure changes:

```bash
sudo nixos-rebuild switch --flake /home/jakob/NixOS-Config#homedepot
```

Deploy a new application version:

```bash
sudo systemctl start homesuite-deploy
```

Run schema migrations:

```bash
sudo systemctl start homesuite-migrate
```

Check runtime status:

```bash
systemctl status homesuite-backend postgresql nginx --no-pager
```
