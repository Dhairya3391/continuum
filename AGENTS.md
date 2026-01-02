# Project Concept: "Personal Universe Simulator"
A hosted, distributed, real-time life-simulation where each user controls an abstract “particle” representing their evolving personality. Users provide small daily inputs, and the simulation processes those inputs to determine growth, decay, and interactions with other particles. The system visualizes how these personality-agents behave inside a shared digital universe.

## Core Purpose
- Minimal daily user commitment (2–3 inputs per day)
- Personality-driven behavior modeled through numeric traits
- Real-time visual output rendered from backend simulation
- Event-driven processing of outcomes
- Hosted publicly for demonstration

---

# High-Level Features

## User Inputs
- Daily questions or sliders regarding mood, energy, intent, preference
- Short-form text that can be sentiment-analyzed or rule-classified
- Limited number of actions per day per user to constrain activity

## Personality Metrics
Converted into numeric values such as:
- Curiosity
- Social affinity
- Aggression/avoidance
- Stability/decay resistance
- Growth potential

These metrics affect interaction decisions in the simulation.

## Particle Lifecycle
- Particles spawn when a user registers
- Particles decay without daily input
- Particles can merge, repel, bond, split, or expire based on metrics
- Daily processing cycles produce a history log for review

## Interaction Rules
- Neighbor scanning inside the simulation grid
- Compatibility thresholds
- Influence modifiers based on personality metrics
- Daily evaluation of outcomes
- Event logs made available to the user

## Output
- Real-time visualization of particle movements
- Daily summary of particle events
- Historical snapshots of universe states
- Optional universe replay

---

# Core Technical Components

## Microservices (Suggested Separation)
1. **Identity Service**
   - Authentication
   - JWT management

2. **Personality Processing Service**
   - Maps user responses to personality metrics
   - Runs sentiment or category classification

3. **Simulation Engine**
   - Physics/movement rules
   - Collision/interaction decisions
   - Daily processing

4. **Event Service**
   - Publishes events such as merges, repulsions, deaths
   - Connects to message queue

5. **Visualization Feed Service**
   - Pushes real-time simulation frames to clients over SignalR

6. **Storage Service**
   - Handles data persistence with Dapper repositories

## Data Layer (Dapper/SQL)
Tables may include:
- Users
- Personality metrics snapshots
- Daily logs
- Universe state
- Particles and attributes
- Event history

## Caching
Used for:
- Active universe state
- Frequently accessed particle attributes
- Real-time read optimization

## Real-Time Communication (SignalR)
- Streams particle positions and interactions to clients
- Supports subscription per-user or per-universe

## Messaging (RabbitMQ or equivalent)
Used for:
- Interaction results
- Particle lifecycle events
- Daily summary triggers

## Background Jobs (Hangfire)
Possible tasks:
- Daily personality processing
- Decay calculations
- Cleanup of inactive particles
- Universe archival

## Authentication
- JWT bearer token for API access
- Optional anonymous session identities

## Rate Limiting
- Limits user submissions per day
- Prevents excessive particle influence

## Cloud Deployment
- Hosted microservices
- Containerized (optional)
- Managed SQL database
- Object storage for snapshots or replays

---

# Data Flow Overview

1. User submits daily input.
2. API passes input to Personality Processing Service.
3. Metrics updated and stored via Dapper.
4. Simulation Engine executes daily tick cycle.
5. Interaction results published through RabbitMQ.
6. SignalR streams updated state to client.
7. Background jobs archive or decay unused entities.

---

# Front-End Concepts (Optional Suggestions)
- Visual grid or orbital canvas representing universe
- Motion-based rendering of particles
- Clickable particle history
- Daily report screen

---

# AI/Logic Approaches (Optional)
- Sentiment analysis on text inputs
- Rule-based mapping of answers to numeric traits
- Lightweight classifier for interaction preference
- No requirement for heavy LLM integration

---

# Stretch Goals
- Multiple universes
- Cross-universe travel based on thresholds
- Seasonal events
- Gamified streak mechanics

---

# Deliverables for Academic Review
- UML or component diagrams
- Microservice communication map
- Deployment architecture (cloud)
- API documentation
- Demonstrable hosted URL
- Screenshots or video of simulation output

---

# Evaluation Fit
- Distributed architecture
- Real-time communication
- Event-driven patterns
- Data access layer (Dapper)
- Authentication & rate limiting
- Background processing
- Cloud hosting
- Optional caching and logging