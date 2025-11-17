# ESS Leave System (Razor Pages, .NET 8)

A lightweight Employee Self-Service Leave Management system built with ASP.NET Core Razor Pages (.NET 8). Simple to run locally or deploy with Docker + Railway.

## Live Demo
URL: https://ess-leave-system-production.up.railway.app

Demo accounts:
- Admin: sarah.johnson@company.co.za / Test@123
- Manager: david.brown@company.co.za / Test@123
- Employee: michael.chen@company.co.za / Test@123

Note: This is a hobby/portfolio project. Data may be reset periodically.

## Features
- BCEA compliant leave types (South Africa)
- Role based access: Admin, Manager, HR, Employee
- Automatic Manager role if a user has direct reports
- Manager approval workflow (with Admin/HR override)
- Auto approval after 5 days pending (urgent reminder at day 4)
- Annual leave balances with Used / Pending / Available tracking
- In app notifications (submitted / approved / rejected / auto approved)
- Employee directory and basic profiles
- Health check endpoint: `/health`

## Tech Stack
- .NET 8 + Razor Pages
- ASP.NET Core Identity (cookie auth)
- Entity Framework Core (SQLite default)
- Docker multi-stage build
- Railway deployment with persistent volume

## Quick Start (Local)
Prerequisites: .NET 8 SDK

```bash
cd ESSLeaveSystem
# Restore & run
dotnet restore
dotnet run

# App will start on http://localhost:5000 (or shown URL)
# Database and seed data are created on first run
```

Seeded demo accounts:
- Admin: sarah.johnson@company.co.za / Test@123
- Manager: david.brown@company.co.za / Test@123
- Employee: michael.chen@company.co.za / Test@123

Manager role assignment:
- A user is a Manager if any employee has `LineManagerId` equal to their `EmployeeId` (assigned during seeding in `Data/ESSDataSeeder.cs`).

## Docker (Local)
```bash
# From ESSLeaveSystem/
docker build -t ess-leave-system .

# Run with persistent volume for SQLite data
docker run -d -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -v essleave-data:/app/data \
  ess-leave-system

# Visit http://localhost:8080
```

## Railway Deployment (SQLite + Persistent Volume)
This repo includes `railway.toml` and production settings for persistence.

- `appsettings.Production.json` uses `Data Source=/app/data/essleave.db`
- `railway.toml` mounts a persistent volume at `/app/data`
- Dockerfile creates `/app/data` at build time

Deploy steps:
1. Push to GitHub (main branch)
2. Connect repo to Railway
3. Railway builds the Dockerfile and deploys automatically

Data persistence:
- SQLite file stored at `/app/data/essleave.db`
- Survives restarts and redeployments

Limitations (SQLite):
- Single instance only (no horizontal scaling)
- Manual backups recommended (download `/app/data/essleave.db` periodically)

## Configuration
- Connection string (Production): `ConnectionStrings:DefaultConnection` ? `Data Source=/app/data/essleave.db`
- Health check: GET `/health`
- Auto approval timing: `Services/LeaveApprovalService.cs`
  - `AUTO_APPROVE_DAYS = 5`
  - `URGENT_REMINDER_DAYS = 4`

## Development Notes
- Target Framework: .NET 8
- Project Type: Razor Pages
- Seeder: `Data/ESSDataSeeder.cs` creates roles, departments, employees, users, leave types, and initial balances. Assigns Manager role based on direct reports.

## Key Files
- `railway.toml` – deployment config and volume mount
- `Dockerfile` – multi-stage build
- `appsettings.Production.json` – production configuration
- `Data/ESSDataSeeder.cs` – seeding and role assignment
- `Services/LeaveApprovalService.cs` – approval + auto approval logic

## Deployed Feature Highlights
- Leave application and approval workflow
- BCEA compliant leave types
- Auto approval after 5 days
- Real time in app notifications
- Manager dashboard
- Admin statistics and balance management
- Employee directory
- Background service (auto approval)

## License
Educational / hobby use. Add a license if you plan commercial reuse.
