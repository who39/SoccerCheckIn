using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class Program
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string? CreatedByEmail { get; set; }

    public ICollection<ProgramUser> ProgramUsers { get; set; } = new List<ProgramUser>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<Player> Players { get; set; } = new List<Player>();
}
