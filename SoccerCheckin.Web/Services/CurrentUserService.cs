using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Models;
using System.Security.Claims;

namespace SoccerCheckin.Web.Services;

public interface ICurrentUserService
{
    string? Email { get; }
    Task<UserSession?> GetUserAsync();
    Task<bool> IsAdminAsync();

    /// <summary>True if user is global admin OR has any membership in the program.</summary>
    Task<bool> HasProgramAccessAsync(int programId);

    /// <summary>True if user is global admin OR is a Manager of the program.</summary>
    Task<bool> CanManageProgramAsync(int programId);
}

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext dbContext) : ICurrentUserService
{
    private UserSession? _cached;

    public string? Email
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.FindFirst("preferred_username")?.Value;
        }
    }

    public async Task<UserSession?> GetUserAsync()
    {
        if (_cached != null) return _cached;
        var email = Email;
        if (string.IsNullOrEmpty(email)) return null;
        _cached = await dbContext.UserSessions.FirstOrDefaultAsync(u => u.MicrosoftEmail == email);
        return _cached;
    }

    public async Task<bool> IsAdminAsync()
    {
        var user = await GetUserAsync();
        return user?.Role == UserRole.Admin;
    }

    public async Task<bool> HasProgramAccessAsync(int programId)
    {
        if (await IsAdminAsync()) return true;
        var me = await GetUserAsync();
        if (me == null) return false;
        return await dbContext.ProgramUsers
            .AnyAsync(pu => pu.ProgramId == programId && pu.UserSessionId == me.Id);
    }

    public async Task<bool> CanManageProgramAsync(int programId)
    {
        if (await IsAdminAsync()) return true;
        var me = await GetUserAsync();
        if (me == null) return false;
        return await dbContext.ProgramUsers
            .AnyAsync(pu => pu.ProgramId == programId
                            && pu.UserSessionId == me.Id
                            && pu.Role == ProgramRole.Manager);
    }
}
