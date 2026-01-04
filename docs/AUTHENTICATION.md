# Authentication & Authorization Documentation

## Overview
The Personal Universe Simulator uses JWT (JSON Web Tokens) for authentication with support for both traditional email/password login and Google OAuth integration.

**Authentication Methods:**
- Local authentication (email/password with BCrypt)
- Google OAuth 2.0

**Token Type:** JWT Bearer  
**Token Expiry:** 60 minutes  
**Refresh Token Expiry:** 7 days  
**Hashing Algorithm:** BCrypt (work factor: 11)

---

## Architecture

```
┌─────────────────┐
│  Client (Web)   │
└────────┬────────┘
         │
         │ 1. Login Request
         │ (email/password or Google token)
         ▼
┌──────────────────────┐
│  Identity Service    │
│  (Port 5001)         │
│                      │
│  ┌────────────────┐  │
│  │ AuthController │  │
│  └────────┬───────┘  │
│           │          │
│           ▼          │
│  ┌────────────────┐  │
│  │Authentication  │  │
│  │Service         │  │
│  │                │  │
│  │ - Validate     │  │
│  │ - Hash/Verify  │  │
│  │ - Gen Tokens   │  │
│  └────────┬───────┘  │
│           │          │
│           ▼          │
│  ┌────────────────┐  │
│  │ JwtService     │  │
│  └────────┬───────┘  │
└───────────┼──────────┘
            │
            │ 2. JWT Token Returned
            ▼
┌─────────────────┐
│  Client         │
│  Stores Token   │
│  (localStorage/ │
│   cookie)       │
└────────┬────────┘
         │
         │ 3. API Requests
         │ Authorization: Bearer <token>
         ▼
┌──────────────────────┐
│  Any Protected API   │
│  (Personality/       │
│   Simulation/etc)    │
│                      │
│  ┌────────────────┐  │
│  │ JWT Middleware │  │
│  │ - Validate     │  │
│  │ - Decode       │  │
│  │ - Set User     │  │
│  └────────┬───────┘  │
└───────────┼──────────┘
            │
            │ 4. Authorized Request
            ▼
      ┌──────────┐
      │ Service  │
      │ Logic    │
      └──────────┘
```

---

## Local Authentication (Email/Password)

### Registration Flow

**Endpoint:** `POST /api/auth/register`

**Sequence Diagram:**
```
Client                Identity Service         Database
  │                          │                    │
  │── POST /register ────────>│                    │
  │   { email, password,     │                    │
  │     username }           │                    │
  │                          │                    │
  │                          │── Check email ────>│
  │                          │<── Exists? ────────│
  │                          │                    │
  │                          │ (if not exists)    │
  │                          │── Hash password ───┤
  │                          │   (BCrypt)         │
  │                          │                    │
  │                          │── Create user ────>│
  │                          │<── User created ───│
  │                          │                    │
  │                          │── Generate JWT ────┤
  │                          │── Gen Refresh ─────┤
  │                          │                    │
  │<─ 200 OK ────────────────│                    │
  │   { token,               │                    │
  │     refreshToken,        │                    │
  │     user }               │                    │
  │                          │                    │
```

**Implementation:**
```csharp
public async Task<AuthenticationResult> RegisterAsync(
    UserRegistrationDto registrationDto, 
    CancellationToken cancellationToken = default)
{
    // 1. Check if email already exists
    var existingUser = await _userRepository.GetByEmailAsync(
        registrationDto.Email, 
        cancellationToken
    );

    if (existingUser != null)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorMessage = "Email already exists"
        };
    }

    // 2. Hash password with BCrypt
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(
        registrationDto.Password, 
        workFactor: 11
    );

    // 3. Create user entity
    var user = new User
    {
        Id = Guid.NewGuid(),
        Username = registrationDto.Username,
        Email = registrationDto.Email,
        PasswordHash = passwordHash,
        CreatedAt = DateTime.UtcNow,
        AuthProvider = "Local",
        IsActive = true
    };

    // 4. Save to database
    await _userRepository.CreateAsync(user, cancellationToken);

    // 5. Generate tokens
    var token = _jwtService.GenerateToken(user);
    var refreshToken = _jwtService.GenerateRefreshToken();

    return new AuthenticationResult
    {
        Success = true,
        Token = token,
        RefreshToken = refreshToken,
        User = user
    };
}
```

**Password Requirements:**
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character

**Validation (Client-side):**
```javascript
function validatePassword(password) {
  const minLength = 8;
  const hasUpperCase = /[A-Z]/.test(password);
  const hasLowerCase = /[a-z]/.test(password);
  const hasDigit = /\d/.test(password);
  const hasSpecialChar = /[!@#$%^&*(),.?":{}|<>]/.test(password);

  return password.length >= minLength && 
         hasUpperCase && 
         hasLowerCase && 
         hasDigit && 
         hasSpecialChar;
}
```

---

### Login Flow

**Endpoint:** `POST /api/auth/login`

**Sequence Diagram:**
```
Client                Identity Service         Database
  │                          │                    │
  │── POST /login ──────────>│                    │
  │   { email, password }    │                    │
  │                          │                    │
  │                          │── Get user ───────>│
  │                          │<── User data ──────│
  │                          │                    │
  │                          │ (if found)         │
  │                          │── Verify password ─┤
  │                          │   (BCrypt.Verify)  │
  │                          │                    │
  │                          │ (if valid)         │
  │                          │── Update last login>│
  │                          │<── Updated ────────│
  │                          │                    │
  │                          │── Generate JWT ────┤
  │                          │── Gen Refresh ─────┤
  │                          │                    │
  │<─ 200 OK ────────────────│                    │
  │   { token,               │                    │
  │     refreshToken,        │                    │
  │     user }               │                    │
  │                          │                    │
```

**Implementation:**
```csharp
public async Task<AuthenticationResult> LoginAsync(
    UserLoginDto loginDto, 
    CancellationToken cancellationToken = default)
{
    // 1. Find user by email
    var user = await _userRepository.GetByEmailAsync(
        loginDto.Email, 
        cancellationToken
    );

    if (user == null)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorMessage = "Invalid email or password"
        };
    }

    // 2. Verify password
    var isPasswordValid = BCrypt.Net.BCrypt.Verify(
        loginDto.Password, 
        user.PasswordHash
    );

    if (!isPasswordValid)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorMessage = "Invalid email or password"
        };
    }

    // 3. Check if account is active
    if (!user.IsActive)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorMessage = "Account is disabled"
        };
    }

    // 4. Update last login timestamp
    user.LastLoginAt = DateTime.UtcNow;
    await _userRepository.UpdateAsync(user, cancellationToken);

    // 5. Generate tokens
    var token = _jwtService.GenerateToken(user);
    var refreshToken = _jwtService.GenerateRefreshToken();

    return new AuthenticationResult
    {
        Success = true,
        Token = token,
        RefreshToken = refreshToken,
        User = user
    };
}
```

---

## Google OAuth Authentication

### OAuth Flow

**Endpoint:** `POST /api/auth/google`

**Sequence Diagram:**
```
Client            Google OAuth        Identity Service      Database
  │                    │                     │                 │
  │── Sign in with ───>│                     │                 │
  │    Google button   │                     │                 │
  │                    │                     │                 │
  │<── Google ID token─│                     │                 │
  │                    │                     │                 │
  │── POST /auth/google ──────────────────> │                 │
  │   { idToken }                            │                 │
  │                                          │                 │
  │                      ┌─────Validate──────┤                 │
  │                      │   token with      │                 │
  │                      │   Google API      │                 │
  │                      └─────────────────> │                 │
  │                      <── User info ─────┤                 │
  │                                          │                 │
  │                                          │─ Find/Create ─>│
  │                                          │   user by      │
  │                                          │   ExternalId   │
  │                                          │<─ User ────────│
  │                                          │                 │
  │                                          │─ Generate JWT ─┤
  │                                          │                 │
  │<─────────── 200 OK ──────────────────────│                 │
  │   { token, refreshToken, user }         │                 │
  │                                          │                 │
```

**Google Token Validation:**
```csharp
public class GoogleAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthService> _logger;

    public async Task<GoogleUserInfo?> ValidateGoogleTokenAsync(
        string idToken, 
        CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}",
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google token validation failed");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenInfo = JsonSerializer.Deserialize<GoogleTokenInfo>(content);

            // Verify audience (client ID)
            var expectedClientId = _configuration["Google:ClientId"];
            if (tokenInfo?.Audience != expectedClientId)
            {
                _logger.LogWarning("Google token audience mismatch");
                return null;
            }

            // Verify issuer
            if (tokenInfo?.Issuer != "https://accounts.google.com" && 
                tokenInfo?.Issuer != "accounts.google.com")
            {
                _logger.LogWarning("Google token issuer mismatch");
                return null;
            }

            // Verify expiration
            var expiryTimestamp = long.Parse(tokenInfo.ExpiresAt);
            var expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp);
            if (expiryTime < DateTimeOffset.UtcNow)
            {
                _logger.LogWarning("Google token expired");
                return null;
            }

            return new GoogleUserInfo
            {
                Id = tokenInfo.Sub,
                Email = tokenInfo.Email,
                Name = tokenInfo.Name,
                Picture = tokenInfo.Picture,
                EmailVerified = tokenInfo.EmailVerified
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google token");
            return null;
        }
    }
}
```

**Google Authentication Implementation:**
```csharp
public async Task<AuthenticationResult> GoogleAuthAsync(
    GoogleAuthDto googleAuthDto, 
    CancellationToken cancellationToken = default)
{
    // 1. Validate Google ID token
    var googleUserInfo = await _googleAuthService.ValidateGoogleTokenAsync(
        googleAuthDto.IdToken, 
        cancellationToken
    );

    if (googleUserInfo == null)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorMessage = "Invalid Google token"
        };
    }

    // 2. Find existing user by external ID or email
    var user = await _userRepository.GetByExternalIdAsync(
        "Google", 
        googleUserInfo.Id, 
        cancellationToken
    );

    if (user == null)
    {
        user = await _userRepository.GetByEmailAsync(
            googleUserInfo.Email, 
            cancellationToken
        );
    }

    // 3. Create new user if doesn't exist
    if (user == null)
    {
        user = new User
        {
            Id = Guid.NewGuid(),
            Username = googleUserInfo.Name,
            Email = googleUserInfo.Email,
            PasswordHash = "", // Empty for OAuth users
            AuthProvider = "Google",
            ExternalId = googleUserInfo.Id,
            ProfilePictureUrl = googleUserInfo.Picture,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.CreateAsync(user, cancellationToken);
    }
    else
    {
        // Update existing user
        user.LastLoginAt = DateTime.UtcNow;
        user.ProfilePictureUrl = googleUserInfo.Picture;
        await _userRepository.UpdateAsync(user, cancellationToken);
    }

    // 4. Generate tokens
    var token = _jwtService.GenerateToken(user);
    var refreshToken = _jwtService.GenerateRefreshToken();

    return new AuthenticationResult
    {
        Success = true,
        Token = token,
        RefreshToken = refreshToken,
        User = user
    };
}
```

**Client-Side Integration (JavaScript):**
```javascript
<!-- Google Sign-In Button -->
<div id="g_id_onload"
     data-client_id="YOUR_GOOGLE_CLIENT_ID"
     data-callback="handleGoogleSignIn">
</div>
<div class="g_id_signin" data-type="standard"></div>

<script src="https://accounts.google.com/gsi/client" async defer></script>

<script>
async function handleGoogleSignIn(response) {
  const idToken = response.credential;

  try {
    const res = await fetch('http://localhost:5001/api/auth/google', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ idToken })
    });

    const data = await res.json();
    
    if (data.token) {
      // Store JWT token
      localStorage.setItem('jwt_token', data.token);
      localStorage.setItem('refresh_token', data.refreshToken);
      
      // Redirect to app
      window.location.href = '/dashboard';
    } else {
      console.error('Authentication failed:', data.error);
    }
  } catch (error) {
    console.error('Error during Google sign-in:', error);
  }
}
</script>
```

---

## JWT Token Generation

### Token Structure

**JWT Contains:**
- User ID (sub claim)
- Email (email claim)
- Username (name claim)
- Issued at (iat claim)
- Expiration (exp claim)
- Issuer (iss claim)
- Audience (aud claim)

**Implementation:**
```csharp
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;

    public string GenerateToken(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"] 
            ?? throw new InvalidOperationException("JWT secret key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "PersonalUniverse";
        var audience = _configuration["Jwt:Audience"] ?? "PersonalUniverse";
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("auth_provider", user.AuthProvider ?? "Local")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
```

**Example JWT Payload:**
```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john@example.com",
  "name": "john_doe",
  "jti": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "iat": "1704283200",
  "auth_provider": "Local",
  "iss": "PersonalUniverse",
  "aud": "PersonalUniverse",
  "exp": 1704286800
}
```

---

## JWT Validation & Middleware

### ASP.NET Core Configuration

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])
            ),
            ClockSkew = TimeSpan.Zero // No tolerance for expiration
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Add("Token-Expired", "true");
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new { error = "Unauthorized" });
                return context.Response.WriteAsync(result);
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
```

### Protected Endpoints

**Controller Attribute:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires JWT token
public class PersonalityController : ControllerBase
{
    // All actions require authentication

    [HttpPost("input")]
    public async Task<IActionResult> SubmitInput([FromBody] DailyInputDto inputDto)
    {
        // Get authenticated user ID
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        // Process with authenticated user
        var result = await _personalityService.ProcessDailyInputAsync(inputDto);
        return Ok(result);
    }
}
```

**Allow Anonymous (Override):**
```csharp
[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    [HttpGet("state")]
    [AllowAnonymous] // No authentication required
    public async Task<IActionResult> GetUniverseState()
    {
        var state = await _simulationService.GetUniverseStateAsync();
        return Ok(state);
    }

    [HttpPost("tick")]
    [Authorize] // Requires authentication
    public async Task<IActionResult> ProcessTick()
    {
        await _simulationService.ProcessSimulationTickAsync();
        return Ok();
    }
}
```

---

## Token Refresh

### Refresh Token Flow

```
Client                 Identity Service         Database
  │                          │                     │
  │── POST /refresh ────────>│                     │
  │   { refreshToken }       │                     │
  │                          │                     │
  │                          │── Validate token ──>│
  │                          │<── Token valid? ────│
  │                          │                     │
  │                          │ (if valid)          │
  │                          │── Generate new JWT ─┤
  │                          │── Gen new refresh ──┤
  │                          │                     │
  │<─ 200 OK ────────────────│                     │
  │   { token,               │                     │
  │     refreshToken }       │                     │
  │                          │                     │
```

**Implementation (Planned):**
```csharp
[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshDto)
{
    var storedRefreshToken = await _tokenRepository.GetRefreshTokenAsync(
        refreshDto.RefreshToken
    );

    if (storedRefreshToken == null || storedRefreshToken.ExpiresAt < DateTime.UtcNow)
    {
        return Unauthorized(new { error = "Invalid or expired refresh token" });
    }

    var user = await _userRepository.GetByIdAsync(storedRefreshToken.UserId);

    if (user == null || !user.IsActive)
    {
        return Unauthorized(new { error = "User not found or inactive" });
    }

    // Generate new tokens
    var newToken = _jwtService.GenerateToken(user);
    var newRefreshToken = _jwtService.GenerateRefreshToken();

    // Invalidate old refresh token
    await _tokenRepository.RevokeRefreshTokenAsync(refreshDto.RefreshToken);

    // Store new refresh token
    await _tokenRepository.StoreRefreshTokenAsync(new RefreshToken
    {
        Token = newRefreshToken,
        UserId = user.Id,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow
    });

    return Ok(new
    {
        token = newToken,
        refreshToken = newRefreshToken
    });
}
```

---

## Security Best Practices

### Token Storage (Client-Side)

**Recommended: HttpOnly Cookies**
```csharp
// Set cookie with token
Response.Cookies.Append("jwt_token", token, new CookieOptions
{
    HttpOnly = true,  // Not accessible via JavaScript
    Secure = true,    // HTTPS only
    SameSite = SameSiteMode.Strict,
    Expires = DateTimeOffset.UtcNow.AddMinutes(60)
});
```

**Alternative: localStorage (Less secure)**
```javascript
// Store in localStorage (vulnerable to XSS)
localStorage.setItem('jwt_token', token);

// Retrieve for API calls
const token = localStorage.getItem('jwt_token');
fetch(url, {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});
```

### Password Security

**BCrypt Configuration:**
- Work factor: 11 (2048 rounds)
- Automatically salted
- Takes ~100ms to hash (prevents brute force)

**Never:**
- Store plaintext passwords
- Use MD5 or SHA for passwords
- Transmit passwords over HTTP
- Log passwords

### JWT Security

**Do:**
- Use HTTPS in production
- Set short expiration times (60 minutes)
- Validate issuer and audience
- Use strong secret keys (256-bit minimum)
- Rotate secret keys periodically

**Don't:**
- Store sensitive data in JWT payload (it's Base64, not encrypted)
- Use the same secret across environments
- Accept JWTs without validation
- Trust client-supplied tokens

### Rate Limiting

**Authentication Endpoints:**
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

// Apply to endpoints
[EnableRateLimiting("auth")]
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
{
    // ...
}
```

---

## Configuration

### appsettings.json

```json
{
  "Jwt": {
    "SecretKey": "your-very-long-secret-key-at-least-256-bits",
    "Issuer": "PersonalUniverse",
    "Audience": "PersonalUniverse",
    "ExpiryMinutes": 60
  },
  "Google": {
    "ClientId": "your-google-oauth-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-google-oauth-client-secret"
  }
}
```

### Environment Variables (Production)

```bash
export JWT__SECRETKEY="production-secret-key"
export JWT__ISSUER="https://personaluniverse.com"
export JWT__AUDIENCE="https://personaluniverse.com"
export GOOGLE__CLIENTID="production-client-id"
export GOOGLE__CLIENTSECRET="production-client-secret"
```

---

## Testing Authentication

### Postman Collection

**1. Register:**
```http
POST http://localhost:5001/api/auth/register
Content-Type: application/json

{
  "username": "test_user",
  "email": "test@example.com",
  "password": "Test123!@#"
}
```

**2. Login:**
```http
POST http://localhost:5001/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test123!@#"
}
```

**3. Use Token:**
```http
GET http://localhost:5003/api/personality/metrics/{particleId}
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Troubleshooting

### Common Issues

**Issue: "401 Unauthorized" on protected endpoints**
- Check if token is included in Authorization header
- Verify token hasn't expired
- Ensure secret key matches between services

**Issue: "Invalid Google token"**
- Verify Google Client ID in configuration
- Check token hasn't expired (they expire after 1 hour)
- Ensure using correct Google OAuth library

**Issue: "Invalid email or password"**
- Check BCrypt hashing is consistent
- Verify password meets requirements
- Check database contains user

**Issue: Token expires too quickly**
- Adjust `Jwt:ExpiryMinutes` in configuration
- Implement refresh token mechanism
- Consider sliding expiration

---

## Future Enhancements

- [ ] Multi-factor authentication (2FA)
- [ ] Email verification
- [ ] Password reset flow
- [ ] Social login (Facebook, GitHub)
- [ ] API key authentication for service-to-service
- [ ] Refresh token rotation
- [ ] Token revocation list
- [ ] Audit logging for auth events
