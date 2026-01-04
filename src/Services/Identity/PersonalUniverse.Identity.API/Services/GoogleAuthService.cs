using Google.Apis.Auth;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.Identity.API.Services;

public class GoogleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        IConfiguration configuration,
        ILogger<GoogleAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string idToken)
    {
        try
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            
            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogError("Google ClientId not configured");
                return null;
            }

            var validationSettings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validationSettings);

            if (payload == null)
            {
                _logger.LogWarning("Google token validation returned null payload");
                return null;
            }

            return new GoogleUserInfo(
                Email: payload.Email,
                Name: payload.Name,
                GoogleId: payload.Subject,
                Picture: payload.Picture
            );
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogError(ex, "Invalid Google JWT token");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google token");
            return null;
        }
    }
}
