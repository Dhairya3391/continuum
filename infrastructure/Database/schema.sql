-- Personal Universe Simulator Database Schema

-- Users Table
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    -- OAuth fields
    AuthProvider NVARCHAR(50) NULL DEFAULT 'Local', -- 'Local', 'Google', etc.
    ExternalId NVARCHAR(255) NULL, -- Google User ID or other provider ID
    ProfilePictureUrl NVARCHAR(500) NULL,
    INDEX IX_Users_Email (Email),
    INDEX IX_Users_Username (Username),
    INDEX IX_Users_ExternalId (ExternalId)
);

-- Particles Table
CREATE TABLE Particles (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    PositionX FLOAT NOT NULL,
    PositionY FLOAT NOT NULL,
    VelocityX FLOAT NOT NULL DEFAULT 0,
    VelocityY FLOAT NOT NULL DEFAULT 0,
    Mass FLOAT NOT NULL DEFAULT 1.0,
    Energy FLOAT NOT NULL DEFAULT 100.0,
    State NVARCHAR(20) NOT NULL DEFAULT 'Active',
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastUpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastInputAt DATETIME2 NULL,
    DecayLevel INT NOT NULL DEFAULT 0,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX IX_Particles_UserId (UserId),
    INDEX IX_Particles_State (State),
    INDEX IX_Particles_Position (PositionX, PositionY)
);

-- PersonalityMetrics Table
CREATE TABLE PersonalityMetrics (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ParticleId UNIQUEIDENTIFIER NOT NULL,
    Curiosity FLOAT NOT NULL DEFAULT 0.5,
    SocialAffinity FLOAT NOT NULL DEFAULT 0.5,
    Aggression FLOAT NOT NULL DEFAULT 0.5,
    Stability FLOAT NOT NULL DEFAULT 0.5,
    GrowthPotential FLOAT NOT NULL DEFAULT 0.5,
    CalculatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Version INT NOT NULL DEFAULT 1,
    FOREIGN KEY (ParticleId) REFERENCES Particles(Id) ON DELETE CASCADE,
    INDEX IX_PersonalityMetrics_ParticleId (ParticleId),
    INDEX IX_PersonalityMetrics_CalculatedAt (CalculatedAt DESC)
);

-- DailyInputs Table
CREATE TABLE DailyInputs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ParticleId UNIQUEIDENTIFIER NOT NULL,
    Type NVARCHAR(20) NOT NULL,
    Question NVARCHAR(500) NOT NULL,
    Response NVARCHAR(MAX) NOT NULL,
    NumericValue FLOAT NULL,
    SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Processed BIT NOT NULL DEFAULT 0,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParticleId) REFERENCES Particles(Id),
    INDEX IX_DailyInputs_UserId (UserId),
    INDEX IX_DailyInputs_ParticleId (ParticleId),
    INDEX IX_DailyInputs_SubmittedAt (SubmittedAt DESC),
    INDEX IX_DailyInputs_Processed (Processed)
);

-- ParticleEvents Table
CREATE TABLE ParticleEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ParticleId UNIQUEIDENTIFIER NOT NULL,
    TargetParticleId UNIQUEIDENTIFIER NULL,
    Type NVARCHAR(50) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Metadata NVARCHAR(MAX) NULL, -- JSON data
    FOREIGN KEY (ParticleId) REFERENCES Particles(Id),
    INDEX IX_ParticleEvents_ParticleId (ParticleId),
    INDEX IX_ParticleEvents_Type (Type),
    INDEX IX_ParticleEvents_OccurredAt (OccurredAt DESC)
);

-- UniverseStates Table
CREATE TABLE UniverseStates (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TickNumber INT NOT NULL UNIQUE,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ActiveParticleCount INT NOT NULL,
    AverageEnergy FLOAT NOT NULL,
    InteractionCount INT NOT NULL,
    SnapshotData NVARCHAR(MAX) NULL, -- JSON serialized state
    INDEX IX_UniverseStates_TickNumber (TickNumber DESC),
    INDEX IX_UniverseStates_Timestamp (Timestamp DESC)
);

-- DailyInputLimits View (for rate limiting)
CREATE VIEW vw_DailyInputCounts AS
SELECT 
    UserId,
    CAST(SubmittedAt AS DATE) AS InputDate,
    COUNT(*) AS InputCount
FROM DailyInputs
GROUP BY UserId, CAST(SubmittedAt AS DATE);
