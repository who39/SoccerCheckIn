using System.ComponentModel.DataAnnotations;

namespace SoccerCheckin.Web.Models;

public class Family
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public int HeadUserSessionId { get; set; }
    public UserSession Head { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
    public ICollection<FamilyInvite> Invites { get; set; } = new List<FamilyInvite>();
    public ICollection<Player> Players { get; set; } = new List<Player>();
}
