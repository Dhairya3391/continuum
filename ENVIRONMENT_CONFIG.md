# Environment Configuration Guide

## Overview
All service URLs and connection strings are configured via appsettings.json and can be overridden with environment variables for deployment.

## Service URLs

### Local Development (Default)
- **Identity Service**: https://localhost:5001
- **Storage Service**: https://localhost:5002
- **Personality Processing**: https://localhost:5003
- **Simulation Engine**: https://localhost:5004
- **Event Service**: https://localhost:5005
- **Visualization Feed**: https://localhost:5006

### Environment Variables
Override any configuration using environment variables in this format:

```bash
# Service URLs
export Services__Identity__Url="https://identity.yourdomain.com"
export Services__Storage__Url="https://storage.yourdomain.com"
export Services__PersonalityProcessing__Url="https://personality.yourdomain.com"
export Services__SimulationEngine__Url="https://simulation.yourdomain.com"
export Services__EventService__Url="https://events.yourdomain.com"
export Services__VisualizationFeed__Url="https://visualization.yourdomain.com"

# Database Connection
export ConnectionStrings__DefaultConnection="Server=your-server;Database=PersonalUniverseDB;User Id=user;Password=pass;"

# JWT Configuration
export Jwt__SecretKey="your-production-secret-key-here"
export Jwt__Issuer="PersonalUniverseSimulator"
export Jwt__Audience="PersonalUniverseClients"

# Google OAuth
export Authentication__Google__ClientId="your-google-client-id"

# RabbitMQ Configuration
export RabbitMQ__Host="rabbitmq-server"
export RabbitMQ__Port="5672"
export RabbitMQ__Username="admin"
export RabbitMQ__Password="your-password"
export RabbitMQ__VirtualHost="/"
export RabbitMQ__ExchangeName="personal_universe_events"
```

## Docker Environment Variables
When using docker-compose, add to your .env file:

```env
# Database
DB_SERVER=sqlserver
DB_NAME=PersonalUniverseDB
DB_USER=sa
DB_PASSWORD=YourStrong@Password

# Services
IDENTITY_URL=http://identity-service:5001
STORAGE_URL=http://storage-service:5002
PERSONALITY_URL=http://personality-service:5003
SIMULATION_URL=http://simulation-service:5004
EVENT_URL=http://event-service:5005
VISUALIZATION_URL=http://visualization-service:5006

# RabbitMQ
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USER=admin
RABBITMQ_PASS=admin123

# JWT
JWT_SECRET=your-super-secret-jwt-key-minimum-32-characters-long
```

## Cloud Deployment (Azure/AWS)
Use your cloud provider's configuration service:

### Azure App Service
Set Application Settings in Azure Portal or via CLI:
```bash
az webapp config appsettings set --name your-app --resource-group your-rg --settings \
  "Services__SimulationEngine__Url=https://simulation.yourdomain.com" \
  "ConnectionStrings__DefaultConnection=Server=tcp:yourserver.database.windows.net,1433;..."
```

### AWS Elastic Beanstalk
Add to .ebextensions/environment.config or set via console

### Kubernetes ConfigMaps
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: app-config
data:
  Services__Identity__Url: "http://identity-service:5001"
  Services__Storage__Url: "http://storage-service:5002"
  # ... etc
```
