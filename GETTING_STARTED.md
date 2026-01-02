# Getting Started with Personal Universe Simulator

## Setup and Hosting Guide

### 1. Prerequisites

Before running the project, ensure you have:

- **.NET 10.0 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **SQL Server** - LocalDB (installed with Visual Studio) or full instance
- **SQL Server Management Studio** (optional, for database management)
- **Docker Desktop** (optional, for containerized deployment)
- **Git** (for version control)

### 2. Database Setup

#### Option A: Using LocalDB (Simplest)

1. Ensure LocalDB is running:
```bash
sqllocaldb info
```

2. If not running, start it:
```bash
sqllocaldb start MSSQLLocalDB
```

3. Create and initialize the database:
```bash
# Connect to LocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE PersonalUniverseDB"

# Run the schema
sqlcmd -S "(localdb)\MSSQLLocalDB" -d PersonalUniverseDB -i infrastructure/Database/schema.sql
```

#### Option B: Using Docker SQL Server

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
   -p 1433:1433 --name sqlserver \
   -d mcr.microsoft.com/mssql/server:2022-latest

# Wait a few seconds for SQL Server to start, then create database
sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -Q "CREATE DATABASE PersonalUniverseDB"

# Run schema
sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -d PersonalUniverseDB -i infrastructure/Database/schema.sql
```

### 3. Update Configuration

Update connection strings in service `appsettings.json` files:

**For LocalDB:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=PersonalUniverseDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

**For Docker SQL Server:**
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=PersonalUniverseDB;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
}
```

**IMPORTANT:** Update the JWT SecretKey in Identity Service:
```json
"Jwt": {
  "SecretKey": "your-super-secret-key-minimum-32-characters-long-for-jwt-signing",
  "Issuer": "PersonalUniverseSimulator",
  "Audience": "PersonalUniverseClients",
  "ExpirationMinutes": "60"
}
```

### 4. Build and Run

#### Build the Solution

```bash
cd /Users/dhairya/Development/continuum
dotnet build PersonalUniverseSimulator.sln
```

#### Run Individual Services

Open multiple terminal windows and run:

**Terminal 1 - Identity Service:**
```bash
cd src/Services/Identity/PersonalUniverse.Identity.API
dotnet run
# Runs on: https://localhost:5001
```

**Terminal 2 - Storage Service:**
```bash
cd src/Services/Storage/PersonalUniverse.Storage.API
dotnet run
# Runs on: https://localhost:5002
```

### 5. Test the APIs

#### Using Swagger UI

1. Identity Service: Open `https://localhost:5001/swagger`
2. Storage Service: Open `https://localhost:5002/swagger`

#### Using cURL

**Register a new user:**
```bash
curl -X POST "https://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "SecurePassword123!"
  }' \
  -k
```

**Login:**
```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePassword123!"
  }' \
  -k
```

**Health Check:**
```bash
curl "https://localhost:5001/api/auth/health" -k
```

### 6. Run with Docker Compose (Complete Stack)

To run all services together:

```bash
# Build all images
docker-compose build

# Start all services
docker-compose up

# Or run in detached mode
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

Service URLs when running with Docker:
- Identity: http://localhost:5001
- Storage: http://localhost:5002
- Personality: http://localhost:5003
- Simulation: http://localhost:5004
- Events: http://localhost:5005
- Visualization: http://localhost:5006
- RabbitMQ Management: http://localhost:15672 (admin/admin123)

### 7. Verify Database Tables

Connect to your database and verify tables were created:

```sql
USE PersonalUniverseDB;

-- List all tables
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE';

-- Should show: Users, Particles, PersonalityMetrics, DailyInputs, ParticleEvents, UniverseStates
```

### 8. Development Workflow

1. **Make code changes** in your preferred editor (VS Code, Visual Studio, Rider)
2. **Build the solution** to check for compilation errors
3. **Run specific services** you're working on
4. **Test endpoints** using Swagger UI or Postman
5. **Commit changes** to version control

### 9. Troubleshooting

**Port already in use:**
```bash
# On macOS/Linux
lsof -i :5001
kill -9 <PID>

# Or change port in Properties/launchSettings.json
```

**Database connection issues:**
- Verify SQL Server is running
- Check connection string format
- Ensure TrustServerCertificate=True is set
- For LocalDB, try `(localdb)\MSSQLLocalDB` or `(localdb)\v15.0`

**SSL Certificate errors:**
```bash
# Trust development certificates
dotnet dev-certs https --trust
```

**Missing packages:**
```bash
# Restore all packages
dotnet restore PersonalUniverseSimulator.sln
```

### 10. Next Steps

Once the basic services are running:

1. **Implement Simulation Engine** - Add particle physics and interaction logic
2. **Add RabbitMQ Integration** - Set up event publishing/subscribing
3. **Create SignalR Hubs** - Enable real-time visualization updates
4. **Build Frontend** - Create a web client to visualize the universe
5. **Add Background Jobs** - Implement daily processing with Hangfire
6. **Deploy to Cloud** - Set up Azure/AWS hosting

### Additional Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Dapper Tutorial](https://github.com/DapperLib/Dapper)
- [JWT Authentication in .NET](https://jwt.io/)
- [SignalR Guide](https://docs.microsoft.com/aspnet/core/signalr/)
- [Docker Compose Reference](https://docs.docker.com/compose/)

### Getting Help

For issues or questions:
1. Check the main [README.md](README.md) for architecture overview
2. Review the [AGENTS.md](AGENTS.md) for project requirements
3. Examine service logs for error messages
4. Verify all prerequisites are installed correctly

---

**Happy Coding! 🚀**
