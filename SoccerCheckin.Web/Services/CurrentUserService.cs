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

    /// <summary>The current user's family (with members loaded), or null if they're not in one.</summary>
    Task<Family?> GetFamilyAsync();

    /// <summary>True when the current user can mark attendance for the player
    /// (program manager, family member of the player, or legacy owner).</summary>
    Task<bool> CanCheckInPlayerAsync(Player player);

    /// <summary>True only when the current user is the head of the given family.</summary>
    Task<bool> CanManageFamilyAsync(Family family);
}

public class CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext dbContext) : ICurrentUserService
{
    private UserSession? _cached;
    private Family? _cachedFamily;
    private bool _familyLoaded;

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

    public async Task<Family?> GetFamilyAsync()
    {
        if (_familyLoaded) return _cachedFamily;
        var me = await GetUserAsync();
        if (me == null) { _familyLoaded = true; return null; }
        _cachedFamily = await dbContext.FamilyMembers
            .Where(m => m.UserSessionId == me.Id)
            .Select(m => m.Family)
            .FirstOrDefaultAsync();
        _familyLoaded = true;
        return _cachedFamily;
    }

    public async Task<bool> CanCheckInPlayerAsync(Player player)
    {
        if (await CanManageProgramAsync(player.ProgramId)) return true;
        var me = await GetUserAsync();
        if (me == null) return false;
        // Family ownership wins when set.
        if (player.FamilyId.HasValue)
        {
            return await dbContext.FamilyMembers
                .AnyAsync(m => m.FamilyId == player.FamilyId.Value && m.UserSessionId == me.Id);
        }
        // Legacy player without a family: fall back to direct ownership.
        return player.OwnerUserSessionId == me.Id;
    }

    public async Task<bool> CanManageFamilyAsync(Family family)
    {
        var me = await GetUserAsync();
        return me != null && family.HeadUserSessionId == me.Id;
    }
}
