using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.Storage.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }
        return Ok(user);
    }

    [HttpGet("username/{username}")]
    public async Task<IActionResult> GetByUsername(string username, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }
        return Ok(user);
    }

    [HttpGet("email/{email}")]
    public async Task<IActionResult> GetByEmail(string email, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }
        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User user, CancellationToken cancellationToken)
    {
        var id = await _userRepository.AddAsync(user, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] User user, CancellationToken cancellationToken)
    {
        user.Id = id;
        var success = await _userRepository.UpdateAsync(user, cancellationToken);
        if (!success)
        {
            return NotFound(new { error = "User not found" });
        }
        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var success = await _userRepository.DeleteAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(new { error = "User not found" });
        }
        return NoContent();
    }
}
