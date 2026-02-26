# GoogDocs Lite Project Guide

Welcome to the comprehensive guide for the **GoogDocs Lite** project! This guide is designed to help you deeply understand the architecture, data flow, and components of this application.

## 1. High-Level Architecture

The project is split into three main .NET 9.0 projects inside a single solution:

1. **`GoogDocsLite.Server` (Web API)**: The backend API responsible for managing the documents. It connects to its own SQLite database (`docs-lite.db`).
2. **`GoogDocsLite.Client` (MVC)**: The frontend user interface built with ASP.NET Core MVC and Razor Views. It handles user authentication, connects to an Identity SQLite database (`identity.db`), and communicates with the Server API to fetch/save documents.
3. **`GoogDocsLite.Shared` (Class Library)**: A shared project for common constants or utilities needed by both Client and Server.

This is a **Backend-for-Frontend (BFF)** inspired architecture, where the MVC app acts as a client that renders the UI and securely makes HTTP calls to the internal Web API on behalf of the user.

---

## 2. Directory Breakdown

### `server/GoogDocsLite.Server` (The API)
- **`Controllers/DocumentsController.cs`**: Contains the REST API endpoints (`GET`, `POST`, `PUT`, `DELETE`). It doesn't use standard JWT bear authentication; instead, it expects two headers:
  - `X-Internal-Api-Key`: A secret key (`dev-internal-key-change-me`) ensuring only the MVC client can call this API.
  - `X-User-Id`: The ID of the authenticated user, forwarded by the client.
- **`Application/Services/DocumentService.cs`**: The business logic layer. Implements CRUD operations, sanitizes rich text HTML using `HtmlSanitizer` to prevent XSS, and updates timestamps.
- **`Data/AppDbContext.cs` & `Entities/DocumentEntity.cs`**: EF Core configuration for the documents. The schema is simple: `Id`, `UserId`, `Title`, `Content`, `CreatedAt`, `UpdatedAt`.

### `client/GoogDocsLite.Client` (The Web App)
- **`Controllers/DocumentsController.cs`**: The MVC controller that handles user interactions. It renders the views for the document list and the editor, and processes form submissions.
- **`Services/DocumentsApiClient.cs`**: A typed `HttpClient` service injected into the MVC controllers. It encapsulates all HTTP requests (`GET`, `POST`, `PUT`, `DELETE`) to the Server API. It automatically attaches the `X-Internal-Api-Key` and the current user's `X-User-Id` to every request.
- **`Data/AppIdentityDbContext.cs`**: Handles ASP.NET Core Identity (Users, Passwords, Login sessions). Note that documents and users are in completely separate databases to demonstrate a microservice-like database per service pattern.
- **`Views/`**: Contains Razor templates (`.cshtml`). For instance, `Views/Documents/Edit.cshtml` integrates the **Quill.js** rich text editor and includes JavaScript for auto-saving changes back to the MVC server without reloading the page.

### `shared/GoogDocsLite.Shared`
- Contains simple models/constants (like `AppDefaults.cs`).

---

## 3. The Data Flow (How it all works)

Let's walk through the lifecycle of editing a document:

1. **User Authentication**:
   - The user visits the app (`http://localhost:5169`), registers/logs in via ASP.NET Core Identity UI.
   - The authentication cookie is set in the browser.

2. **Opening a Document**:
   - The user clicks on a document in the dashboard (`/docs`).
   - The browser navigates to `/docs/edit/{id}`.
   - The `DocumentsController.Edit` (Client) reads the user's ID from their claims, then calls `DocumentsApiClient.GetAsync(id, userId)`.
   - `DocumentsApiClient` sends an HTTP GET request to `http://localhost:5175/api/documents/{id}` with `X-Internal-Api-Key` and `X-User-Id` headers.
   - The API (`DocumentsController.Get` -> `DocumentService`) fetches the document from `docs-lite.db` and returns it as JSON.
   - The Client maps the JSON to a `DocumentEditorViewModel` and renders the `Edit.cshtml` view.

3. **Rich Text Editing (Quill.js)**:
   - `Edit.cshtml` loads Quill.js from a CDN and initializes it onto a `<div>`. It injects the `Content` property from the model as HTML into the editor.
   - As the user types, a JavaScript auto-save timer is triggered (e.g., every 3-5 seconds).

4. **Auto-Saving**:
   - The auto-save JS code sends a `POST` request (AJAX) to the MVC Client's `/docs/autosave/{id}` endpoint with the new title and HTML content.
   - The MVC `DocumentsController.AutoSave` handles this POST, extracts the auth claims, and calls `DocumentsApiClient.UpdateAsync(...)`.
   - `DocumentsApiClient` fires a `PUT` request to the Server API.
   - The Server API `DocumentService.UpdateForUserAsync` runs `HtmlSanitizer` on the HTML to clean up any malicious `<script>` tags, updates the `UpdatedAt` timestamp, saves to SQLite, and returns success.
   - The MVC Client returns a JSON success response to the browser's JS, which updates the UI to show "Saved".

---

## 4. Key Design Decisions

- **Separation of Concerns (Client vs API)**: 
  The frontend and backend are decoupled. This means you could eventually build a mobile app (e.g., MAUI, React Native) that talks directly to the same Server API, purely sending JSON format data.
- **Database per Service**:
  User accounts live in `identity.db` (Client project), while actual content lives in `docs-lite.db` (Server project). They are loosely coupled by `UserId` (a string).
- **Security**: 
  Instead of exposing the documents API to the public internet, it uses an internal API key pattern. The MVC client is the trusted proxy. Input validation (HTML Sanitization) happens strictly on the backend before database insertion.

## 5. Summary of Docker Compose Implementation
If ran via `docker compose up`, the ecosystem spins up instances of the web API and the MVC app in distinct containers. It configures Docker volumes (`server-data` and `client-data`) to persist the SQLite `.db` files so that you don't lose documents or users between container restarts.
