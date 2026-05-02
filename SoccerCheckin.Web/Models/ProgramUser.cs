namespace SoccerCheckin.Web.Models;

public enum ProgramRole
{
    Member = 0,
    Manager = 1
}

public class ProgramUser
{
    public int Id { get; set; }

    public int ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    public int UserSessionId { get; set; }
    public UserSession UserSession { get; set; } = null!;

    public ProgramRole Role { get; set; } = ProgramRole.Member;

    public DateTime AssignedAtUtc { get; set; }
}
