# TaskFlow

TaskFlow is a multi-user kanban task management system in the spirit of Trello / Jira-lite.

The current implementation focuses on a strong backend core:

- JWT-style access authentication with refresh sessions
- user registration, login, and profile updates
- workspaces with roles (`Owner`, `Admin`, `Member`, `Viewer`)
- boards, private/public access, and board members
- columns with ordering and delete restrictions
- task cards with drag-and-drop move logic and position recalculation
- assignees, deadlines, labels, checklist items, comments
- notifications and card activity history
- SignalR board events
- background reminders for upcoming deadlines
- demo seed data for quick exploration
- embedded SPA frontend served by `TaskFlow.Api`

## Tech stack

- C# / ASP.NET Core Web API
- clean-ish layered structure via `Domain`, `Application`, `Infrastructure`, `Api`
- PostgreSQL
- Entity Framework Core
- SignalR for realtime board events

## Project structure

```text
src/
  TaskFlow.Domain/
  TaskFlow.Application/
  TaskFlow.Infrastructure/
  TaskFlow.Api/
```

`TaskFlow.Api` is the executable entry point and currently compiles the layered source tree into one runnable app so `dotnet build` works reliably in this environment.

## Database

The app now uses PostgreSQL and applies EF Core migrations automatically on startup.

Default local connection string:

```text
Host=localhost;Port=5432;Database=taskflow;Username=taskflow;Password=taskflow
```

## Run with Docker Compose

Make sure Docker Desktop is running, then from the project root:

```powershell
docker compose up --build
```

Services:

- API: `http://127.0.0.1:5087`
- PostgreSQL: `localhost:5432`

Open `http://127.0.0.1:5087/` to use the frontend.

## Seed users

- `alice@taskflow.local` / `Passw0rd!`
- `bob@taskflow.local` / `Passw0rd!`
- `carol@taskflow.local` / `Passw0rd!`

## Key API routes

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `GET /api/users/me`
- `GET /api/users?search=alice`
- `PUT /api/users/me`
- `GET /api/workspaces`
- `POST /api/workspaces`
- `GET /api/workspaces/{id}`
- `POST /api/workspaces/{id}/members`
- `GET /api/workspaces/{id}/labels`
- `POST /api/workspaces/{id}/labels`
- `POST /api/workspaces/{workspaceId}/boards`
- `GET /api/boards/{id}`
- `PUT /api/boards/{id}`
- `DELETE /api/boards/{id}`
- `POST /api/boards/{id}/members`
- `GET /api/boards/{id}/cards`
- `POST /api/boards/{boardId}/columns`
- `PUT /api/columns/{id}`
- `DELETE /api/columns/{id}`
- `PUT /api/columns/reorder`
- `POST /api/columns/{columnId}/cards`
- `GET /api/cards/{id}`
- `PUT /api/cards/{id}`
- `DELETE /api/cards/{id}`
- `PUT /api/cards/{id}/move`
- `PUT /api/cards/{id}/assign`
- `PUT /api/cards/{id}/labels`
- `POST /api/cards/{cardId}/comments`
- `PUT /api/comments/{id}`
- `DELETE /api/comments/{id}`
- `POST /api/cards/{cardId}/checklist`
- `PUT /api/checklist-items/{id}`
- `DELETE /api/checklist-items/{id}`
- `GET /api/notifications`
- `PUT /api/notifications/{id}/read`
- `GET /api/cards/{id}/activity`

## Frontend

The frontend is now embedded into `TaskFlow.Api/wwwroot` and served from the API host root path:

- App shell: `/`
- Board deep-link: `/board/{id}`

Main supported flows:

- register / login / refresh session
- create workspaces and boards
- browse workspace members and boards
- invite workspace members by searching users
- manage board settings and board members
- create, edit, archive, delete, and drag cards between columns
- create, rename, delete, and reorder columns
- edit card details, labels, assignee, checklist items, and comments
- view notifications and card activity

## Realtime

SignalR hub:

- `GET /hubs/boards`

Hub methods:

- `JoinBoard(Guid boardId)`
- `LeaveBoard(Guid boardId)`

Server events include:

- `board.created`
- `board.updated`
- `column.created`
- `column.updated`
- `column.deleted`
- `card.created`
- `card.updated`
- `card.moved`
- `card.assigned`
- `comment.created`
- `comment.updated`
- `comment.deleted`
