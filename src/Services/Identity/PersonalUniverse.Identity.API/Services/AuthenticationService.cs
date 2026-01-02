using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.DTOs;
using PersonalUniverse.Shared.Models.Entities;
using BCrypt.Net;

namespace PersonalUniverse.Identity.API.Services;

public interface IAuthenticationService
{
    Task<AuthenticationResult> RegisterAsync(UserRegistrationDto registrationDto, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> LoginAsync(UserLoginDto loginDto, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> GoogleAuthAsync(GoogleAuthDto googleAuthDto, CancellationToken cancellationToken = default);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly GoogleAuthService _googleAuthService;

    public AuthenticationService(
        IUserRepository userRepository, 
        IJwtService jwtService,
        GoogleAuthService googleAuthService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _googleAuthService = googleAuthService;
    }

    public async Task<AuthenticationResult> RegisterAsync(UserRegistrationDto registrationDto, CancellationToken cancellationToken = default)
    {
        // Check if user already exists
        var existingUserByEmail = await _userRepository.GetByEmailAsync(registrationDto.Email, cancellationToken);
        if (existingUserByEmail != null)
        {
            return new AuthenticationResult(false, null, null, null, "Email already registered");
        }

        var existingUserByUsername = await _userRepository.GetByUsernameAsync(registrationDto.Username, cancellationToken);
        if (existingUserByUsername != null)
        {
            return new AuthenticationResult(false, null, null, null, "Username already taken");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(registrationDto.Password);

        // Create user
        var user = new User
        {
            Username = registrationDto.Username,
            Email = registrationDto.Email,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            AuthProvider = "Local"
        };

        var userId = await _userRepository.AddAsync(user, cancellationToken);
        user.Id = userId;

        // Generate tokens
        var token = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken(user);

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.CreatedAt, user.IsActive, user.AuthProvider, user.ProfilePictureUrl);

        return new AuthenticationResult(true, token, refreshToken, userDto, null);
    }

    public async Task<AuthenticationResult> LoginAsync(UserLoginDto loginDto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(loginDto.Email, cancellationToken);
        
        if (user == null)
        {
            return new AuthenticationResult(false, null, null, null, "Invalid email or password");
        }

        if (!user.IsActive)
        {
            return new AuthenticationResult(false, null, null, null, "Account is inactive");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            return new AuthenticationResult(false, null, null, null, "Invalid email or password");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        // Generate tokens
        var token = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken(user);

        var userDto = new UserDto(user.Id, user.Username, user.Email, user.CreatedAt, user.IsActive, user.AuthProvider, user.ProfilePictureUrl);

        return new AuthenticationResult(true, token, refreshToken, userDto, null);
    }

    public async Task<AuthenticationResult> GoogleAuthAsync(GoogleAuthDto googleAuthDto, CancellationToken cancellationToken = default)
    {
        // Validate Google ID token
        var googleUserInfo = await _googleAuthService.ValidateGoogleTokenAsync(googleAuthDto.IdToken);
        
        if (googleUserInfo == null)
        {
            return new AuthenticationResult(false, null, null, null, "Invalid Google token");
        }

        // Check if user exists by email
        var existingUser = await _userRepository.GetByEmailAsync(googleUserInfo.Email, cancellationToken);

        User user;
        
        if (existingUser != null)
        {
            // Update existing user with Google info if not already set
            if (existingUser.AuthProvider != "Google")
            {
                existingUser.AuthProvider = "Google";
                existingUser.ExternalId = googleUserInfo.GoogleId;
                existingUser.ProfilePictureUrl = googleUserInfo.Picture;
                await _userRepository.UpdateAsync(existingUser, cancellationToken);
            }
            
            existingUser.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(existingUser, cancellationToken);
            
            user = existingUser;
        }
        else
        {
            // Create new user from Google info
            user = new User
            {
                Username = googleUserInfo.Name.Replace(" ", "").ToLower() + "_" + Guid.NewGuid().ToString().Substring(0, 6),
                Email = googleUserInfo.Email,
                PasswordHash = string.Empty, // No password for OAuth users
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                AuthProvider = "Google",
                ExternalId = googleUserInfo.GoogleId,
                ProfilePictureUrl = googleUserInfo.Picture
            };

            var userId = await _userRepository.AddAsync(user, cancellationToken);
            user.Id = userId;
        }

        // Generate tokens
        var token = _jwtService.GenerateToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken(user);

        var userDto = new UserDto(
            user.Id, 
            user.Username, 
            user.Email, 
            user.CreatedAt, 
            user.IsActive,
            user.AuthProvider,
            user.ProfilePictureUrl
        );

        return new AuthenticationResult(true, token, refreshToken, userDto, null);
    }
}
