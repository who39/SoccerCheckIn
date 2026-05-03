using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class FamilyInvite
{
    public int Id { get; set; }

    public int FamilyId { get; set; }
    public Family Family { get; set; } = null!;

    [Required]
    [StringLength(64)]
    public string Token { get; set; } = string.Empty;

    /// <summary>If set, only a user with this email can accept the invite.</summary>
    [StringLength(256)]
    public string? Email { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }

    public int CreatedByUserSessionId { get; set; }
}
