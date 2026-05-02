using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class Player
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    // Owning user (the user who created/owns this player; can manage attendance for them)
    public int OwnerUserSessionId { get; set; }
    public UserSession OwnerUserSession { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
