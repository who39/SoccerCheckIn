namespace SoccerCheckin.Web.Models;

public class Attendance
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>
    /// True if the player is checked in (attending). False explicitly means "not attending".
    /// Absence of a row means undecided.
    /// </summary>
    public bool IsAttending { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
