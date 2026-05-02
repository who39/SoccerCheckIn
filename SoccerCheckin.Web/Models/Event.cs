using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class Event
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(300)]
    public string? Location { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
