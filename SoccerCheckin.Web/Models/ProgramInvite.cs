using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class ProgramInvite
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    [Required]
    [StringLength(64)]
    public string Token { get; set; } = string.Empty;

    public ProgramRole RoleToGrant { get; set; } = ProgramRole.Member;

    public string? CreatedByEmail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
