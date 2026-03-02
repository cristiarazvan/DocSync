# DocSync

DocSync is a collaborative document editor built with `.NET 9`, designed as a portfolio-ready full-stack project that demonstrates real-time collaboration, role-based access control, and service separation in an ASP.NET Core ecosystem.

The application combines an ASP.NET Core MVC client, a separate Web API for document operations, ASP.NET Core Identity for authentication, SignalR for live collaboration, and EF Core with SQLite for persistence.

## Overview

This project was built to explore how a document editor can be structured beyond basic CRUD. It includes:

- Rich-text document editing with Quill
- Authentication and user management with ASP.NET Core Identity
- Role-based sharing (`Owner`, `Editor`, `Viewer`)
- Invite-based collaboration flows
- Real-time presence and revision-aware collaboration
- Single active-editor locking for coordinated editing
- Dockerized local deployment

## Architecture

DocSync follows a Backend-for-Frontend style structure:

- `client/GoogDocsLite.Client`
  ASP.NET Core MVC application with Razor Views, Identity UI, SignalR hub, and typed HTTP clients for API communication
- `server/GoogDocsLite.Server`
  ASP.NET Core Web API responsible for document storage, sharing rules, realtime state, and edit-lock enforcement
- `shared/GoogDocsLite.Shared`
  Shared constants and cross-project primitives

This separation keeps authentication concerns in the MVC application while the document API remains focused on document data, collaboration rules, and persistence.

## Key Features

- Secure registration, login, and logout with ASP.NET Core Identity
- Document creation, listing, editing, renaming, and deletion
- Rich-text editing with Quill
- Manual save and autosave workflows
- Email-based document sharing and invite acceptance/decline flows
- Server-enforced access control for `Owner`, `Editor`, and `Viewer`
- SignalR-based presence indicators for active collaborators
- Active editor lock with acquire, heartbeat, and release flows
- Revision-aware realtime synchronization with operation submission and replay
- HTML snapshot synchronization for resync and recovery scenarios
- SQLite persistence with EF Core migrations
- Docker Compose setup for local multi-container execution

## Tech Stack

- `C#`
- `.NET 9`
- `ASP.NET Core MVC`
- `ASP.NET Core Web API`
- `ASP.NET Core Identity`
- `SignalR`
- `Entity Framework Core`
- `SQLite`
- `Quill.js`
- `HtmlSanitizer`
- `Docker` and `Docker Compose`

## Project Structure

```txt
GOOGDOCS/
  client/GoogDocsLite.Client/
  server/GoogDocsLite.Server/
  shared/GoogDocsLite.Shared/
  GoogDocsLite.sln
  docker-compose.yml
```

## Running Locally

Requirements:

- `.NET 9 SDK`
- `Docker` (optional, only for containerized run)

Start the API:

```bash
dotnet run --project server/GoogDocsLite.Server/GoogDocsLite.Server.csproj --urls http://localhost:5175
```

Start the MVC client:

```bash
dotnet run --project client/GoogDocsLite.Client/GoogDocsLite.Client.csproj --urls http://localhost:5169
```

Open:

- Client: `http://localhost:5169`
- API health check: `http://localhost:5175/api/health`

## Running with Docker

```bash
docker compose up --build
```

Open:

- Client: `http://localhost:5169`
- API health check: `http://localhost:5175/api/health`

Persistent volumes:

- `server-data` for document storage
- `client-data` for identity storage

Stop containers:

```bash
docker compose down
```

## Demo Accounts

Demo seeding is enabled by default. The following users are created automatically on startup:

- `owner.demo@googdocs.local` / `demo123`
- `editor.demo@googdocs.local` / `demo123`
- `viewer.demo@googdocs.local` / `demo123`
- `outsider.demo@googdocs.local` / `demo123`

The API also seeds sample documents, including private, shared, and pending-invite scenarios.

## Selected API Surface

Document management:

- `GET /api/documents?view=owned|shared|all`
- `POST /api/documents`
- `GET /api/documents/{id}`
- `PUT /api/documents/{id}`
- `DELETE /api/documents/{id}`

Sharing and invites:

- `GET /api/documents/{id}/shares`
- `POST /api/documents/{id}/shares`
- `DELETE /api/documents/{id}/shares/{permissionOrInviteId}`
- `GET /api/invites/incoming`
- `POST /api/invites/{inviteId}/accept`
- `POST /api/invites/{inviteId}/decline`

Realtime and locking:

- `GET /api/documents/{id}/lock`
- `POST /api/documents/{id}/lock/acquire`
- `POST /api/documents/{id}/lock/heartbeat`
- `POST /api/documents/{id}/lock/release`
- `GET /api/documents/{id}/realtime/state`
- `POST /api/documents/{id}/realtime/ops`
- `GET /api/documents/{id}/realtime/ops`
- `PUT /api/documents/{id}/realtime/html-snapshot`

## Security and Design Notes

- The MVC client forwards authenticated user context to the API
- The API expects an internal service key header for protected internal access
- Rich-text content is sanitized server-side before persistence
- Authorization rules are enforced server-side rather than relying on UI checks alone
- Document and identity data are stored in separate SQLite databases

## Why This Project Matters

This project demonstrates practical experience with:

- Full-stack ASP.NET Core application design
- Realtime collaboration patterns with SignalR
- Role-based authorization and invite workflows
- EF Core data modeling and migrations
- Multi-project solution design and service separation
- Containerized local development workflows

## Notes

- The solution currently targets `net9.0`
- Quill assets are loaded from `jsdelivr`, so internet access is required for the editor toolbar assets

