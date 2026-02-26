# GoogDocs Lite (.NET)

Google Docs Lite clone (Stage 0 + Stage 1 + Stage 2 + Stage 3 + Stage 4 + Stage 5 MVP) with:
- `server`: ASP.NET Core Web API
- `client`: ASP.NET Core MVC app (Razor Views)
- `shared`: shared constants
- SQLite + EF Core migrations
- ASP.NET Core Identity (register/login/logout)
- Quill rich-text editor + autosave
- Sharing/invites (Viewer/Editor) + pending invite sync
- SignalR live presence + single active editor lock

For a detailed learning path, see `GUIDE_WALKTHROUGH_RO.md`.

> Note: this repo currently targets `net9.0` because the installed SDK in this environment is .NET 9.  
> If you have .NET 10 installed, switch `net9.0` to `net10.0` in project files and creation commands.

## 1) Project structure

```txt
GOOGDOCS/
  server/GoogDocsLite.Server/
    Application/Services/
    Contracts/
    Controllers/
    Data/
  client/GoogDocsLite.Client/
    Controllers/
    Models/
    Services/
    Views/
  shared/GoogDocsLite.Shared/
  docker-compose.yml
  GoogDocsLite.sln
  README.md
```

## 2) Exact scaffold commands (from empty folder)

```bash
mkdir -p server client shared
dotnet new sln -n GoogDocsLite

dotnet new webapi -n GoogDocsLite.Server -o server/GoogDocsLite.Server --framework net9.0 --no-https
dotnet new mvc -n GoogDocsLite.Client -o client/GoogDocsLite.Client --framework net9.0 --no-https
dotnet new classlib -n GoogDocsLite.Shared -o shared/GoogDocsLite.Shared --framework net9.0

dotnet sln GoogDocsLite.sln add \
  server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  client/GoogDocsLite.Client/GoogDocsLite.Client.csproj \
  shared/GoogDocsLite.Shared/GoogDocsLite.Shared.csproj

dotnet add server/GoogDocsLite.Server/GoogDocsLite.Server.csproj reference shared/GoogDocsLite.Shared/GoogDocsLite.Shared.csproj
dotnet add client/GoogDocsLite.Client/GoogDocsLite.Client.csproj reference shared/GoogDocsLite.Shared/GoogDocsLite.Shared.csproj

dotnet add server/GoogDocsLite.Server/GoogDocsLite.Server.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.8
dotnet add server/GoogDocsLite.Server/GoogDocsLite.Server.csproj package Microsoft.EntityFrameworkCore.Design --version 9.0.8
dotnet add server/GoogDocsLite.Server/GoogDocsLite.Server.csproj package HtmlSanitizer

dotnet add client/GoogDocsLite.Client/GoogDocsLite.Client.csproj package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.8
dotnet add client/GoogDocsLite.Client/GoogDocsLite.Client.csproj package Microsoft.EntityFrameworkCore.Design --version 9.0.8
dotnet add client/GoogDocsLite.Client/GoogDocsLite.Client.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 9.0.9
dotnet add client/GoogDocsLite.Client/GoogDocsLite.Client.csproj package Microsoft.AspNetCore.Identity.UI --version 9.0.9

dotnet new tool-manifest
dotnet tool install dotnet-ef --version 9.0.8
```

## 3) EF migrations commands

```bash
dotnet tool run dotnet-ef migrations add InitialCreate \
  --project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  --startup-project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  --output-dir Data/Migrations

dotnet tool run dotnet-ef database update \
  --project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  --startup-project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj

dotnet tool run dotnet-ef migrations add Stage45SharingAndLocks \
  --project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  --startup-project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  --output-dir Data/Migrations

dotnet tool run dotnet-ef database update \
  --project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj \
  --startup-project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj

dotnet tool run dotnet-ef migrations add InitialIdentity \
  --project client/GoogDocsLite.Client/GoogDocsLite.Client.csproj \
  --startup-project client/GoogDocsLite.Client/GoogDocsLite.Client.csproj \
  --context AppIdentityDbContext \
  --output-dir Data/Migrations/Identity

dotnet tool run dotnet-ef database update \
  --project client/GoogDocsLite.Client/GoogDocsLite.Client.csproj \
  --startup-project client/GoogDocsLite.Client/GoogDocsLite.Client.csproj \
  --context AppIdentityDbContext
```

## 4) Run locally (recommended first)

Terminal 1 (API):
```bash
dotnet run --project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj --urls http://localhost:5175
```

Terminal 2 (MVC client):
```bash
dotnet run --project client/GoogDocsLite.Client/GoogDocsLite.Client.csproj --urls http://localhost:5169
```

Open: `http://localhost:5169`

## 4.1) Demo seeded accounts (auto-created on startup)

`SeedDemoData` is enabled by default in both apps, so these users are created automatically:

- `owner.demo@googdocs.local` / `demo123`
- `editor.demo@googdocs.local` / `demo123`
- `viewer.demo@googdocs.local` / `demo123`
- `outsider.demo@googdocs.local` / `demo123`

The API also seeds demo documents:
- one private owner doc
- one owner doc shared to editor
- one owner doc shared to viewer
- one owner doc with a pending invite (`pending.demo@googdocs.local`)

## 5) Run with Docker (optional clean workflow)

```bash
docker compose up --build
```

Open:
- Client: `http://localhost:5169`
- API: `http://localhost:5175/api/health`

Docker volumes persist:
- documents DB (`server-data`)
- identity DB (`client-data`)

Stop:
```bash
docker compose down
```

## 6) Features implemented (Stages 1, 2, 3, 4, 5 MVP)

- Create document (title)
- List documents
- Open document
- Edit title + rich text content (Quill)
- Save (manual button)
- Autosave every few seconds when content/title changes
- Rename (edit title + save)
- Delete
- Save state indicator (`Saving...` / `Saved`)
- Friendly errors in UI
- Register/Login/Logout with ASP.NET Core Identity
- Documents routes require authentication
- Sharing by email (owner invites as `Viewer` or `Editor`)
- Incoming invites inbox (accept/decline)
- Pending invite sync by user email header
- Access roles enforced server-side (`Owner` / `Editor` / `Viewer`)
- Live presence in editor (SignalR)
- Edit lock endpoints + UI (`Request edit access` / `Release edit access`)
- `PUT` guarded by lock ownership (`423 Locked` if lock missing/owned by someone else)
- MVC routing/pages:
  - `/` dashboard
  - `/docs` documents list
  - `/docs/{id}` editor
  - `/docs/invites` incoming invites

## 7) API endpoints

- `GET /api/health`
- `GET /api/documents?view=owned|shared|all`
- `POST /api/documents`
- `GET /api/documents/{id}`
- `PUT /api/documents/{id}`
- `DELETE /api/documents/{id}`
- `GET /api/documents/{id}/shares`
- `POST /api/documents/{id}/shares`
- `DELETE /api/documents/{id}/shares/{permissionOrInviteId}`
- `GET /api/invites/incoming`
- `POST /api/invites/{inviteId}/accept`
- `POST /api/invites/{inviteId}/decline`
- `POST /api/invites/sync-pending`
- `GET /api/documents/{id}/lock`
- `POST /api/documents/{id}/lock/acquire`
- `POST /api/documents/{id}/lock/heartbeat`
- `POST /api/documents/{id}/lock/release`

All document endpoints are scoped by user id header (`X-User-Id`) that MVC sends for the authenticated user.
API also expects internal service header (`X-Internal-Api-Key`) to block direct public usage.

## 8) Manual test plan (Stage 4 + Stage 5 focus)

1. Open `http://localhost:5169`.
2. Register `User A` and create a document.
3. In editor as `User A`, request edit access and confirm you can type/save.
4. Share document to `User B` email as `Editor`.
5. Register/login as `User B` with same email, open `/docs/invites`, accept invite.
6. Open `/docs?view=shared` as `User B`, open shared doc.
7. Confirm `User B` cannot save until pressing `Request edit access`.
8. Open same doc in `User A` and keep lock; confirm `User B` sees read-only + lock holder info.
9. Release lock as `User A`; acquire lock as `User B`; confirm save/autosave works.
10. With both tabs open, edit/save from lock owner and confirm remote tab updates content live.
11. As `Viewer` (optional), confirm read works but save/share/delete/lock acquire are blocked.
12. As owner, revoke share entry and confirm shared user loses access.

## 9) Troubleshooting

- If client cannot reach API:
  - Confirm API is running on `http://localhost:5175`.
  - Check `client/GoogDocsLite.Client/appsettings.Development.json` has `"ApiBaseUrl": "http://localhost:5175"`.
- If database issues happen:
  - Re-run both migration command groups (server docs DB + client identity DB).
  - Delete `server/GoogDocsLite.Server/docs-lite.db` and run migration update again.
  - Delete `client/GoogDocsLite.Client/identity.db` and run identity migration update again.
- If `dotnet-ef` is missing:
  - Run `dotnet tool restore`.
- If Quill toolbar doesn’t load:
  - Check internet access to jsdelivr CDN (Quill assets are loaded from CDN).
- If realtime presence/lock events do not appear:
  - Check browser console for SignalR connection errors to `/hubs/document-collab`.
  - Confirm client app runs on `http://localhost:5169` and user is authenticated.
