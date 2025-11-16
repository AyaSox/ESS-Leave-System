# ESS Leave System (Razor Pages, .NET 8)

A lightweight Employee Self-Service Leave Management system built with ASP.NET Core Razor Pages (.NET 8). Optimized for simple deployment (Docker + Railway) and hobbyist/portfolio use.

## Features
- South African BCEA-compliant leave types (Annual, Sick, Family Responsibility, Maternity, Paternity, Study, Unpaid)
- Roles: Admin, Manager, HR, Employee
- Manager role assigned automatically if a user has direct reports
- Leave approval workflow (manager approval) + Admin/HR overrides
- Auto-approval after 5 days pending (urgent reminder at day 4)
- Leave balances per year, tracked and updated automatically
- Notifications (UI dropdown) for approvals, rejections, auto-approvals
- Employee directory and basic profiles
- Health check endpoint: `/health`

## Tech Stack
- .NET 8 + Razor Pages
- ASP.NET Core Identity (cookie auth)
- Entity Framework Core (SQLite by default)
- Docker multi-stage build
- Railway deployment with persistent volume

## Quick Start (Local)
Prerequisites: .NET 8 SDK

```
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
- A user is a Manager if any employee has `LineManagerId` equal to their `EmployeeId` (assigned during seeding and can be recalculated in services).

## Docker (Local)
```
# From ESSLeaveSystem/
 docker build -t ess-leave-system .

# Run with persistent volume for SQLite data
 docker run -d -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -v essleave-data:/app/data \
  ess-leave-system

# Visit http://localhost:8080
```

## Railway Deployment (SQLite with Persistent Volume)
This repo includes `railway.toml` and production settings for persistence.

- `appsettings.Production.json` uses `Data Source=/app/data/essleave.db`
- `railway.toml` mounts a persistent volume at `/app/data`
- Dockerfile creates `/app/data` at build time

Deploy steps:
1) Push to GitHub (main branch)
2) Connect repo to Railway
3) Railway auto-builds Dockerfile and deploys

Data persistence:
- SQLite file is stored in Railway volume `/app/data/essleave.db`
- Data survives restarts and redeployments

Limitations of SQLite:
- Single instance only (no horizontal scaling)
- Manual backups recommended (download `/app/data/essleave.db` periodically)

## Configuration
- Connection string (Production): `ConnectionStrings:DefaultConnection` ? `Data Source=/app/data/essleave.db`
- Health check: GET `/health`
- Auto-approval window: set in `Services/LeaveApprovalService.cs`
  - `AUTO_APPROVE_DAYS = 5`
  - `URGENT_REMINDER_DAYS = 4`

## Development Notes
- Target Framework: .NET 8
- Project Type: Razor Pages
- Seeder: `Data/ESSDataSeeder.cs` creates roles, departments, employees, users, leave types, and initial balances.
- Manager relationships seeded; users with direct reports receive the `Manager` role.

## Scripts/Docs
- `railway.toml` – Railway deploy config with persistent volume
- `Dockerfile` – Multi-stage Docker build
- `RAILWAY_PERSISTENT_DATABASE_FIX.md` – Rationale and details for persistence
- `MANAGER_ROLE_ASSIGNMENT_GUIDE.md` – How manager roles are assigned

## License
This project is for educational/hobby use. Add a license if you plan to share or reuse commercially.
