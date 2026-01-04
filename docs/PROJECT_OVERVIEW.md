# Personal Universe Simulator - Project Overview

## 🌌 The Concept

Personal Universe Simulator is a hosted, distributed, real-time life simulation where each user controls an abstract "particle" representing their evolving personality. Users provide small daily inputs, and the simulation processes those inputs to determine growth, decay, and interactions with other particles in a shared digital universe.

## 💡 The Idea Behind It

### Core Philosophy
The project explores the metaphor of human personalities as particles in a physics simulation. Just as particles in the physical universe interact based on fundamental properties (mass, charge, velocity), personality particles interact based on psychological traits (curiosity, social affinity, aggression, stability, growth potential).

### Daily Commitment Design
The system is intentionally designed for minimal user engagement:
- **2-3 inputs per day maximum** (enforced by rate limiting)
- **Simple questions** about mood, energy, intent, and preferences
- **Short-form text responses** that are sentiment-analyzed
- **Daily processing cycles** that compute all interactions once per day

This constraint creates several benefits:
1. Reduces user fatigue and ensures long-term engagement
2. Makes the simulation computationally feasible
3. Creates anticipation as users wait for daily results
4. Mimics real-life pace of personality development

## 🎯 Project Goals

### Primary Objectives
1. **Demonstrate Distributed Architecture** - Six independent microservices communicating via REST, RabbitMQ, and SignalR
2. **Real-time Processing** - Event-driven system with background jobs and live updates
3. **Personality Modeling** - Convert subjective human responses into numeric traits
4. **Physics-based Interactions** - Particles attract, repel, merge, or bond based on compatibility
5. **Cloud Deployment** - Production-ready system hosted publicly

### Academic Evaluation Fit
This project satisfies multiple academic requirements:
- ✅ Distributed architecture with microservices
- ✅ Real-time communication (SignalR)
- ✅ Event-driven patterns (RabbitMQ pub/sub)
- ✅ Data persistence layer (Dapper + SQL Server)
- ✅ Authentication & authorization (JWT + OAuth)
- ✅ Rate limiting and security
- ✅ Background processing (Hangfire)
- ✅ Caching strategies (Redis)
- ✅ Cloud hosting ready

## 🧠 How It Works

### User Journey
1. **Registration** → User creates account (local or Google OAuth)
2. **Particle Spawn** → A particle is created at random position in universe
3. **Daily Input** → User answers 2-3 questions about their current state
4. **Personality Calculation** → Sentiment analysis converts responses to numeric traits
5. **Daily Tick** → At midnight UTC, simulation processes all particle movements
6. **Interactions** → Particles near each other interact based on personality compatibility
7. **Real-time Updates** → Users see their particle move, interact, merge, or repel
8. **Decay System** → Inactive particles (no input for 24 hours) start decaying
9. **History** → All interactions and state changes are logged

### Personality Metrics
Each particle has five core traits (0.0 to 1.0 scale):

1. **Curiosity** - Drives exploration, influences growth potential
2. **Social Affinity** - Determines attraction to other particles
3. **Aggression** - Causes repulsion and competitive behavior
4. **Stability** - Resistance to decay and external influence
5. **Growth Potential** - Rate of personality evolution

These metrics are calculated from:
- Text sentiment analysis (60+ keyword detection)
- Numeric slider responses
- Input patterns over time
- Historical personality versions

### Particle Interactions
When particles are within interaction radius (50 units):

**Merge** (Compatibility > 0.85)
- Two particles combine into one
- Mass and energy are summed
- Personality traits are averaged
- Resulting particle is more stable

**Bond** (Compatibility 0.65 - 0.85)
- Particles align their velocities
- Move together temporarily
- Slight personality influence on each other

**Attract** (Compatibility 0.50 - 0.65)
- Gentle gravitational pull
- Particles drift closer over time

**Repel** (Compatibility < 0.50)
- Strong opposing force
- Particles pushed apart
- Higher aggression = stronger repulsion

### Compatibility Calculation
```
Compatibility = Weighted Average of:
  - Curiosity similarity (20%)
  - Social Affinity similarity (30%)
  - Aggression difference (inverted, 20%)
  - Stability average (15%)
  - Growth Potential similarity (15%)
```

### Decay System
Particles require daily input to stay active:
- **0-24 hours** without input → Active state maintained
- **24-48 hours** → Decaying state begins (10% decay per 6-hour check)
- **48+ hours** or 100% decay → Expired state (soft delete after 30 days)

## 🏗️ System Architecture

### Microservices (6 total)
1. **Identity Service** (Port 5001) - Authentication, JWT, Google OAuth
2. **Storage Service** (Port 5002) - Data access layer with Dapper
3. **Personality Processing** (Port 5003) - Sentiment analysis, trait calculation
4. **Simulation Engine** (Port 5004) - Physics, interactions, daily tick
5. **Event Service** (Port 5005) - RabbitMQ event publishing/consuming
6. **Visualization Feed** (Port 5006) - SignalR real-time broadcasting

### Shared Libraries (2)
1. **PersonalUniverse.Shared.Models** - Entities, DTOs, Mappers
2. **PersonalUniverse.Shared.Contracts** - Interfaces, Events

### External Dependencies
- **SQL Server** - Primary data store
- **RabbitMQ** - Message broker for events
- **Redis** - Caching layer for active particles
- **Hangfire** - Background job scheduler
- **Google OAuth 2.0** - Third-party authentication

## 📊 Key Metrics

### Universe Properties
- **Size**: 1000 x 1000 units (toroidal wraparound)
- **Interaction Radius**: 50 units
- **Max Particles**: Unlimited (scales with database)
- **Daily Tick Time**: Midnight UTC
- **Decay Check Frequency**: Every 6 hours

### Performance Characteristics
- **Daily Input Limit**: 3 per user per day (application-level)
- **API Rate Limit**: 5-30 requests/minute (varies by service)
- **Cache TTL**: 5-60 minutes (varies by data type)
- **Event Processing**: Asynchronous with guaranteed delivery

## 🎓 Educational Value

### Concepts Demonstrated
1. **Microservices Communication** - REST APIs, message queues, real-time protocols
2. **Event Sourcing Patterns** - All particle lifecycle events are published
3. **CQRS Lite** - Read/write separation via caching layer
4. **Background Job Processing** - Scheduled tasks with Hangfire
5. **Rate Limiting Strategies** - Multiple levels of protection
6. **Authentication Flows** - Local + OAuth 2.0 with JWT
7. **Data Access Patterns** - Repository pattern with Dapper
8. **Real-time Push** - SignalR hubs and groups
9. **Containerization** - Docker Compose orchestration
10. **Cloud-Ready Design** - Environment-based configuration

## 🚀 Future Enhancements

### Planned (Not Yet Implemented)
- Multiple parallel universes
- Cross-universe travel based on energy thresholds
- Seasonal events (solar flares, black holes)
- Gamified streak mechanics
- User achievements and leaderboards
- Enhanced sentiment analysis with NLP models
- Machine learning for personality prediction

### Technical Improvements
- Distributed tracing (OpenTelemetry)
- Centralized logging (Seq, ELK)
- Metrics dashboard (Prometheus + Grafana)
- Horizontal scaling with Kubernetes
- GraphQL API layer
- WebAssembly client for better visualization

## 📝 Documentation Structure

This documentation suite includes:
- **ARCHITECTURE.md** - Complete system architecture with diagrams
- **TECHNOLOGIES.md** - All technologies used and integration details
- **MICROSERVICES.md** - Detailed explanation of each service
- **DATABASE_SCHEMA.md** - Database design and relationships
- **API_DOCUMENTATION.md** - All REST endpoints documented
- **EVENT_SYSTEM.md** - RabbitMQ events and patterns
- **AUTHENTICATION.md** - JWT and Google OAuth flows
- **CACHING_STRATEGY.md** - Redis caching patterns
- **BACKGROUND_JOBS.md** - Hangfire scheduled tasks
- **SETUP_GUIDE.md** - Local development setup
- **DEPLOYMENT_GUIDE.md** - Cloud deployment instructions
