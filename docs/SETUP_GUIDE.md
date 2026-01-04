# Local Development Setup Guide

## Prerequisites

### Required Software

1. **.NET 10.0 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify installation:
     ```bash
     dotnet --version
     # Should output: 10.0.x
     ```

2. **Docker Desktop**
   - Download: https://www.docker.com/products/docker-desktop
   - Required for SQL Server, Redis, and RabbitMQ
   - Verify installation:
     ```bash
     docker --version
     docker-compose --version
     ```

3. **IDE (Choose one)**
   - **Visual Studio 2025** (Recommended for Windows)
     - Workload: ASP.NET and web development
   - **JetBrains Rider** (Cross-platform)
   - **VS Code** with C# Dev Kit extension

4. **Git**
   - Download: https://git-scm.com/downloads
   - Verify installation:
     ```bash
     git --version
     ```

### Optional Tools

- **Postman** or **Insomnia** - API testing
- **Redis Commander** - Redis GUI
- **Azure Data Studio** - SQL Server GUI
- **RabbitMQ Management** - Already included in Docker image

---

## Clone Repository

```bash
# Clone the repository
git clone https://github.com/yourusername/personal-universe-simulator.git
cd personal-universe-simulator

# Or if you already have it
cd /path/to/continuum
```

---

## Infrastructure Setup

**All infrastructure services are hosted externally:**
- **SQL Server** - Hosted on Somee.com (continuum.mssql.somee.com)
- **Redis** - Hosted on Redis Cloud (ap-south-1 region)
- **RabbitMQ** - Hosted on CloudAMQP (puffin cluster)

**No Docker required!** All connection strings are configured in the `.env` file.

### 1. Configure Environment Variables

Copy the example environment file and update if needed:

```bash
# From project root
cp .env.example .env

# Edit .env with your credentials (already configured for hosted services)
# nano .env  # or use your preferred editor
```

**Verify your .env has these connection strings:**
```env
DB_CONNECTION_STRING=Server=tcp:continuum.mssql.somee.com,1433;Initial Catalog=continuum;...
REDIS_CONNECTION_STRING=redis-16523.c212.ap-south-1-1.ec2.cloud.redislabs.com:16523,password=...
RABBITMQ_HOST=puffin.rmq2.cloudamqp.com
```

---

### 2. Initialize Database

**Database is already hosted and initialized on Somee.com**

If you need to recreate the schema or migrate to a different hosted database:

```bash
# Connect using Azure Data Studio, SSMS, or VS Code
# Server: continuum.mssql.somee.com,1433
# Username: dhairya3391_SQLLogin_1
# Password: (from .env)

# Run the schema creation script
cd infrastructure/Database
# Execute schema.sql in your SQL tool
```

**Verify database connection:**
```bash
# Test connectivity
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$(grep DB_CONNECTION_STRING .env | cut -d '=' -f2-)"
```

# Run schema
sqlcmd -S localhost,1433 -U sa -P YourStrong@Passw0rd -d PersonalUniverse -i infrastructure/Database/schema.sql
```

**Verify Database:**
```sql
USE PersonalUniverse;
GO

-- Check tables
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE';

-- Expected: Users, Particles, PersonalityMetrics, DailyInputs, ParticleEvents, UniverseStates
```

---

## Application Configuration

### 1. Update Connection Strings

Each service has an `appsettings.Development.json` file. Update connection strings if needed.

**Default configuration works with Docker services above.**

**Identity Service** (`src/Services/Identity/PersonalUniverse.Identity.API/appsettings.Development.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=PersonalUniverse;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;",
    "Redis": "localhost:6379"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest"
  },
  "Jwt": {
    "SecretKey": "your-development-secret-key-minimum-256-bits-long",
    "Issuer": "PersonalUniverse",
    "Audience": "PersonalUniverse",
    "ExpiryMinutes": 60
  },
  "Google": {
    "ClientId": "your-google-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-google-client-secret"
  }
}
```

**Copy this configuration to all service `appsettings.Development.json` files** (adjust paths):
- Storage Service
- Personality Processing Service
- Simulation Engine
- Event Service
- Visualization Feed Service

---

### 2. Generate JWT Secret Key

```bash
# Generate a secure random key
openssl rand -base64 32

# Or using PowerShell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

Update `Jwt:SecretKey` in all `appsettings.Development.json` files.

---

### 3. Google OAuth Setup (Optional)

If you want to test Google authentication:

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URI: `http://localhost:3000` (or your frontend URL)
6. Copy Client ID and Client Secret to `appsettings.Development.json`

**Skip this step if only testing local authentication.**

---

## Build & Run Services

### Option 1: Run All Services (Recommended)

Use the provided shell scripts or run manually.

**PowerShell (Windows):**
```powershell
# From project root
cd scripts
./run-all-services.ps1
```

**Bash (Mac/Linux):**
```bash
# From project root
cd scripts
chmod +x run-all-services.sh
./run-all-services.sh
```

**Manual (All Platforms):**
```bash
# From project root

# Terminal 1: Identity Service
cd src/Services/Identity/PersonalUniverse.Identity.API
dotnet run

# Terminal 2: Storage Service
cd src/Services/Storage/PersonalUniverse.Storage.API
dotnet run

# Terminal 3: Personality Processing
cd src/Services/PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API
dotnet run

# Terminal 4: Simulation Engine
cd src/Services/SimulationEngine/PersonalUniverse.SimulationEngine.API
dotnet run

# Terminal 5: Event Service
cd src/Services/EventService/PersonalUniverse.EventService.API
dotnet run

# Terminal 6: Visualization Feed
cd src/Services/VisualizationFeed/PersonalUniverse.VisualizationFeed.API
dotnet run
```

---

### Option 2: Run Individual Services

**Identity Service:**
```bash
cd src/Services/Identity/PersonalUniverse.Identity.API
dotnet restore
dotnet build
dotnet run
```

Runs on http://localhost:5001

**Repeat for other services** (ports 5002-5006)

---

### Option 3: Using Visual Studio

1. Open `PersonalUniverse.sln`
2. Right-click solution → Properties
3. Select "Multiple startup projects"
4. Set all 6 services to "Start"
5. Click "Apply" and "OK"
6. Press F5 to run all services

---

### Option 4: Using Docker Compose (All Services)

```bash
# Build images
docker-compose -f docker-compose.services.yml build

# Run all services
docker-compose -f docker-compose.services.yml up
```

**Note:** This also starts SQL Server, Redis, and RabbitMQ if not already running.

---

## Verify Installation

### 1. Check Service Health

```bash
# Identity Service
curl http://localhost:5001/api/auth/health

# Personality Processing
curl http://localhost:5003/api/personality/health

# Simulation Engine
curl http://localhost:5004/api/simulation/health

# Visualization Feed
curl http://localhost:5006/api/broadcast/health
```

**Expected response for each:**
```json
{
  "status": "healthy",
  "service": "Identity Service"
}
```

---

### 2. Test Registration

```bash
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "Test123!@#"
  }'
```

**Expected response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "user": {
    "id": "...",
    "username": "testuser",
    "email": "test@example.com"
  }
}
```

---

### 3. Test Particle Spawn

```bash
# Use token from registration
export JWT_TOKEN="your-jwt-token-here"

curl -X POST http://localhost:5004/api/particles/spawn/{userId} \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

### 4. Access Scalar API Documentation

Open in browser:
- http://localhost:5001/scalar/v1 (Identity)
- http://localhost:5003/scalar/v1 (Personality Processing)
- http://localhost:5004/scalar/v1 (Simulation Engine)
- http://localhost:5006/scalar/v1 (Visualization Feed)

---

### 5. Access Hangfire Dashboard

http://localhost:5004/hangfire

**Default credentials:** No authentication in development

---

### 6. Check RabbitMQ

http://localhost:15672

**Credentials:** guest/guest

**Verify:**
- Exchange `personal-universe-exchange` exists
- Queues are created when events are published

---

## Troubleshooting

### Issue: Services won't start

**Check ports are available:**
```bash
# Mac/Linux
lsof -i :5001
lsof -i :5002
lsof -i :5003
lsof -i :5004
lsof -i :5005
lsof -i :5006

# Windows
netstat -ano | findstr :5001
netstat -ano | findstr :5002
```

**Kill conflicting processes or change ports in `appsettings.Development.json`:**
```json
{
  "Urls": "http://localhost:5010" // Change port
}
```

---

### Issue: Cannot connect to SQL Server

**Check Docker container:**
```bash
docker ps | grep sql-server
docker logs sql-server
```

**Test connection:**
```bash
sqlcmd -S localhost,1433 -U sa -P YourStrong@Passw0rd -Q "SELECT @@VERSION"
```

**If container not running:**
```bash
cd infrastructure
docker-compose up -d sql-server
```

---

### Issue: Redis connection timeout

**Check Redis:**
```bash
docker logs redis
redis-cli ping
# Should return: PONG
```

**Restart Redis:**
```bash
docker-compose restart redis
```

---

### Issue: RabbitMQ not accessible

**Check RabbitMQ:**
```bash
docker logs rabbitmq
```

**Wait for RabbitMQ to fully start** (can take 30-60 seconds)

**Verify management UI:** http://localhost:15672

---

### Issue: Hangfire tables not created

**Hangfire auto-creates tables on first run.** If they don't appear:

```sql
USE PersonalUniverse;
GO

-- Check for Hangfire schema
SELECT SCHEMA_NAME 
FROM INFORMATION_SCHEMA.SCHEMATA 
WHERE SCHEMA_NAME = 'HangFire';
```

**If missing, restart Simulation Engine** - it will create tables automatically.

---

### Issue: JWT validation fails

**Ensure all services use the same JWT secret key:**
```bash
# Check each appsettings.Development.json
grep -r "SecretKey" src/Services/*/*/appsettings.Development.json
```

**All should match!**

---

## Development Workflow

### 1. Make Code Changes

Edit files in your IDE.

### 2. Hot Reload (No Restart)

**.NET 10 supports hot reload:**
```bash
dotnet watch run
```

Changes to code will auto-reload without restarting.

### 3. Manual Restart

If hot reload doesn't work:
```bash
# Stop service (Ctrl+C)
# Start again
dotnet run
```

### 4. Database Migrations

**When schema changes:**
```sql
-- Apply changes manually in SQL tool
ALTER TABLE Particles ADD NewColumn INT;

-- Or update schema.sql and re-run
sqlcmd -S localhost,1433 -U sa -P YourStrong@Passw0rd -d PersonalUniverse -i infrastructure/Database/schema.sql
```

---

## IDE Configuration

### Visual Studio Code

**Install Extensions:**
- C# Dev Kit
- Docker
- REST Client
- SQL Server (mssql)

**Launch Configuration** (`.vscode/launch.json`):
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Identity Service",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Services/Identity/PersonalUniverse.Identity.API/bin/Debug/net10.0/PersonalUniverse.Identity.API.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/Services/Identity/PersonalUniverse.Identity.API",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
    // Add similar configurations for other services
  ],
  "compounds": [
    {
      "name": "All Services",
      "configurations": ["Identity Service", "Storage Service", "Personality Processing", "Simulation Engine", "Event Service", "Visualization Feed"]
    }
  ]
}
```

---

### Visual Studio 2025

**Configure Multiple Startup Projects:**
1. Right-click solution
2. Properties → Startup Project
3. Select "Multiple startup projects"
4. Set all 6 services to "Start"
5. Apply and OK

**Environment Variables:**
- Project Properties → Debug → Environment Variables
- Add: `ASPNETCORE_ENVIRONMENT=Development`

---

### JetBrains Rider

**Run Configurations:**
1. Add Configuration → .NET Launch Settings Profile
2. Select each service's launchSettings.json
3. Create compound configuration with all services

---

## Testing

### Unit Tests

```bash
# Run all tests
dotnet test

# Run tests for specific project
cd tests/PersonalUniverse.Tests
dotnet test
```

### Integration Tests

```bash
# Ensure Docker services are running
cd infrastructure
docker-compose up -d

# Run integration tests
cd tests/PersonalUniverse.IntegrationTests
dotnet test
```

---

## Logging

### View Logs

**Console output** shows structured logs by default.

**Customize log level** in `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "PersonalUniverse": "Debug"
    }
  }
}
```

---

## Next Steps

- ✅ All services running
- ✅ Database initialized
- ✅ Can register users and spawn particles

**Now you can:**
1. Test APIs using Scalar documentation
2. Monitor background jobs in Hangfire dashboard
3. Watch RabbitMQ messages in management UI
4. Start developing the frontend
5. Review [API_DOCUMENTATION.md](./API_DOCUMENTATION.md) for endpoint details

---

## Common Commands

```bash
# Rebuild solution
dotnet clean
dotnet build

# Restore NuGet packages
dotnet restore

# Run with specific environment
dotnet run --environment Production

# Watch for changes (hot reload)
dotnet watch run

# Generate user secrets
dotnet user-secrets init
dotnet user-secrets set "Jwt:SecretKey" "your-secret"

# List running .NET processes
dotnet --info

# Stop all Docker services
docker-compose down

# View Docker logs
docker-compose logs -f sql-server
```

---

## Support

If you encounter issues:
1. Check [Troubleshooting](#troubleshooting) section
2. Verify all Docker services are running
3. Check service logs for errors
4. Ensure ports are not in use
5. Verify .NET 10 SDK is installed

For project-specific issues, refer to individual service documentation in `/docs` folder.
