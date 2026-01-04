# Database Schema Documentation

## Overview
The Personal Universe Simulator uses SQL Server 2022 as its primary data store. The schema consists of 6 main tables and 1 view, designed for optimal performance with proper indexing.

## Entity Relationship Diagram (Text)

```
┌─────────────┐
│    Users    │
│             │
│ Id (PK)     │──┐
│ Username    │  │
│ Email       │  │
│ PasswordHash│  │
│ CreatedAt   │  │
│ AuthProvider│  │
└─────────────┘  │
                 │ 1:1
                 │
                 ▼
         ┌───────────────┐        1:N        ┌─────────────────────┐
         │   Particles   │◄──────────────────│  PersonalityMetrics │
         │               │                   │                     │
         │ Id (PK)       │                   │ Id (PK)             │
         │ UserId (FK)   │                   │ ParticleId (FK)     │
         │ PositionX     │                   │ Curiosity           │
         │ PositionY     │                   │ SocialAffinity      │
         │ VelocityX     │                   │ Aggression          │
         │ VelocityY     │                   │ Stability           │
         │ Mass          │                   │ GrowthPotential     │
         │ Energy        │                   │ CalculatedAt        │
         │ State         │                   │ Version             │
         │ LastInputAt   │                   └─────────────────────┘
         └───────────────┘
                 │
                 │ 1:N
                 │
                 ├───────────────┬─────────────────┐
                 ▼               ▼                 ▼
         ┌──────────────┐  ┌───────────────┐  ┌────────────────┐
         │ DailyInputs  │  │ParticleEvents │  │ UniverseStates │
         │              │  │               │  │                │
         │ Id (PK)      │  │ Id (PK)       │  │ Id (PK)        │
         │ UserId (FK)  │  │ ParticleId(FK)│  │ TickNumber     │
         │ ParticleId(FK)│ │ Type          │  │ Timestamp      │
         │ Type         │  │ Description   │  │ ParticleCount  │
         │ Question     │  │ OccurredAt    │  │ SnapshotData   │
         │ Response     │  └───────────────┘  └────────────────┘
         │ SubmittedAt  │
         └──────────────┘
```

## Tables

### 1. Users

**Purpose:** Store user accounts and authentication information

**Columns:**
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PRIMARY KEY | Unique user identifier |
| Username | NVARCHAR(50) | NOT NULL, UNIQUE | Display name |
| Email | NVARCHAR(255) | NOT NULL, UNIQUE | Email address (used for login) |
| PasswordHash | NVARCHAR(500) | NOT NULL | BCrypt hashed password (can be empty for OAuth) |
| CreatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Account creation timestamp |
| LastLoginAt | DATETIME2 | NULL | Last successful login |
| IsActive | BIT | NOT NULL, DEFAULT 1 | Account status |
| AuthProvider | NVARCHAR(50) | NULL, DEFAULT 'Local' | Authentication provider (Local, Google) |
| ExternalId | NVARCHAR(255) | NULL | Provider-specific user ID |
| ProfilePictureUrl | NVARCHAR(500) | NULL | URL to profile picture |

**Indexes:**
- `IX_Users_Email` - Fast email lookup for login
- `IX_Users_Username` - Fast username lookup
- `IX_Users_ExternalId` - OAuth user lookup

**Example Data:**
```sql
INSERT INTO Users (Id, Username, Email, PasswordHash, AuthProvider, ExternalId, ProfilePictureUrl)
VALUES 
    (NEWID(), 'john_doe', 'john@example.com', '$2a$11$...', 'Local', NULL, NULL),
    (NEWID(), 'jane_gmail', 'jane@gmail.com', '', 'Google', '117234567890123456789', 'https://...');
```

**Relationships:**
- 1:1 with Particles (one user has one active particle)
- 1:N with DailyInputs (one user has many inputs)

---

### 2. Particles

**Purpose:** Store particle state in the simulation universe

**Columns:**
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PRIMARY KEY | Unique particle identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL, FOREIGN KEY | Owner of the particle |
| PositionX | FLOAT | NOT NULL | X coordinate (0-1000) |
| PositionY | FLOAT | NOT NULL | Y coordinate (0-1000) |
| VelocityX | FLOAT | NOT NULL, DEFAULT 0 | X velocity component |
| VelocityY | FLOAT | NOT NULL, DEFAULT 0 | Y velocity component |
| Mass | FLOAT | NOT NULL, DEFAULT 1.0 | Particle mass (increases with merges) |
| Energy | FLOAT | NOT NULL, DEFAULT 100.0 | Current energy level (0-inf) |
| State | NVARCHAR(20) | NOT NULL, DEFAULT 'Active' | Active, Decaying, Expired, Merged |
| CreatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Particle spawn time |
| LastUpdatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Last state change |
| LastInputAt | DATETIME2 | NULL | Last daily input received |
| DecayLevel | INT | NOT NULL, DEFAULT 0 | Decay percentage (0-100) |

**Indexes:**
- `IX_Particles_UserId` - Find particle by user
- `IX_Particles_State` - Filter active particles
- `IX_Particles_Position` - Spatial queries (neighbor detection)

**State Transitions:**
```
Active → Decaying (24h no input) → Expired (100% decay)
Active → Merged (merged with another particle)
```

**Example Data:**
```sql
INSERT INTO Particles (Id, UserId, PositionX, PositionY, VelocityX, VelocityY, Mass, Energy, State)
VALUES 
    (NEWID(), @userId, 342.56, 789.23, 1.5, -0.8, 1.2, 95.5, 'Active');
```

**Relationships:**
- N:1 with Users (many particles belong to one user over time, but only 1 active)
- 1:N with PersonalityMetrics (one particle has many metric snapshots)
- 1:N with DailyInputs (one particle receives many inputs)
- 1:N with ParticleEvents (one particle has many events)

---

### 3. PersonalityMetrics

**Purpose:** Store personality trait calculations with versioning history

**Columns:**
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PRIMARY KEY | Unique record identifier |
| ParticleId | UNIQUEIDENTIFIER | NOT NULL, FOREIGN KEY | Particle these metrics belong to |
| Curiosity | FLOAT | NOT NULL, DEFAULT 0.5 | Curiosity trait (0.0-1.0) |
| SocialAffinity | FLOAT | NOT NULL, DEFAULT 0.5 | Social affinity trait (0.0-1.0) |
| Aggression | FLOAT | NOT NULL, DEFAULT 0.5 | Aggression trait (0.0-1.0) |
| Stability | FLOAT | NOT NULL, DEFAULT 0.5 | Stability trait (0.0-1.0) |
| GrowthPotential | FLOAT | NOT NULL, DEFAULT 0.5 | Growth potential trait (0.0-1.0) |
| CalculatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Calculation timestamp |
| Version | INT | NOT NULL, DEFAULT 1 | Version number (increments with each calculation) |

**Indexes:**
- `IX_PersonalityMetrics_ParticleId` - Find all metrics for a particle
- `IX_PersonalityMetrics_CalculatedAt DESC` - Get latest metrics first

**Versioning:**
- Every daily input creates a new version
- Particle interactions (merge) create new version
- Latest version is used for compatibility calculations

**Example Data:**
```sql
INSERT INTO PersonalityMetrics (Id, ParticleId, Curiosity, SocialAffinity, Aggression, Stability, GrowthPotential, Version)
VALUES 
    (NEWID(), @particleId, 0.72, 0.65, 0.31, 0.58, 0.69, 5);
```

**Query Latest Metrics:**
```sql
SELECT TOP 1 * 
FROM PersonalityMetrics 
WHERE ParticleId = @particleId 
ORDER BY Version DESC;
```

---

### 4. DailyInputs

**Purpose:** Store user daily input submissions

**Columns:**
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PRIMARY KEY | Unique input identifier |
| UserId | UNIQUEIDENTIFIER | NOT NULL, FOREIGN KEY | User who submitted input |
| ParticleId | UNIQUEIDENTIFIER | NOT NULL, FOREIGN KEY | Particle this input affects |
| Type | NVARCHAR(20) | NOT NULL | Mood, Energy, Intent, Preference, FreeText |
| Question | NVARCHAR(500) | NOT NULL | Question asked to user |
| Response | NVARCHAR(MAX) | NOT NULL | User's text response |
| NumericValue | FLOAT | NULL | Numeric slider value (0.0-1.0) if applicable |
| SubmittedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Submission timestamp |
| Processed | BIT | NOT NULL, DEFAULT 0 | Whether personality has been calculated |

**Indexes:**
- `IX_DailyInputs_UserId` - Find inputs by user
- `IX_DailyInputs_ParticleId` - Find inputs for particle
- `IX_DailyInputs_SubmittedAt DESC` - Recent inputs first
- `IX_DailyInputs_Processed` - Find unprocessed inputs

**Rate Limiting Query:**
```sql
SELECT COUNT(*) 
FROM DailyInputs 
WHERE UserId = @userId 
  AND CAST(SubmittedAt AS DATE) = @today;
```

**Example Data:**
```sql
INSERT INTO DailyInputs (Id, UserId, ParticleId, Type, Question, Response, NumericValue)
VALUES 
    (NEWID(), @userId, @particleId, 'Mood', 
     'How are you feeling today?', 
     'I''m feeling curious and energetic!', 
     0.8);
```

---

### 5. ParticleEvents

**Purpose:** Log all particle lifecycle events for history and analytics

**Columns:**
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PRIMARY KEY | Unique event identifier |
| ParticleId | UNIQUEIDENTIFIER | NOT NULL, FOREIGN KEY | Primary particle in event |
| TargetParticleId | UNIQUEIDENTIFIER | NULL | Secondary particle (for interactions) |
| Type | NVARCHAR(50) | NOT NULL | Spawned, Merged, Repelled, Expired, Interaction |
| Description | NVARCHAR(MAX) | NOT NULL | Human-readable event description |
| OccurredAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Event timestamp |
| Metadata | NVARCHAR(MAX) | NULL | JSON data with additional details |

**Indexes:**
- `IX_ParticleEvents_ParticleId` - Find events for particle
- `IX_ParticleEvents_Type` - Filter by event type
- `IX_ParticleEvents_OccurredAt DESC` - Chronological order

**Event Types:**
- **Spawned** - Particle created
- **Merged** - Particle merged with another
- **Repelled** - Particle repelled by another
- **Expired** - Particle reached end of life
- **Interaction** - Generic interaction event

**Metadata Examples:**
```json
// Merge event
{
  "sourceMass": 1.0,
  "targetMass": 1.2,
  "resultingMass": 2.2,
  "compatibility": 0.87
}

// Repel event
{
  "distance": 45.3,
  "repulsionForce": 12.5,
  "compatibility": 0.31
}
```

---

### 6. UniverseStates

**Purpose:** Store snapshots of universe state after each daily tick

**Columns:**
| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | UNIQUEIDENTIFIER | PRIMARY KEY | Unique snapshot identifier |
| TickNumber | INT | NOT NULL, UNIQUE | Incremental tick counter |
| Timestamp | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | When tick was processed |
| ActiveParticleCount | INT | NOT NULL | Number of active particles |
| AverageEnergy | FLOAT | NOT NULL | Average energy across all particles |
| InteractionCount | INT | NOT NULL | Number of interactions this tick |
| SnapshotData | NVARCHAR(MAX) | NULL | JSON serialized full state (optional) |

**Indexes:**
- `IX_UniverseStates_TickNumber DESC` - Latest ticks first
- `IX_UniverseStates_Timestamp DESC` - Chronological queries

**Example Data:**
```sql
INSERT INTO UniverseStates (Id, TickNumber, ActiveParticleCount, AverageEnergy, InteractionCount)
VALUES 
    (NEWID(), 42, 150, 87.3, 23);
```

**Analytics Queries:**
```sql
-- Universe growth over time
SELECT TickNumber, ActiveParticleCount, Timestamp
FROM UniverseStates
ORDER BY TickNumber;

-- Energy trends
SELECT TickNumber, AverageEnergy
FROM UniverseStates
WHERE Timestamp >= DATEADD(day, -30, GETUTCDATE())
ORDER BY TickNumber;
```

---

## Views

### vw_DailyInputCounts

**Purpose:** Aggregate daily input counts per user for rate limiting

**Definition:**
```sql
CREATE VIEW vw_DailyInputCounts AS
SELECT 
    UserId,
    CAST(SubmittedAt AS DATE) AS InputDate,
    COUNT(*) AS InputCount
FROM DailyInputs
GROUP BY UserId, CAST(SubmittedAt AS DATE);
```

**Usage:**
```sql
-- Check if user has reached daily limit
SELECT InputCount 
FROM vw_DailyInputCounts 
WHERE UserId = @userId 
  AND InputDate = CAST(GETUTCDATE() AS DATE);
```

---

## Indexes Strategy

### Clustered Indexes
All tables use UNIQUEIDENTIFIER as clustered primary key. In production, consider using sequential GUIDs (NEWSEQUENTIALID()) for better insert performance.

### Non-Clustered Indexes
- **Foreign Keys:** Always indexed for join performance
- **Filter Columns:** State, Type columns indexed for WHERE clauses
- **Sort Columns:** Timestamp columns with DESC for recent-first queries
- **Spatial Queries:** Composite index on (PositionX, PositionY)

---

## Data Integrity

### Foreign Key Relationships
All foreign keys have `ON DELETE CASCADE` except:
- ParticleEvents → Particles (NO ACTION, keep history)
- PersonalityMetrics → Particles (CASCADE, delete with particle)

### Constraints
- Email and Username must be unique
- All enum-like columns (State, Type) should have CHECK constraints (future enhancement)
- Personality metrics should be between 0.0 and 1.0 (future enhancement)

---

## Migration Script

**Initial Database Setup:**
```sql
-- Create database
CREATE DATABASE PersonalUniverse;
GO

USE PersonalUniverse;
GO

-- Run schema.sql
-- (Execute all CREATE TABLE statements)

-- Verify schema
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE';
```

**Sample Data for Testing:**
```sql
-- Create test user
DECLARE @userId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Users (Id, Username, Email, PasswordHash, AuthProvider)
VALUES (@userId, 'testuser', 'test@example.com', '$2a$11$...', 'Local');

-- Spawn particle
DECLARE @particleId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Particles (Id, UserId, PositionX, PositionY, Mass, Energy, State, LastInputAt)
VALUES (@particleId, @userId, 500.0, 500.0, 1.0, 100.0, 'Active', GETUTCDATE());

-- Initialize personality
INSERT INTO PersonalityMetrics (Id, ParticleId, Curiosity, SocialAffinity, Aggression, Stability, GrowthPotential, Version)
VALUES (NEWID(), @particleId, 0.5, 0.5, 0.5, 0.5, 0.5, 1);
```

---

## Performance Considerations

### Query Optimization
- Use `NOLOCK` hint for read-heavy queries where stale data is acceptable
- Implement pagination for large result sets
- Use stored procedures for complex queries

### Index Maintenance
```sql
-- Rebuild fragmented indexes monthly
ALTER INDEX ALL ON Particles REBUILD;

-- Update statistics weekly
UPDATE STATISTICS Particles WITH FULLSCAN;
```

### Archival Strategy
- Move expired particles older than 30 days to archive table
- Keep only last 90 days of DailyInputs in main table
- Compress UniverseStates snapshots older than 7 days

---

## Backup Strategy

**Daily Full Backup:**
```sql
BACKUP DATABASE PersonalUniverse 
TO DISK = '/var/opt/mssql/backup/PersonalUniverse_Full.bak'
WITH FORMAT, COMPRESSION;
```

**Hourly Transaction Log Backup:**
```sql
BACKUP LOG PersonalUniverse 
TO DISK = '/var/opt/mssql/backup/PersonalUniverse_Log.trn'
WITH COMPRESSION;
```

**Point-in-Time Recovery:**
```sql
RESTORE DATABASE PersonalUniverse_Test
FROM DISK = '/var/opt/mssql/backup/PersonalUniverse_Full.bak'
WITH NORECOVERY;

RESTORE LOG PersonalUniverse_Test
FROM DISK = '/var/opt/mssql/backup/PersonalUniverse_Log.trn'
WITH STOPAT = '2026-01-03T14:30:00',
RECOVERY;
```
