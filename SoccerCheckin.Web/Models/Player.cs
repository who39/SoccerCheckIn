using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class Player
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    // Owning user (the user who created/registered this player; kept for audit/legacy auth fallback)
    public int OwnerUserSessionId { get; set; }
    public UserSession OwnerUserSession { get; set; } = null!;

    // Family that owns this player. Any family member can check the player in/out.
    // Nullable so existing players (created before the family feature) keep working.
    public int? FamilyId { get; set; }
    public Family? Family { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
