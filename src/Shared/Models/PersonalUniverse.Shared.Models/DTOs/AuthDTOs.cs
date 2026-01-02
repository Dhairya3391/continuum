namespace PersonalUniverse.Shared.Models.DTOs;

public record UserRegistrationDto(
    string Username,
    string Email,
    string Password
);

public record UserLoginDto(
    string Email,
    string Password
);

public record GoogleAuthDto(
    string IdToken
);

public record GoogleUserInfo(
    string Email,
    string Name,
    string GoogleId,
    string? Picture
);

public record UserDto(
    Guid Id,
    string Username,
    string Email,
    DateTime CreatedAt,
    bool IsActive,
    string? AuthProvider = null,
    string? ProfilePictureUrl = null
);

public record AuthenticationResult(
    bool Success,
    string? Token,
    string? RefreshToken,
    UserDto? User,
    string? ErrorMessage
);
