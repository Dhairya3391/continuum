# Cloud Deployment Guide

## Overview
This guide covers deploying the Personal Universe Simulator to cloud environments, specifically Azure and AWS. The architecture is containerized and can be deployed using various strategies.

**Deployment Options:**
1. **Azure Container Apps** (Recommended for Azure)
2. **AWS ECS/Fargate** (Recommended for AWS)
3. **Kubernetes (AKS/EKS)** (Advanced)
4. **Azure App Service** (Simpler, less flexible)

---

## Pre-Deployment Checklist

### 1. Code Preparation

- [ ] Update `appsettings.Production.json` with production configuration
- [ ] Remove development secrets from code
- [ ] Enable HTTPS redirection
- [ ] Configure CORS for production domains
- [ ] Update JWT secret key (strong, unique)
- [ ] Set production database connection strings
- [ ] Configure production Redis connection
- [ ] Set up production RabbitMQ credentials
- [ ] Test all services locally with production configuration
- [ ] Run all unit and integration tests

### 2. Build Docker Images

```bash
# From project root
cd src/Services/Identity/PersonalUniverse.Identity.API
docker build -t personaluniverse/identity:latest -f Dockerfile ../../..

cd ../../../Storage/PersonalUniverse.Storage.API
docker build -t personaluniverse/storage:latest -f Dockerfile ../../..

cd ../../../PersonalityProcessing/PersonalUniverse.PersonalityProcessing.API
docker build -t personaluniverse/personality:latest -f Dockerfile ../../..

cd ../../../SimulationEngine/PersonalUniverse.SimulationEngine.API
docker build -t personaluniverse/simulation:latest -f Dockerfile ../../..

cd ../../../EventService/PersonalUniverse.EventService.API
docker build -t personaluniverse/events:latest -f Dockerfile ../../..

cd ../../../VisualizationFeed/PersonalUniverse.VisualizationFeed.API
docker build -t personaluniverse/visualization:latest -f Dockerfile ../../..
```

### 3. Test Images Locally

```bash
# Test each image
docker run -p 5001:8080 personaluniverse/identity:latest
docker run -p 5003:8080 personaluniverse/personality:latest
# etc.
```

---

## Azure Deployment

### Option 1: Azure Container Apps (Recommended)

#### Prerequisites

```bash
# Install Azure CLI
az --version

# Login
az login

# Set subscription
az account set --subscription "Your Subscription Name"
```

#### 1. Create Resource Group

```bash
az group create \
  --name personal-universe-rg \
  --location eastus
```

#### 2. Create Container Registry

```bash
# Create ACR
az acr create \
  --resource-group personal-universe-rg \
  --name personaluniverseacr \
  --sku Basic \
  --admin-enabled true

# Login to ACR
az acr login --name personaluniverseacr

# Get login credentials
az acr credential show --name personaluniverseacr
```

#### 3. Push Images to ACR

```bash
# Tag images for ACR
docker tag personaluniverse/identity:latest personaluniverseacr.azurecr.io/identity:latest
docker tag personaluniverse/storage:latest personaluniverseacr.azurecr.io/storage:latest
docker tag personaluniverse/personality:latest personaluniverseacr.azurecr.io/personality:latest
docker tag personaluniverse/simulation:latest personaluniverseacr.azurecr.io/simulation:latest
docker tag personaluniverse/events:latest personaluniverseacr.azurecr.io/events:latest
docker tag personaluniverse/visualization:latest personaluniverseacr.azurecr.io/visualization:latest

# Push to ACR
docker push personaluniverseacr.azurecr.io/identity:latest
docker push personaluniverseacr.azurecr.io/storage:latest
docker push personaluniverseacr.azurecr.io/personality:latest
docker push personaluniverseacr.azurecr.io/simulation:latest
docker push personaluniverseacr.azurecr.io/events:latest
docker push personaluniverseacr.azurecr.io/visualization:latest
```

#### 4. Create Azure SQL Database

```bash
# Create SQL Server
az sql server create \
  --name personaluniverse-sql \
  --resource-group personal-universe-rg \
  --location eastus \
  --admin-user sqladmin \
  --admin-password 'YourStrongP@ssw0rd!'

# Create firewall rule (allow Azure services)
az sql server firewall-rule create \
  --resource-group personal-universe-rg \
  --server personaluniverse-sql \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create database
az sql db create \
  --resource-group personal-universe-rg \
  --server personaluniverse-sql \
  --name PersonalUniverse \
  --service-objective S0 \
  --zone-redundant false

# Get connection string
az sql db show-connection-string \
  --client ado.net \
  --server personaluniverse-sql \
  --name PersonalUniverse
```

**Initialize Database:**
```bash
# Connect and run schema.sql
sqlcmd -S personaluniverse-sql.database.windows.net -U sqladmin -P 'YourStrongP@ssw0rd!' -d PersonalUniverse -i infrastructure/Database/schema.sql
```

#### 5. Create Azure Cache for Redis

```bash
az redis create \
  --resource-group personal-universe-rg \
  --name personaluniverse-redis \
  --location eastus \
  --sku Basic \
  --vm-size c0

# Get connection string
az redis show \
  --resource-group personal-universe-rg \
  --name personaluniverse-redis \
  --query "hostName" -o tsv

az redis list-keys \
  --resource-group personal-universe-rg \
  --name personaluniverse-redis
```

#### 6. Create Azure Service Bus (RabbitMQ Alternative)

**Option A: Use RabbitMQ in Azure Container Instance**

```bash
az container create \
  --resource-group personal-universe-rg \
  --name rabbitmq \
  --image rabbitmq:3-management \
  --dns-name-label personaluniverse-rabbitmq \
  --ports 5672 15672 \
  --cpu 1 \
  --memory 2 \
  --environment-variables \
    RABBITMQ_DEFAULT_USER=admin \
    RABBITMQ_DEFAULT_PASS='YourRabbitMQPassword!'
```

**Option B: Use Azure Service Bus**

```bash
az servicebus namespace create \
  --resource-group personal-universe-rg \
  --name personaluniverse-sb \
  --location eastus \
  --sku Standard

az servicebus topic create \
  --resource-group personal-universe-rg \
  --namespace-name personaluniverse-sb \
  --name personal-universe-events

# Get connection string
az servicebus namespace authorization-rule keys list \
  --resource-group personal-universe-rg \
  --namespace-name personaluniverse-sb \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

#### 7. Create Container App Environment

```bash
az containerapp env create \
  --name personal-universe-env \
  --resource-group personal-universe-rg \
  --location eastus
```

#### 8. Deploy Container Apps

**Identity Service:**
```bash
az containerapp create \
  --name identity-service \
  --resource-group personal-universe-rg \
  --environment personal-universe-env \
  --image personaluniverseacr.azurecr.io/identity:latest \
  --registry-server personaluniverseacr.azurecr.io \
  --registry-username <ACR_USERNAME> \
  --registry-password <ACR_PASSWORD> \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 5 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection='<SQL_CONNECTION_STRING>' \
    ConnectionStrings__Redis='<REDIS_CONNECTION_STRING>' \
    RabbitMQ__Host='<RABBITMQ_HOST>' \
    RabbitMQ__Username='admin' \
    RabbitMQ__Password='<RABBITMQ_PASSWORD>' \
    Jwt__SecretKey='<JWT_SECRET>' \
    Jwt__Issuer='https://personaluniverse.com' \
    Jwt__Audience='https://personaluniverse.com'
```

**Repeat for other services:**
```bash
# Storage Service
az containerapp create --name storage-service ...

# Personality Processing Service
az containerapp create --name personality-service ...

# Simulation Engine
az containerapp create --name simulation-service ...

# Event Service
az containerapp create --name event-service ...

# Visualization Feed Service
az containerapp create --name visualization-service ...
```

#### 9. Configure Custom Domain (Optional)

```bash
# Add custom domain
az containerapp hostname add \
  --hostname api.personaluniverse.com \
  --resource-group personal-universe-rg \
  --name identity-service

# Bind SSL certificate
az containerapp hostname bind \
  --hostname api.personaluniverse.com \
  --resource-group personal-universe-rg \
  --name identity-service \
  --certificate <CERTIFICATE_ID>
```

#### 10. Set Up Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
  --app personal-universe-insights \
  --location eastus \
  --resource-group personal-universe-rg

# Get instrumentation key
az monitor app-insights component show \
  --app personal-universe-insights \
  --resource-group personal-universe-rg \
  --query instrumentationKey -o tsv

# Update container apps with instrumentation key
az containerapp update \
  --name identity-service \
  --resource-group personal-universe-rg \
  --set-env-vars \
    APPLICATIONINSIGHTS_CONNECTION_STRING='<CONNECTION_STRING>'
```

---

### Option 2: Azure Kubernetes Service (AKS)

#### 1. Create AKS Cluster

```bash
az aks create \
  --resource-group personal-universe-rg \
  --name personal-universe-aks \
  --node-count 3 \
  --node-vm-size Standard_DS2_v2 \
  --enable-addons monitoring \
  --generate-ssh-keys

# Get credentials
az aks get-credentials \
  --resource-group personal-universe-rg \
  --name personal-universe-aks
```

#### 2. Create Kubernetes Deployments

**deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: identity-service
spec:
  replicas: 2
  selector:
    matchLabels:
      app: identity-service
  template:
    metadata:
      labels:
        app: identity-service
    spec:
      containers:
      - name: identity
        image: personaluniverseacr.azurecr.io/identity:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-connection
              key: connection-string
        - name: ConnectionStrings__Redis
          valueFrom:
            secretKeyRef:
              name: redis-connection
              key: connection-string
---
apiVersion: v1
kind: Service
metadata:
  name: identity-service
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 8080
  selector:
    app: identity-service
```

#### 3. Create Secrets

```bash
kubectl create secret generic db-connection \
  --from-literal=connection-string='<SQL_CONNECTION_STRING>'

kubectl create secret generic redis-connection \
  --from-literal=connection-string='<REDIS_CONNECTION_STRING>'

kubectl create secret generic rabbitmq-credentials \
  --from-literal=username='admin' \
  --from-literal=password='<RABBITMQ_PASSWORD>'

kubectl create secret generic jwt-secret \
  --from-literal=secret-key='<JWT_SECRET_KEY>'
```

#### 4. Deploy to AKS

```bash
kubectl apply -f deployment.yaml
kubectl get services
kubectl get pods
```

---

## AWS Deployment

### Option 1: AWS ECS with Fargate

#### Prerequisites

```bash
# Install AWS CLI
aws --version

# Configure credentials
aws configure
```

#### 1. Create ECR Repositories

```bash
# Create repositories
aws ecr create-repository --repository-name personaluniverse/identity
aws ecr create-repository --repository-name personaluniverse/storage
aws ecr create-repository --repository-name personaluniverse/personality
aws ecr create-repository --repository-name personaluniverse/simulation
aws ecr create-repository --repository-name personaluniverse/events
aws ecr create-repository --repository-name personaluniverse/visualization

# Login to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com
```

#### 2. Push Images to ECR

```bash
# Tag images
docker tag personaluniverse/identity:latest <AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/personaluniverse/identity:latest

# Push images
docker push <AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/personaluniverse/identity:latest
# Repeat for all services
```

#### 3. Create RDS Instance (SQL Server)

```bash
aws rds create-db-instance \
  --db-instance-identifier personaluniverse-db \
  --db-instance-class db.t3.medium \
  --engine sqlserver-ex \
  --master-username admin \
  --master-user-password 'YourStrongPassword!' \
  --allocated-storage 20 \
  --vpc-security-group-ids sg-xxxxxxxx \
  --publicly-accessible
```

#### 4. Create ElastiCache (Redis)

```bash
aws elasticache create-cache-cluster \
  --cache-cluster-id personaluniverse-redis \
  --cache-node-type cache.t3.micro \
  --engine redis \
  --num-cache-nodes 1 \
  --security-group-ids sg-xxxxxxxx
```

#### 5. Create ECS Cluster

```bash
aws ecs create-cluster --cluster-name personal-universe-cluster
```

#### 6. Create Task Definition

**task-definition.json:**
```json
{
  "family": "identity-service",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "containerDefinitions": [
    {
      "name": "identity",
      "image": "<AWS_ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/personaluniverse/identity:latest",
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__DefaultConnection",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:ACCOUNT_ID:secret:db-connection"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/personal-universe",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "identity"
        }
      }
    }
  ]
}
```

**Register task:**
```bash
aws ecs register-task-definition --cli-input-json file://task-definition.json
```

#### 7. Create ECS Service

```bash
aws ecs create-service \
  --cluster personal-universe-cluster \
  --service-name identity-service \
  --task-definition identity-service \
  --desired-count 2 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[subnet-xxxxxx],securityGroups=[sg-xxxxxx],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=arn:aws:elasticloadbalancing:us-east-1:ACCOUNT_ID:targetgroup/identity-tg,containerName=identity,containerPort=8080"
```

---

## Production Configuration

### appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "PersonalUniverse": "Information"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=personaluniverse-sql.database.windows.net;Database=PersonalUniverse;User Id=sqladmin;Password=${SQL_PASSWORD};Encrypt=true;TrustServerCertificate=false;",
    "Redis": "personaluniverse-redis.redis.cache.windows.net:6380,password=${REDIS_PASSWORD},ssl=True,abortConnect=False"
  },
  "RabbitMQ": {
    "Host": "personaluniverse-rabbitmq.eastus.azurecontainer.io",
    "Port": 5672,
    "Username": "admin",
    "Password": "${RABBITMQ_PASSWORD}",
    "VirtualHost": "/"
  },
  "Jwt": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "https://api.personaluniverse.com",
    "Audience": "https://personaluniverse.com",
    "ExpiryMinutes": 60
  },
  "Google": {
    "ClientId": "${GOOGLE_CLIENT_ID}",
    "ClientSecret": "${GOOGLE_CLIENT_SECRET}"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://personaluniverse.com",
      "https://www.personaluniverse.com"
    ]
  },
  "ApplicationInsights": {
    "ConnectionString": "${APPINSIGHTS_CONNECTION_STRING}"
  }
}
```

---

## Environment Variables

### Azure Container Apps

Set via CLI:
```bash
az containerapp update \
  --name identity-service \
  --resource-group personal-universe-rg \
  --set-env-vars \
    SQL_PASSWORD=secretref:sql-password \
    REDIS_PASSWORD=secretref:redis-password \
    RABBITMQ_PASSWORD=secretref:rabbitmq-password \
    JWT_SECRET_KEY=secretref:jwt-secret
```

### Kubernetes Secrets

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: app-secrets
type: Opaque
data:
  sql-password: <base64-encoded-password>
  redis-password: <base64-encoded-password>
  rabbitmq-password: <base64-encoded-password>
  jwt-secret-key: <base64-encoded-secret>
```

---

## SSL/TLS Configuration

### Azure Application Gateway

```bash
az network application-gateway create \
  --name personaluniverse-gateway \
  --resource-group personal-universe-rg \
  --location eastus \
  --sku Standard_v2 \
  --http-settings-cookie-based-affinity Disabled \
  --frontend-port 443 \
  --http-settings-port 8080 \
  --http-settings-protocol Http \
  --public-ip-address gateway-public-ip \
  --vnet-name personal-universe-vnet \
  --subnet gateway-subnet \
  --cert-file certificate.pfx \
  --cert-password 'CertPassword'
```

---

## Monitoring & Logging

### Application Insights

**Automatic Instrumentation:**
```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

**Custom Metrics:**
```csharp
_telemetryClient.TrackMetric("ParticleCount", activeParticles.Count);
_telemetryClient.TrackEvent("DailyTickCompleted");
```

### Log Analytics

Query logs in Azure Portal:
```kusto
traces
| where timestamp > ago(1h)
| where severityLevel >= 3
| project timestamp, message, severityLevel
| order by timestamp desc
```

---

## Scaling Configuration

### Azure Container Apps

**Autoscaling Rules:**
```bash
az containerapp update \
  --name simulation-service \
  --resource-group personal-universe-rg \
  --min-replicas 1 \
  --max-replicas 10 \
  --scale-rule-name cpu-scale \
  --scale-rule-type cpu \
  --scale-rule-metadata "type=Utilization" "value=70"
```

### Kubernetes HPA

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: identity-service-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: identity-service
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

---

## CI/CD Pipeline

### Azure DevOps

**azure-pipelines.yml:**
```yaml
trigger:
- main

pool:
  vmImage: 'ubuntu-latest'

stages:
- stage: Build
  jobs:
  - job: BuildAndPush
    steps:
    - task: Docker@2
      inputs:
        containerRegistry: 'personaluniverseacr'
        repository: 'personaluniverse/identity'
        command: 'buildAndPush'
        Dockerfile: 'src/Services/Identity/PersonalUniverse.Identity.API/Dockerfile'

- stage: Deploy
  jobs:
  - deployment: DeployToAzure
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureCLI@2
            inputs:
              azureSubscription: 'Azure-Subscription'
              scriptType: 'bash'
              scriptLocation: 'inlineScript'
              inlineScript: |
                az containerapp update \
                  --name identity-service \
                  --resource-group personal-universe-rg \
                  --image personaluniverseacr.azurecr.io/identity:$(Build.BuildId)
```

---

## Post-Deployment

### 1. Verify Services

```bash
# Check health endpoints
curl https://api.personaluniverse.com/api/auth/health
curl https://api.personaluniverse.com/api/personality/health
curl https://api.personaluniverse.com/api/simulation/health
```

### 2. Run Smoke Tests

```bash
# Register user
curl -X POST https://api.personaluniverse.com/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","email":"test@example.com","password":"Test123!@#"}'
```

### 3. Monitor Metrics

- Check Application Insights dashboard
- Verify Hangfire jobs are running
- Monitor RabbitMQ message flow
- Check database connections

---

## Estimated Costs

### Azure (Monthly)

| Service | Tier | Cost |
|---------|------|------|
| Container Apps (6 services) | 1 vCPU, 2 GB | ~$150 |
| Azure SQL Database | S0 | ~$15 |
| Azure Cache for Redis | Basic C0 | ~$17 |
| Azure Container Instance (RabbitMQ) | 1 CPU, 2 GB | ~$30 |
| Application Insights | Pay-as-you-go | ~$20 |
| **Total** | | **~$232/month** |

### AWS (Monthly)

| Service | Tier | Cost |
|---------|------|------|
| ECS Fargate (6 tasks) | 0.5 vCPU, 1 GB | ~$130 |
| RDS SQL Server | db.t3.medium | ~$120 |
| ElastiCache Redis | cache.t3.micro | ~$13 |
| Application Load Balancer | | ~$23 |
| CloudWatch Logs | 10 GB | ~$5 |
| **Total** | | **~$291/month** |

---

## Best Practices

- ✅ Use managed services (Azure SQL, Redis, etc.)
- ✅ Enable auto-scaling
- ✅ Set up monitoring and alerts
- ✅ Use secrets management (Key Vault, Secrets Manager)
- ✅ Enable HTTPS everywhere
- ✅ Implement health checks
- ✅ Use connection pooling
- ✅ Enable logging and tracing
- ✅ Set up CI/CD pipeline
- ✅ Regular backups

---

## Support

For deployment issues:
1. Check service health endpoints
2. Review application logs (Application Insights/CloudWatch)
3. Verify environment variables are set correctly
4. Check network security groups/firewalls
5. Verify database connectivity

**Next Steps:**
- Set up custom domain
- Configure CDN for frontend
- Implement backup strategy
- Set up disaster recovery
