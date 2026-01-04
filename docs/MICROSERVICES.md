# Microservices Detailed Documentation

## 1. Identity Service (Port 5001)

### Purpose
Handles all authentication and authorization. Issues JWT tokens for authenticated sessions. Integrates with Google OAuth for third-party login. Triggers particle spawning for new users.

### Responsibilities
- User registration (local accounts)
- User login (local accounts)
- Google OAuth authentication
- JWT token generation and validation
- Automatic particle spawning after registration
- User profile management

### Technology Stack
- **ASP.NET Core 10.0** - Web API framework
- **JWT Bearer Authentication** - Token-based auth
- **BCrypt** - Password hashing
- **Google.Apis.Auth** - OAuth validation
- **HttpClient** - Inter-service communication

### API Endpoints

#### POST /api/auth/register
**Purpose:** Register new user with email and password

**Request:**
```json
{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "SecureP@ss123"
}
```

**Response:**
```json
{
  "userId": "guid",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "john@example.com",
  "username": "john_doe",
  "particleId": "guid"
}
```

**Flow:**
1. Validate email format and password strength
2. Check if email already exists
3. Hash password with BCrypt (work factor 11)
4. Save user to database
5. Call Simulation Engine to spawn particle
6. Generate JWT token
7. Return token and user info

#### POST /api/auth/login
**Purpose:** Authenticate existing user

**Request:**
```json
{
  "email": "john@example.com",
  "password": "SecureP@ss123"
}
```

**Response:**
```json
{
  "userId": "guid",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "john@example.com",
  "username": "john_doe"
}
```

**Flow:**
1. Find user by email
2. Verify password with BCrypt
3. Generate JWT token
4. Return token and user info

#### POST /api/auth/google
**Purpose:** Authenticate via Google OAuth

**Request:**
```json
{
  "idToken": "google.id.token.here"
}
```

**Response:**
```json
{
  "userId": "guid",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "email": "john@gmail.com",
  "username": "john_gmail",
  "profilePictureUrl": "https://...",
  "particleId": "guid"
}
```

**Flow:**
1. Validate ID token with Google
2. Extract user info (email, name, picture)
3. Check if user exists (by Google ID or email)
4. If new user, create account and spawn particle
5. Generate JWT token
6. Return token and user info

#### GET /api/auth/health
**Purpose:** Service health check

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2026-01-03T10:30:00Z"
}
```

### Services

#### IAuthenticationService
```csharp
public interface IAuthenticationService
{
    Task<AuthResult> RegisterAsync(RegisterDto dto);
    Task<AuthResult> LoginAsync(LoginDto dto);
    Task<AuthResult> GoogleAuthAsync(string idToken);
}
```

#### IJwtService
```csharp
public interface IJwtService
{
    string GenerateToken(Guid userId, string email);
    ClaimsPrincipal? ValidateToken(string token);
}
```

#### GoogleAuthService
```csharp
public class GoogleAuthService
{
    public async Task<GoogleAuthResult> ValidateGoogleTokenAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _clientId }
        };
        
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        
        return new GoogleAuthResult
        {
            Email = payload.Email,
            Name = payload.Name,
            PictureUrl = payload.Picture,
            GoogleId = payload.Subject
        };
    }
}
```

### Configuration
```json
{
  "Jwt": {
    "SecretKey": "your-256-bit-secret-key",
    "Issuer": "PersonalUniverseSimulator",
    "Audience": "PersonalUniverseClients",
    "ExpirationHours": 24
  },
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com"
    }
  },
  "SimulationEngine": {
    "BaseUrl": "http://localhost:5004"
  }
}
```

### Security Features
- **Password Hashing:** BCrypt with salt (work factor 11)
- **JWT Signing:** HMAC-SHA256
- **Rate Limiting:** 5 requests/minute per IP
- **Google Token Validation:** Server-side verification
- **HTTPS Only:** In production

---

## 2. Storage Service (Port 5002)

### Purpose
Central data access layer. All database operations go through this service. Uses Dapper for lightweight, fast ORM.

### Responsibilities
- CRUD operations for all entities
- Database connection management
- Repository pattern implementation
- Data validation and integrity
- Optimized queries with indexes

### Technology Stack
- **ASP.NET Core 10.0** - Web API framework
- **Dapper 2.1.52** - Micro-ORM
- **Microsoft.Data.SqlClient** - SQL Server driver
- **SQL Server 2022** - Relational database

### Repositories

#### IUserRepository
```csharp
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByExternalIdAsync(string provider, string externalId, CancellationToken ct = default);
    Task<Guid> AddAsync(User user, CancellationToken ct = default);
    Task<bool> UpdateAsync(User user, CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, CancellationToken ct = default);
}
```

**Implementation Highlights:**
```csharp
public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
{
    using var connection = _connectionFactory.CreateConnection();
    var sql = "SELECT * FROM Users WHERE Email = @Email";
    return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
}

public async Task<Guid> AddAsync(User user, CancellationToken ct)
{
    using var connection = _connectionFactory.CreateConnection();
    var sql = @"
        INSERT INTO Users (Id, Username, Email, PasswordHash, AuthProvider, ExternalId, ProfilePictureUrl, CreatedAt)
        VALUES (@Id, @Username, @Email, @PasswordHash, @AuthProvider, @ExternalId, @ProfilePictureUrl, @CreatedAt);
        SELECT @Id;";
    
    user.Id = Guid.NewGuid();
    user.CreatedAt = DateTime.UtcNow;
    
    return await connection.ExecuteScalarAsync<Guid>(sql, user);
}
```

#### IParticleRepository
```csharp
public interface IParticleRepository
{
    Task<Particle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Particle?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken ct = default);
    Task<IEnumerable<Particle>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<Particle>> GetParticlesInRegionAsync(double minX, double minY, double maxX, double maxY, CancellationToken ct = default);
    Task<Guid> AddAsync(Particle particle, CancellationToken ct = default);
    Task<bool> UpdateAsync(Particle particle, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**Spatial Query Example:**
```csharp
public async Task<IEnumerable<Particle>> GetParticlesInRegionAsync(
    double minX, double minY, double maxX, double maxY, CancellationToken ct)
{
    using var connection = _connectionFactory.CreateConnection();
    var sql = @"
        SELECT * FROM Particles 
        WHERE PositionX BETWEEN @MinX AND @MaxX
          AND PositionY BETWEEN @MinY AND @MaxY
          AND State = 'Active'";
    
    return await connection.QueryAsync<Particle>(sql, new { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY });
}
```

#### IPersonalityMetricsRepository
```csharp
public interface IPersonalityMetricsRepository
{
    Task<PersonalityMetrics?> GetLatestByParticleIdAsync(Guid particleId, CancellationToken ct = default);
    Task<IEnumerable<PersonalityMetrics>> GetHistoryByParticleIdAsync(Guid particleId, CancellationToken ct = default);
    Task<Guid> AddAsync(PersonalityMetrics metrics, CancellationToken ct = default);
    Task<bool> UpdateAsync(PersonalityMetrics metrics, CancellationToken ct = default);
}
```

#### IDailyInputRepository
```csharp
public interface IDailyInputRepository
{
    Task<IEnumerable<DailyInput>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetCountByUserAndDateAsync(Guid userId, DateTime date, CancellationToken ct = default);
    Task<Guid> AddAsync(DailyInput input, CancellationToken ct = default);
}
```

**Rate Limiting Query:**
```csharp
public async Task<int> GetCountByUserAndDateAsync(Guid userId, DateTime date, CancellationToken ct)
{
    using var connection = _connectionFactory.CreateConnection();
    var sql = @"
        SELECT COUNT(*) FROM DailyInputs 
        WHERE UserId = @UserId 
          AND CAST(SubmittedAt AS DATE) = @Date";
    
    return await connection.ExecuteScalarAsync<int>(sql, new { UserId = userId, Date = date.Date });
}
```

#### IUniverseStateRepository
```csharp
public interface IUniverseStateRepository
{
    Task<UniverseState?> GetLatestAsync(CancellationToken ct = default);
    Task<IEnumerable<UniverseState>> GetHistoryAsync(int limit, CancellationToken ct = default);
    Task<Guid> AddAsync(UniverseState state, CancellationToken ct = default);
}
```

#### IParticleEventRepository
```csharp
public interface IParticleEventRepository
{
    Task<IEnumerable<ParticleEvent>> GetByParticleIdAsync(Guid particleId, CancellationToken ct = default);
    Task<IEnumerable<ParticleEvent>> GetRecentEventsAsync(int limit, CancellationToken ct = default);
    Task<Guid> AddAsync(ParticleEvent @event, CancellationToken ct = default);
}
```

### Connection Factory Pattern
```csharp
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    
    public SqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
```

**Usage:**
```csharp
// In Program.cs
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

// In Repository
using var connection = _connectionFactory.CreateConnection();
var result = await connection.QueryAsync<T>(sql, parameters);
```

### No Direct API Endpoints
Storage service is consumed internally by other services. No public REST API exposed. Could be added for admin purposes if needed.

---

## 3. Personality Processing Service (Port 5003)

### Purpose
Processes user daily inputs. Analyzes text sentiment. Calculates personality metrics. Enforces daily input limits.

### Responsibilities
- Accept daily user inputs
- Sentiment analysis on text responses
- Personality trait calculation
- Rate limiting (3 inputs per day)
- Personality history versioning
- Update particle's last input timestamp

### Technology Stack
- **ASP.NET Core 10.0** - Web API framework
- **System.Threading.RateLimiting** - Middleware rate limiting
- **Custom Sentiment Engine** - 60+ keyword detection

### API Endpoints

#### POST /api/personality/input
**Purpose:** Submit daily input and calculate personality metrics

**Request:**
```json
{
  "userId": "guid",
  "inputType": "Mood",
  "question": "How are you feeling today?",
  "response": "I'm feeling curious and energetic!",
  "numericValue": 0.8
}
```

**Response:**
```json
{
  "success": true,
  "inputId": "guid",
  "metrics": {
    "particleId": "guid",
    "curiosity": 0.72,
    "socialAffinity": 0.65,
    "aggression": 0.31,
    "stability": 0.58,
    "growthPotential": 0.69,
    "calculatedAt": "2026-01-03T10:30:00Z",
    "version": 5
  },
  "remainingInputsToday": 2
}
```

**Flow:**
1. Validate user exists
2. Check daily input count (max 3)
3. Save daily input to database
4. Perform sentiment analysis on response text
5. Calculate personality metrics (weighted average with history)
6. Save new personality metrics version
7. Update particle's LastInputAt timestamp
8. Return calculated metrics

### Sentiment Analysis Engine

**Keyword Categories (60+ total):**
```csharp
private readonly Dictionary<string, double> _sentimentKeywords = new()
{
    // Positive emotions
    ["happy"] = 0.8, ["joy"] = 0.9, ["excited"] = 0.85,
    ["love"] = 0.9, ["peaceful"] = 0.7, ["content"] = 0.75,
    
    // Negative emotions
    ["sad"] = -0.7, ["angry"] = -0.8, ["frustrated"] = -0.75,
    ["anxious"] = -0.6, ["depressed"] = -0.9, ["tired"] = -0.5,
    
    // Curiosity indicators
    ["curious"] = 0.8, ["wonder"] = 0.7, ["explore"] = 0.75,
    ["learn"] = 0.8, ["discover"] = 0.85, ["interested"] = 0.7,
    
    // Social indicators
    ["together"] = 0.7, ["friend"] = 0.8, ["connect"] = 0.75,
    ["team"] = 0.7, ["share"] = 0.6, ["community"] = 0.7,
    
    // Aggressive/competitive
    ["compete"] = 0.6, ["fight"] = 0.7, ["win"] = 0.65,
    ["dominate"] = 0.8, ["challenge"] = 0.5, ["conquer"] = 0.75,
    
    // Stability/calmness
    ["stable"] = 0.7, ["calm"] = 0.8, ["consistent"] = 0.7,
    ["reliable"] = 0.75, ["steady"] = 0.7, ["balanced"] = 0.8
};
```

**Analysis Method:**
```csharp
public SentimentResult AnalyzeSentiment(string text)
{
    var words = text.ToLower().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
    
    double curiosityScore = 0;
    double socialScore = 0;
    double aggressionScore = 0;
    double stabilityScore = 0;
    int matchCount = 0;
    
    foreach (var word in words)
    {
        if (_curiosityKeywords.ContainsKey(word))
        {
            curiosityScore += _curiosityKeywords[word];
            matchCount++;
        }
        // ... similar for other traits
    }
    
    // Normalize scores
    if (matchCount > 0)
    {
        curiosityScore /= matchCount;
        socialScore /= matchCount;
        // ... etc
    }
    
    return new SentimentResult
    {
        Curiosity = Math.Clamp(curiosityScore, 0, 1),
        SocialAffinity = Math.Clamp(socialScore, 0, 1),
        Aggression = Math.Clamp(aggressionScore, 0, 1),
        Stability = Math.Clamp(stabilityScore, 0, 1)
    };
}
```

### Personality Calculation Logic
```csharp
public async Task<PersonalityMetrics> CalculateMetricsAsync(Guid userId, DailyInputDto input)
{
    // Get current metrics (70% weight)
    var currentMetrics = await _metricsRepo.GetLatestByParticleIdAsync(particle.Id);
    
    // Analyze new input (30% weight)
    var sentiment = _sentimentEngine.AnalyzeSentiment(input.Response);
    
    // Weighted average
    var newMetrics = new PersonalityMetrics
    {
        ParticleId = particle.Id,
        Curiosity = (currentMetrics.Curiosity * 0.7) + (sentiment.Curiosity * 0.3),
        SocialAffinity = (currentMetrics.SocialAffinity * 0.7) + (sentiment.SocialAffinity * 0.3),
        Aggression = (currentMetrics.Aggression * 0.7) + (sentiment.Aggression * 0.3),
        Stability = (currentMetrics.Stability * 0.7) + (sentiment.Stability * 0.3),
        GrowthPotential = CalculateGrowthPotential(sentiment, inputFrequency),
        CalculatedAt = DateTime.UtcNow,
        Version = currentMetrics.Version + 1
    };
    
    await _metricsRepo.AddAsync(newMetrics);
    return newMetrics;
}
```

### Rate Limiting
**Two Layers:**

1. **Application Layer (Business Logic)**
```csharp
var today = DateTime.UtcNow.Date;
var todayInputCount = await _inputRepo.GetCountByUserAndDateAsync(userId, today);

if (todayInputCount >= 3)
{
    return new ProcessingResult
    {
        Success = false,
        Message = "Daily input limit reached (3 per day)"
    };
}
```

2. **Middleware Layer (ASP.NET)**
```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }
        )
    );
});
```

### Configuration
```json
{
  "PersonalityProcessing": {
    "MaxDailyInputs": 3,
    "SentimentWeight": 0.3,
    "HistoryWeight": 0.7,
    "DefaultTraits": {
      "Curiosity": 0.5,
      "SocialAffinity": 0.5,
      "Aggression": 0.5,
      "Stability": 0.5,
      "GrowthPotential": 0.5
    }
  }
}
```

---

*Continued in next part...*
