namespace SoccerCheckin.Web.Models;

public class FamilyMember
{
    public int Id { get; set; }

    public int FamilyId { get; set; }
    public Family Family { get; set; } = null!;

    public int UserSessionId { get; set; }
    public UserSession UserSession { get; set; } = null!;

    public DateTime JoinedAtUtc { get; set; }
}
