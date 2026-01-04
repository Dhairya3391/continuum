# API Documentation

## Overview
This document provides comprehensive documentation for all REST API endpoints across the 6 microservices in the Personal Universe Simulator.

**Base URLs (Local Development):**
- Identity Service: `http://localhost:5001`
- Storage Service: `http://localhost:5002`
- Personality Processing Service: `http://localhost:5003`
- Simulation Engine: `http://localhost:5004`
- Event Service: `http://localhost:5005`
- Visualization Feed Service: `http://localhost:5006`

**Authentication:**
Most endpoints require JWT authentication. Include the token in the Authorization header:
```
Authorization: Bearer <jwt_token>
```

---

## Identity Service (Port 5001)

### Authentication & User Management

#### POST /api/auth/register
Register a new user account.

**Authentication:** None required

**Request Body:**
```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "SecureP@ss123"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_here",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "john_doe",
    "email": "john@example.com",
    "createdAt": "2026-01-03T10:30:00Z",
    "authProvider": "Local"
  }
}
```

**Error Responses:**
- `400 Bad Request` - Validation error or user already exists
```json
{
  "error": "Email already exists"
}
```

**Example:**
```bash
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john_doe",
    "email": "john@example.com",
    "password": "SecureP@ss123"
  }'
```

---

#### POST /api/auth/login
Authenticate with email and password.

**Authentication:** None required

**Request Body:**
```json
{
  "email": "john@example.com",
  "password": "SecureP@ss123"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_here",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "john_doe",
    "email": "john@example.com",
    "lastLoginAt": "2026-01-03T10:45:00Z",
    "authProvider": "Local"
  }
}
```

**Error Responses:**
- `400 Bad Request` - Missing credentials
- `401 Unauthorized` - Invalid credentials
```json
{
  "error": "Invalid email or password"
}
```

**Example:**
```bash
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "john@example.com",
    "password": "SecureP@ss123"
  }'
```

---

#### POST /api/auth/google
Authenticate with Google OAuth.

**Authentication:** None required

**Request Body:**
```json
{
  "idToken": "google_id_token_from_client"
}
```

**Success Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_here",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "Jane Doe",
    "email": "jane@gmail.com",
    "authProvider": "Google",
    "externalId": "117234567890123456789",
    "profilePictureUrl": "https://lh3.googleusercontent.com/..."
  }
}
```

**Error Responses:**
- `400 Bad Request` - Missing token
- `401 Unauthorized` - Invalid Google token

**Flow:**
1. Client obtains Google ID token from Google Sign-In
2. Send token to this endpoint
3. Backend validates token with Google
4. Returns JWT for subsequent requests

**Example (Frontend - JavaScript):**
```javascript
// After Google Sign-In on client
google.accounts.id.initialize({
  client_id: 'YOUR_GOOGLE_CLIENT_ID',
  callback: async (response) => {
    const res = await fetch('http://localhost:5001/api/auth/google', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ idToken: response.credential })
    });
    const data = await res.json();
    localStorage.setItem('jwt', data.token);
  }
});
```

---

#### GET /api/auth/health
Health check endpoint.

**Authentication:** None required

**Success Response (200 OK):**
```json
{
  "status": "healthy",
  "service": "Identity Service"
}
```

---

## Personality Processing Service (Port 5003)

### Daily Input & Personality Calculation

#### POST /api/personality/input
Submit daily user input for personality calculation.

**Authentication:** Required (JWT)

**Request Body:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "Mood",
  "question": "How are you feeling today?",
  "response": "I'm feeling curious and energetic!",
  "numericValue": 0.8
}
```

**Fields:**
- `type`: Mood | Energy | Intent | Preference | FreeText
- `numericValue`: Optional slider value (0.0-1.0)

**Success Response (200 OK):**
```json
{
  "success": true,
  "message": "Input processed successfully",
  "personalityMetrics": {
    "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "curiosity": 0.72,
    "socialAffinity": 0.65,
    "aggression": 0.31,
    "stability": 0.58,
    "growthPotential": 0.69,
    "version": 5,
    "calculatedAt": "2026-01-03T10:50:00Z"
  },
  "dailyInputId": "9fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Error Responses:**
- `400 Bad Request` - Validation error or rate limit exceeded
```json
{
  "error": "Daily input limit reached (3/day)"
}
```
- `401 Unauthorized` - Missing/invalid JWT

**Rate Limiting:**
- Max 3 inputs per user per day
- Window resets at midnight UTC
- Additional rate limit: 10 requests/minute per user

**Example:**
```bash
curl -X POST http://localhost:5003/api/personality/input \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <jwt_token>" \
  -d '{
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "type": "Mood",
    "question": "How are you feeling today?",
    "response": "I am curious about learning new things!",
    "numericValue": 0.8
  }'
```

---

#### GET /api/personality/metrics/{particleId}
Retrieve current personality metrics for a particle.

**Authentication:** Required (JWT)

**Path Parameters:**
- `particleId` - UUID of the particle

**Success Response (200 OK):**
```json
{
  "id": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
  "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "curiosity": 0.72,
  "socialAffinity": 0.65,
  "aggression": 0.31,
  "stability": 0.58,
  "growthPotential": 0.69,
  "version": 5,
  "calculatedAt": "2026-01-03T10:50:00Z"
}
```

**Error Responses:**
- `404 Not Found` - No metrics found for particle

**Example:**
```bash
curl -X GET http://localhost:5003/api/personality/metrics/7fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer <jwt_token>"
```

---

#### GET /api/personality/input-count/{userId}
Check daily input usage for rate limiting.

**Authentication:** Required (JWT)

**Path Parameters:**
- `userId` - UUID of the user

**Success Response (200 OK):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "count": 2,
  "maxAllowed": 3,
  "remaining": 1
}
```

**Example:**
```bash
curl -X GET http://localhost:5003/api/personality/input-count/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer <jwt_token>"
```

---

#### GET /api/personality/health
Health check endpoint.

**Authentication:** Required (JWT)

**Success Response (200 OK):**
```json
{
  "status": "healthy",
  "service": "Personality Processing"
}
```

---

## Simulation Engine (Port 5004)

### Particle Management

#### POST /api/particles/spawn/{userId}
Spawn a new particle for a user.

**Authentication:** Required (JWT)

**Path Parameters:**
- `userId` - UUID of the user

**Success Response (200 OK):**
```json
{
  "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "positionX": 456.78,
  "positionY": 789.23,
  "velocityX": 0.0,
  "velocityY": 0.0,
  "mass": 1.0,
  "energy": 100.0,
  "state": "Active",
  "createdAt": "2026-01-03T11:00:00Z",
  "lastInputAt": null,
  "decayLevel": 0
}
```

**Error Responses:**
- `500 Internal Server Error` - Failed to spawn particle

**Example:**
```bash
curl -X POST http://localhost:5004/api/particles/spawn/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer <jwt_token>"
```

---

#### GET /api/particles/active
Get all active particles in the universe.

**Authentication:** Required (JWT)

**Success Response (200 OK):**
```json
[
  {
    "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "positionX": 456.78,
    "positionY": 789.23,
    "velocityX": 1.5,
    "velocityY": -0.8,
    "mass": 1.2,
    "energy": 95.5,
    "state": "Active",
    "decayLevel": 0
  },
  {
    "id": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userId": "4fa85f64-5717-4562-b3fc-2c963f66afa6",
    "positionX": 123.45,
    "positionY": 567.89,
    "velocityX": -2.1,
    "velocityY": 1.3,
    "mass": 1.0,
    "energy": 88.2,
    "state": "Active",
    "decayLevel": 15
  }
]
```

**Example:**
```bash
curl -X GET http://localhost:5004/api/particles/active \
  -H "Authorization: Bearer <jwt_token>"
```

---

#### GET /api/particles/user/{userId}
Get particle for a specific user.

**Authentication:** Required (JWT)

**Path Parameters:**
- `userId` - UUID of the user

**Success Response (200 OK):**
```json
{
  "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "positionX": 456.78,
  "positionY": 789.23,
  "velocityX": 1.5,
  "velocityY": -0.8,
  "mass": 1.2,
  "energy": 95.5,
  "state": "Active",
  "lastInputAt": "2026-01-03T10:50:00Z",
  "decayLevel": 0
}
```

**Error Responses:**
- `404 Not Found` - Particle not found for user

**Example:**
```bash
curl -X GET http://localhost:5004/api/particles/user/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer <jwt_token>"
```

---

#### PUT /api/particles/update
Update particle state manually.

**Authentication:** Required (JWT)

**Request Body:**
```json
{
  "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "positionX": 460.0,
  "positionY": 790.0,
  "velocityX": 1.6,
  "velocityY": -0.7,
  "energy": 94.0,
  "state": "Active",
  "decayLevel": 5
}
```

**Success Response (200 OK):**
```json
{
  "message": "Particle updated successfully"
}
```

**Error Responses:**
- `404 Not Found` - Particle not found

**Example:**
```bash
curl -X PUT http://localhost:5004/api/particles/update \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <jwt_token>" \
  -d '{
    "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "energy": 90.0,
    "decayLevel": 10
  }'
```

---

### Simulation Management

#### GET /api/simulation/state
Get current universe state snapshot.

**Authentication:** None required

**Success Response (200 OK):**
```json
{
  "tickNumber": 42,
  "timestamp": "2026-01-03T12:00:00Z",
  "activeParticleCount": 150,
  "averageEnergy": 87.3,
  "interactionCount": 23,
  "particles": [
    {
      "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
      "positionX": 456.78,
      "positionY": 789.23,
      "velocityX": 1.5,
      "velocityY": -0.8,
      "mass": 1.2,
      "energy": 95.5,
      "state": "Active"
    }
  ]
}
```

**Example:**
```bash
curl -X GET http://localhost:5004/api/simulation/state
```

---

#### POST /api/simulation/tick
Manually trigger simulation tick (normally runs automatically via Hangfire).

**Authentication:** None required

**Success Response (200 OK):**
```json
{
  "message": "Simulation tick processed successfully"
}
```

**Error Responses:**
- `500 Internal Server Error` - Tick processing failed

**Example:**
```bash
curl -X POST http://localhost:5004/api/simulation/tick
```

---

#### GET /api/simulation/neighbors/{particleId}?radius=50
Find neighboring particles within radius.

**Authentication:** None required

**Path Parameters:**
- `particleId` - UUID of the particle

**Query Parameters:**
- `radius` - Detection radius (default: 50.0)

**Success Response (200 OK):**
```json
[
  {
    "particle": {
      "id": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
      "positionX": 480.0,
      "positionY": 795.0,
      "mass": 1.0,
      "energy": 88.2
    },
    "distance": 25.3,
    "compatibility": 0.75
  },
  {
    "particle": {
      "id": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "positionX": 500.0,
      "positionY": 810.0,
      "mass": 1.5,
      "energy": 92.0
    },
    "distance": 48.7,
    "compatibility": 0.42
  }
]
```

**Example:**
```bash
curl -X GET "http://localhost:5004/api/simulation/neighbors/7fa85f64-5717-4562-b3fc-2c963f66afa6?radius=50"
```

---

#### GET /api/simulation/health
Health check endpoint.

**Authentication:** None required

**Success Response (200 OK):**
```json
{
  "status": "healthy",
  "service": "Simulation Engine"
}
```

---

## Visualization Feed Service (Port 5006)

### Broadcasting & Real-Time Updates

#### POST /api/broadcast/universe-state
Receive universe state from Simulation Engine and broadcast to all connected clients (Internal use).

**Authentication:** None required

**Request Body:**
```json
{
  "tickNumber": 42,
  "timestamp": "2026-01-03T12:00:00Z",
  "activeParticleCount": 150,
  "particles": [
    {
      "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "positionX": 456.78,
      "positionY": 789.23,
      "velocityX": 1.5,
      "velocityY": -0.8,
      "mass": 1.2,
      "energy": 95.5,
      "state": "Active",
      "decayLevel": 0
    }
  ]
}
```

**Success Response (200 OK):**
```json
{
  "message": "Universe state broadcasted successfully"
}
```

**Side Effect:**
Triggers SignalR broadcast to all clients connected to `/hubs/universe`.

---

#### POST /api/broadcast/particle-update
Broadcast individual particle update to followers (Internal use).

**Authentication:** None required

**Request Body:**
```json
{
  "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "positionX": 460.0,
  "positionY": 795.0,
  "velocityX": 1.6,
  "velocityY": -0.7,
  "mass": 1.2,
  "energy": 94.0,
  "state": "Active",
  "decayLevel": 5
}
```

**Success Response (200 OK):**
```json
{
  "message": "Particle update broadcasted successfully"
}
```

---

#### GET /api/stream/info
Get SignalR hub connection information.

**Authentication:** None required

**Success Response (200 OK):**
```json
{
  "hubUrl": "/hubs/universe",
  "supportedMethods": [
    "UniverseStateUpdate",
    "ParticleUpdate",
    "ActiveParticlesUpdate",
    "ParticleEvent",
    "SimulationMetrics"
  ],
  "clientMethods": [
    "JoinUniverse",
    "LeaveUniverse",
    "FollowParticle",
    "UnfollowParticle"
  ]
}
```

**Example:**
```bash
curl -X GET http://localhost:5006/api/stream/info
```

---

#### GET /api/broadcast/health
Health check endpoint.

**Authentication:** None required

**Success Response (200 OK):**
```json
{
  "status": "healthy",
  "service": "Broadcast"
}
```

---

## SignalR Hub (/hubs/universe)

### Real-Time WebSocket Connection

**Hub URL:** `http://localhost:5006/hubs/universe`

#### Client-Side Connection (JavaScript):
```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5006/hubs/universe")
  .configureLogging(signalR.LogLevel.Information)
  .build();

// Connect
await connection.start();
console.log("Connected to Universe Hub");

// Join universe to receive broadcasts
await connection.invoke("JoinUniverse");

// Follow specific particle
await connection.invoke("FollowParticle", particleId);

// Unfollow particle
await connection.invoke("UnfollowParticle", particleId);

// Leave universe
await connection.invoke("LeaveUniverse");
```

#### Server-to-Client Events:

**UniverseStateUpdate**
Receives full universe state snapshot.
```javascript
connection.on("UniverseStateUpdate", (state) => {
  console.log("Universe tick:", state.tickNumber);
  console.log("Active particles:", state.particles.length);
  // Update visualization
});
```

**ActiveParticlesUpdate**
Receives list of all active particles.
```javascript
connection.on("ActiveParticlesUpdate", (particles) => {
  console.log("Particles:", particles);
  // Update particle display
});
```

**ParticleUpdate**
Receives update for followed particle.
```javascript
connection.on("ParticleUpdate", (particle) => {
  console.log("Particle position:", particle.positionX, particle.positionY);
  console.log("Energy:", particle.energy);
  // Update specific particle visualization
});
```

**ParticleEvent**
Receives interaction events.
```javascript
connection.on("ParticleEvent", (event) => {
  console.log("Event:", event.type);
  console.log("Description:", event.description);
  // Show notification
});
```

**SimulationMetrics**
Receives simulation statistics.
```javascript
connection.on("SimulationMetrics", (metrics) => {
  console.log("Average energy:", metrics.averageEnergy);
  console.log("Interaction count:", metrics.interactionCount);
  // Update metrics display
});
```

---

## Storage Service (Port 5002)

The Storage Service primarily provides internal repository access for other services and doesn't expose public REST endpoints. It's accessed via direct dependency injection.

**Internal Repositories:**
- `IUserRepository`
- `IParticleRepository`
- `IPersonalityMetricsRepository`
- `IDailyInputRepository`
- `IParticleEventRepository`
- `IUniverseStateRepository`

---

## Event Service (Port 5005)

The Event Service manages RabbitMQ message publishing and consumption. It doesn't expose REST endpoints as it operates entirely through message queues.

**Published Events:**
- `ParticleSpawnedEvent`
- `ParticleMergedEvent`
- `ParticleRepelledEvent`
- `ParticleExpiredEvent`
- `InteractionOccurredEvent`
- `PersonalityUpdatedEvent`

See [EVENT_SYSTEM.md](./EVENT_SYSTEM.md) for detailed event documentation.

---

## Error Response Format

All services follow a consistent error response format:

**400 Bad Request:**
```json
{
  "error": "Descriptive error message"
}
```

**401 Unauthorized:**
```json
{
  "error": "Invalid or missing authentication token"
}
```

**404 Not Found:**
```json
{
  "error": "Resource not found"
}
```

**500 Internal Server Error:**
```json
{
  "error": "Internal server error",
  "details": "Optional detailed error message"
}
```

---

## Rate Limiting

### Application-Level Rate Limiting

**Personality Processing Service:**
- Daily input limit: 3 submissions per user per day
- General rate limit: 10 requests per minute per user

**Simulation Engine:**
- Particle spawn: 1 per user per 24 hours
- General endpoints: 100 requests per minute per IP

### Implementation Details:
```csharp
// ASP.NET Core Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

---

## CORS Configuration

All services are configured with CORS to allow cross-origin requests during development:

**Allowed Origins:**
- `http://localhost:3000` (React dev server)
- `http://localhost:5173` (Vite dev server)
- `http://localhost:8080` (Vue/Angular dev server)

**Allowed Methods:** GET, POST, PUT, DELETE, OPTIONS

**Allowed Headers:** Authorization, Content-Type

**Production:** Configure specific allowed origins in `appsettings.Production.json`

---

## Swagger/Scalar Documentation

Interactive API documentation is available via Scalar:

**Local Development URLs:**
- Identity Service: http://localhost:5001/scalar/v1
- Personality Processing: http://localhost:5003/scalar/v1
- Simulation Engine: http://localhost:5004/scalar/v1
- Visualization Feed: http://localhost:5006/scalar/v1

These provide:
- Interactive API testing
- Request/response examples
- Schema definitions
- Authentication testing

---

## Complete API Flow Example

### User Registration → Daily Input → Visualization

```bash
# 1. Register user
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "test_user",
    "email": "test@example.com",
    "password": "Test123!"
  }'

# Response: {"token": "...", "user": {"id": "user-id", ...}}

# 2. Spawn particle
curl -X POST http://localhost:5004/api/particles/spawn/user-id \
  -H "Authorization: Bearer <token>"

# Response: {"id": "particle-id", "positionX": 456.78, ...}

# 3. Submit daily input
curl -X POST http://localhost:5003/api/personality/input \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "userId": "user-id",
    "particleId": "particle-id",
    "type": "Mood",
    "question": "How are you feeling?",
    "response": "Curious and energetic!",
    "numericValue": 0.8
  }'

# Response: {"success": true, "personalityMetrics": {...}}

# 4. Get particle state
curl -X GET http://localhost:5004/api/particles/user/user-id \
  -H "Authorization: Bearer <token>"

# Response: Updated particle with new personality-driven velocities

# 5. Connect to SignalR for real-time updates
# (Client-side JavaScript shown in SignalR section)
```

---

## Monitoring & Observability

### Health Check Endpoints

All services expose `/health` endpoints for monitoring:

```bash
# Check all services
curl http://localhost:5001/api/auth/health
curl http://localhost:5003/api/personality/health
curl http://localhost:5004/api/simulation/health
curl http://localhost:5006/api/broadcast/health
```

### Logging

All services use ASP.NET Core logging with configurable levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Log Categories:**
- `PersonalUniverse.Identity` - Authentication logs
- `PersonalUniverse.SimulationEngine` - Physics calculations
- `PersonalUniverse.PersonalityProcessing` - Sentiment analysis
- `PersonalUniverse.EventService` - Message queue operations

---

## Security Best Practices

### JWT Token Management
- Tokens expire after 60 minutes
- Refresh tokens valid for 7 days
- Store tokens securely (HttpOnly cookies recommended for web)
- Never expose tokens in URLs

### Password Requirements
- Minimum 8 characters
- Must contain uppercase, lowercase, digit, and special character
- BCrypt hashing with work factor 11

### API Security
- Always use HTTPS in production
- Validate all user inputs
- Use parameterized queries (Dapper handles this)
- Rate limiting enabled on all endpoints
- CORS configured for specific origins only

---

## Version Information

**API Version:** 1.0  
**Last Updated:** January 2026  
**.NET Version:** 10.0  
**OpenAPI Specification:** 3.0
