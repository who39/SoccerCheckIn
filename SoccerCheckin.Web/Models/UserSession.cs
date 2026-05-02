using System.Security.Claims;

namespace SoccerCheckin.Web.Models;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public class UserSession
{
    public int Id { get; set; }

    // WeChat fields (deprecated, kept for backward compatibility)
    public string WeChatOpenId { get; set; } = string.Empty;
    public string? WeChatNickname { get; set; }
    public string? WeChatHeadimgUrl { get; set; }

    // Microsoft fields
    public string? MicrosoftId { get; set; }
    public string? MicrosoftEmail { get; set; }
    public string? MicrosoftDisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.User;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastLoginUtc { get; set; }

    public ICollection<ProgramUser> ProgramUsers { get; set; } = new List<ProgramUser>();
}
