# Personal Universe Simulator - Docker Test Guide

This guide will help you test the entire distributed system using Docker Compose.

## Prerequisites

- Docker Desktop installed and running
- .NET 10 SDK installed
- 8GB+ RAM recommended
- Ports available: 5000-5005, 5432, 5672, 6379, 15672

## Quick Start

### 1. Set up environment variables

```bash
cp .env.example .env
```

Edit the `.env` file with your configuration. The defaults should work for Docker Compose.

### 2. Build all services

```bash
dotnet build
```

### 3. Start the entire stack

```bash
docker-compose up -d
```

This will start:
- SQL Server (port 1433)
- RabbitMQ (port 5672, management UI: 15672)
- Redis (port 6379)
- Identity Service (port 5000)
- Storage Service (port 5001)
- Personality Processing Service (port 5002)
- Simulation Engine (port 5003)
- Event Service (port 5004)
- Visualization Feed Service (port 5005)

### 4. View logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f identity-service
docker-compose logs -f simulation-engine
```

### 5. Check service health

```bash
# Check if all containers are running
docker-compose ps

# Test Identity Service
curl http://localhost:5000/health

# Test Storage Service
curl http://localhost:5001/health

# Test Simulation Engine
curl http://localhost:5003/health
```

### 6. Access management UIs

- **RabbitMQ Management**: http://localhost:15672 (guest/guest)
- **Hangfire Dashboard**: http://localhost:5003/hangfire
- **API Documentation** (Scalar):
  - Identity: http://localhost:5000/scalar/v1
  - Storage: http://localhost:5001/scalar/v1
  - Personality Processing: http://localhost:5002/scalar/v1
  - Simulation Engine: http://localhost:5003/scalar/v1
  - Event Service: http://localhost:5004/scalar/v1
  - Visualization Feed: http://localhost:5005/scalar/v1

## Testing the System

### 1. Register a User

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "Test123!"
  }'
```

Save the JWT token from the response.

### 2. Spawn a Particle

```bash
TOKEN="<your_jwt_token>"
USER_ID="<your_user_id>"

curl -X POST "http://localhost:5003/api/particles/spawn/$USER_ID" \
  -H "Authorization: Bearer $TOKEN"
```

### 3. Submit Daily Input

```bash
PARTICLE_ID="<your_particle_id>"

curl -X POST http://localhost:5002/api/personality/input \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "'$USER_ID'",
    "particleId": "'$PARTICLE_ID'",
    "question": "How are you feeling?",
    "response": "Curious and energetic!",
    "timestamp": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'"
  }'
```

### 4. Get Personality Metrics

```bash
curl -X GET "http://localhost:5002/api/personality/metrics/$PARTICLE_ID" \
  -H "Authorization: Bearer $TOKEN"
```

### 5. View Active Particles

```bash
curl -X GET http://localhost:5003/api/particles/active \
  -H "Authorization: Bearer $TOKEN"
```

### 6. Trigger Simulation Tick (Manual)

```bash
curl -X POST http://localhost:5003/api/simulation/tick \
  -H "Authorization: Bearer $TOKEN"
```

### 7. Check Events in RabbitMQ

1. Go to http://localhost:15672
2. Login with guest/guest
3. Go to "Queues" tab
4. Check messages in `personal_universe_events` exchange

## Running Integration Tests

```bash
cd tests/IntegrationTests/PersonalUniverse.IntegrationTests
dotnet test
```

The integration tests use Testcontainers to automatically spin up isolated SQL Server, RabbitMQ, and Redis containers for testing.

## Monitoring

### Check Container Status

```bash
docker-compose ps
```

### View Resource Usage

```bash
docker stats
```

### Check Database

```bash
docker exec -it personaluniverse-sql /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P YourStrong@Passw0rd \
  -Q "SELECT name FROM sys.databases"
```

### Check Redis Cache

```bash
docker exec -it personaluniverse-redis redis-cli KEYS "*"
```

## Troubleshooting

### Services won't start

```bash
# Check logs
docker-compose logs

# Rebuild images
docker-compose build --no-cache
docker-compose up -d
```

### Database connection issues

```bash
# Check SQL Server is ready
docker logs personaluniverse-sql

# Verify connection string in .env file
```

### Port conflicts

```bash
# Check what's using the ports
lsof -i :5000
lsof -i :5003

# Stop conflicting services or change ports in docker-compose.yml
```

### Clear everything and restart

```bash
# Stop and remove all containers
docker-compose down

# Remove volumes (WARNING: deletes all data)
docker-compose down -v

# Rebuild and start fresh
docker-compose build --no-cache
docker-compose up -d
```

## Performance Testing

### Load Test with Apache Bench

```bash
# Test registration endpoint
ab -n 100 -c 10 -T application/json -p register.json \
  http://localhost:5000/api/auth/register

# Test particle spawning
ab -n 100 -c 10 -H "Authorization: Bearer $TOKEN" \
  http://localhost:5003/api/particles/spawn/$USER_ID
```

### Monitor RabbitMQ Message Rate

Watch the RabbitMQ management UI to see message throughput during simulation ticks.

### Monitor Redis Cache Hit Rate

```bash
docker exec -it personaluniverse-redis redis-cli INFO stats | grep hits
```

## Cleanup

```bash
# Stop all services
docker-compose down

# Remove volumes (deletes all data)
docker-compose down -v

# Remove images
docker-compose down --rmi all
```

## Next Steps

1. ✅ All services running
2. ✅ Can register users and spawn particles
3. ✅ Daily inputs processed and metrics calculated
4. ✅ Simulation ticks executing via Hangfire
5. ✅ Events published to RabbitMQ
6. ✅ Real-time updates via SignalR
7. ✅ Redis caching active particles
8. ✅ JWT authentication protecting endpoints

## Production Deployment

For production deployment:
1. Use proper secrets management (Azure Key Vault, AWS Secrets Manager)
2. Configure health checks and monitoring
3. Set up load balancing
4. Configure proper logging (ELK stack, Application Insights)
5. Use managed services (Azure SQL, RabbitMQ Cloud, Redis Cloud)
6. Set up CI/CD pipelines
7. Configure SSL/TLS certificates
8. Implement rate limiting at API gateway level
9. Set up automated backups
10. Configure auto-scaling

## Support

For issues or questions, check:
- Logs: `docker-compose logs -f`
- RabbitMQ UI: http://localhost:15672
- Hangfire Dashboard: http://localhost:5003/hangfire
