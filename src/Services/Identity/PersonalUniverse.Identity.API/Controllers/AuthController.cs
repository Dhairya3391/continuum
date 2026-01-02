using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.Identity.API.Services;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authenticationService, ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationDto registrationDto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(registrationDto.Username) || 
            string.IsNullOrWhiteSpace(registrationDto.Email) || 
            string.IsNullOrWhiteSpace(registrationDto.Password))
        {
            return BadRequest("Username, email, and password are required");
        }

        var result = await _authenticationService.RegisterAsync(registrationDto, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            token = result.Token,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
        {
            return BadRequest("Email and password are required");
        }

        var result = await _authenticationService.LoginAsync(loginDto, cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            token = result.Token,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    /// <summary>
    /// Authenticate with Google OAuth
    /// </summary>
    /// <param name="googleAuthDto">Google ID token from client</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT tokens and user information</returns>
    [HttpPost("google")]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthDto googleAuthDto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(googleAuthDto.IdToken))
        {
            return BadRequest(new { error = "Google ID token is required" });
        }

        var result = await _authenticationService.GoogleAuthAsync(googleAuthDto, cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.ErrorMessage });
        }

        return Ok(new
        {
            token = result.Token,
            refreshToken = result.RefreshToken,
            user = result.User
        });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Identity Service" });
    }
}
