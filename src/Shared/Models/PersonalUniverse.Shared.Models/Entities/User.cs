namespace PersonalUniverse.Shared.Models.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // OAuth fields
    public string? AuthProvider { get; set; } // "Google", "Local", etc.
    public string? ExternalId { get; set; } // Google User ID
    public string? ProfilePictureUrl { get; set; }
}
