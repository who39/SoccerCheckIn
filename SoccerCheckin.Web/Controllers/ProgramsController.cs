using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Models;
using SoccerCheckin.Web.Services;

namespace SoccerCheckin.Web.Controllers;

[Authorize]
public class ProgramsController(AppDbContext dbContext, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index()
    {
        var isAdmin = await currentUser.IsAdminAsync();
        var me = await currentUser.GetUserAsync();

        // First-time visitor: bootstrap the UserSession from the cookie identity if missing.
        if (me == null)
        {
            var email = currentUser.Email;
            if (!string.IsNullOrEmpty(email))
            {
                me = new UserSession
                {
                    MicrosoftEmail = email,
                    Role = UserRole.User,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastLoginUtc = DateTime.UtcNow
                };
                dbContext.UserSessions.Add(me);
                await dbContext.SaveChangesAsync();
            }
        }

        IQueryable<Models.Program> query = dbContext.Programs.AsNoTracking();

        if (!isAdmin)
        {
            if (me == null)
            {
                // Truly anonymous shouldn't reach here (auth required), but be defensive.
                return View(new List<Models.Program>());
            }
            query = query.Where(p => p.ProgramUsers.Any(pu => pu.UserSessionId == me.Id));
        }

        var programs = await query
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync();

        var managedIds = me == null
            ? new HashSet<int>()
            : (await dbContext.ProgramUsers
                .Where(pu => pu.UserSessionId == me.Id && pu.Role == ProgramRole.Manager)
                .Select(pu => pu.ProgramId)
                .ToListAsync()).ToHashSet();

        ViewBag.IsAdmin = isAdmin;
        ViewBag.ManagedProgramIds = managedIds;
        return View(programs);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await currentUser.HasProgramAccessAsync(id)) return Forbid();

        var program = await dbContext.Programs
            .Include(p => p.ProgramUsers).ThenInclude(pu => pu.UserSession)
            .Include(p => p.Events)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (program == null) return NotFound();

        // Build attendance summary: rows = events (chronological), cols = players
        var players = await dbContext.Players
            .Where(p => p.ProgramId == id)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var eventIds = program.Events.Select(e => e.Id).ToList();
        var attendances = await dbContext.Attendances
            .Where(a => eventIds.Contains(a.EventId) && a.IsAttending)
            .Select(a => new { a.EventId, a.PlayerId })
            .ToListAsync();

        var attendingSet = new HashSet<(int EventId, int PlayerId)>(
            attendances.Select(a => (a.EventId, a.PlayerId)));

        var attendingCounts = attendances
            .GroupBy(a => a.EventId)
            .ToDictionary(g => g.Key, g => g.Count());

        ViewBag.IsAdmin = await currentUser.IsAdminAsync();
        ViewBag.CanManage = await currentUser.CanManageProgramAsync(id);
        ViewBag.SummaryPlayers = players;
        ViewBag.AttendingSet = attendingSet;
        ViewBag.AttendingCounts = attendingCounts;
        return View(program);
    }

    // ---------- Any authenticated user can create a program (becomes its first Manager) ----------

    [HttpGet]
    public IActionResult Create() => View(new Models.Program());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Models.Program program)
    {
        ModelState.Remove(nameof(Models.Program.ProgramUsers));
        ModelState.Remove(nameof(Models.Program.Events));
        ModelState.Remove(nameof(Models.Program.Players));
        if (!ModelState.IsValid) return View(program);

        var me = await currentUser.GetUserAsync();
        if (me == null)
        {
            // Bootstrap a UserSession from the cookie identity if missing.
            var email = currentUser.Email;
            if (string.IsNullOrEmpty(email)) return Forbid();
            me = new UserSession
            {
                MicrosoftEmail = email,
                Role = UserRole.User,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow
            };
            dbContext.UserSessions.Add(me);
            await dbContext.SaveChangesAsync();
        }

        program.CreatedAtUtc = DateTime.UtcNow;
        program.UpdatedAtUtc = DateTime.UtcNow;
        program.CreatedByEmail = currentUser.Email;
        dbContext.Programs.Add(program);
        await dbContext.SaveChangesAsync();

        // Creator becomes the first Manager of the program.
        dbContext.ProgramUsers.Add(new ProgramUser
        {
            ProgramId = program.Id,
            UserSessionId = me.Id,
            Role = ProgramRole.Manager,
            AssignedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = program.Id });
    }

    // Delete remains restricted to global admin or a Manager of the program.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        var program = await dbContext.Programs.FindAsync(id);
        if (program == null) return NotFound();
        dbContext.Programs.Remove(program);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ---------- Admin OR Manager: edit + manage members ----------

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        var program = await dbContext.Programs.FindAsync(id);
        if (program == null) return NotFound();
        return View(program);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Models.Program input)
    {
        if (id != input.Id) return BadRequest();
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        if (!ModelState.IsValid) return View(input);

        var program = await dbContext.Programs.FindAsync(id);
        if (program == null) return NotFound();

        program.Name = input.Name;
        program.Description = input.Description;
        program.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Members(int id)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        var program = await dbContext.Programs
            .Include(p => p.ProgramUsers).ThenInclude(pu => pu.UserSession)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (program == null) return NotFound();
        return View(program);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(int id, string email, ProgramRole role = ProgramRole.Member)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();

        var program = await dbContext.Programs.FindAsync(id);
        if (program == null) return NotFound();

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Email is required.";
            return RedirectToAction(nameof(Members), new { id });
        }

        email = email.Trim();

        var user = await dbContext.UserSessions.FirstOrDefaultAsync(u => u.MicrosoftEmail == email);
        if (user == null)
        {
            user = new UserSession
            {
                MicrosoftEmail = email,
                Role = UserRole.User,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow
            };
            dbContext.UserSessions.Add(user);
            await dbContext.SaveChangesAsync();
        }

        var existing = await dbContext.ProgramUsers
            .FirstOrDefaultAsync(pu => pu.ProgramId == id && pu.UserSessionId == user.Id);

        if (existing == null)
        {
            dbContext.ProgramUsers.Add(new ProgramUser
            {
                ProgramId = id,
                UserSessionId = user.Id,
                Role = role,
                AssignedAtUtc = DateTime.UtcNow
            });
        }
        else if (existing.Role != role)
        {
            existing.Role = role;
        }

        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Members), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMemberRole(int id, int userSessionId, ProgramRole role)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();

        var pu = await dbContext.ProgramUsers
            .FirstOrDefaultAsync(x => x.ProgramId == id && x.UserSessionId == userSessionId);
        if (pu != null)
        {
            pu.Role = role;
            await dbContext.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Members), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int id, int userSessionId)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();

        var pu = await dbContext.ProgramUsers
            .FirstOrDefaultAsync(x => x.ProgramId == id && x.UserSessionId == userSessionId);
        if (pu != null)
        {
            dbContext.ProgramUsers.Remove(pu);
            await dbContext.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Members), new { id });
    }

    // ---------- Invites ----------

    [HttpGet]
    public async Task<IActionResult> Invites(int id)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        var program = await dbContext.Programs.FindAsync(id);
        if (program == null) return NotFound();

        var invites = await dbContext.ProgramInvites
            .Where(i => i.ProgramId == id)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();

        ViewBag.Program = program;
        return View(invites);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvite(int id, ProgramRole role = ProgramRole.Member, int? expireDays = 14)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        var program = await dbContext.Programs.FindAsync(id);
        if (program == null) return NotFound();

        // 32-byte URL-safe token
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(24);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        dbContext.ProgramInvites.Add(new ProgramInvite
        {
            ProgramId = id,
            Token = token,
            RoleToGrant = role,
            CreatedByEmail = currentUser.Email,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expireDays.HasValue ? DateTime.UtcNow.AddDays(expireDays.Value) : null
        });
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Invites), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvite(int id, int inviteId)
    {
        if (!await currentUser.CanManageProgramAsync(id)) return Forbid();
        var invite = await dbContext.ProgramInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.ProgramId == id);
        if (invite != null && invite.RevokedAtUtc == null)
        {
            invite.RevokedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Invites), new { id });
    }

    /// <summary>Public-ish: any authenticated user can accept an invite via its token.</summary>
    [HttpGet("/Programs/Join/{token}")]
    public async Task<IActionResult> Join(string token)
    {
        var invite = await dbContext.ProgramInvites
            .Include(i => i.Program)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invite == null) return View("JoinError", "This invite link is invalid.");
        if (invite.RevokedAtUtc != null) return View("JoinError", "This invite has been revoked.");
        if (invite.ExpiresAtUtc.HasValue && invite.ExpiresAtUtc.Value < DateTime.UtcNow)
            return View("JoinError", "This invite has expired.");

        var me = await currentUser.GetUserAsync();
        var email = currentUser.Email;
        if (me == null && !string.IsNullOrEmpty(email))
        {
            // Auto-create the user session record for the signed-in identity that hasn't logged in via the cookie path before.
            me = new UserSession
            {
                MicrosoftEmail = email,
                Role = UserRole.User,
                CreatedAtUtc = DateTime.UtcNow,
                LastLoginUtc = DateTime.UtcNow
            };
            dbContext.UserSessions.Add(me);
            await dbContext.SaveChangesAsync();
        }
        if (me == null) return Forbid();

        var existing = await dbContext.ProgramUsers
            .FirstOrDefaultAsync(pu => pu.ProgramId == invite.ProgramId && pu.UserSessionId == me.Id);

        if (existing == null)
        {
            dbContext.ProgramUsers.Add(new ProgramUser
            {
                ProgramId = invite.ProgramId,
                UserSessionId = me.Id,
                Role = invite.RoleToGrant,
                AssignedAtUtc = DateTime.UtcNow
            });
        }
        else if (existing.Role == ProgramRole.Member && invite.RoleToGrant == ProgramRole.Manager)
        {
            // Upgrade only; never downgrade via invite.
            existing.Role = ProgramRole.Manager;
        }
        await dbContext.SaveChangesAsync();

        TempData["JoinSuccess"] = $"You've joined {invite.Program.Name}.";
        return RedirectToAction(nameof(Details), new { id = invite.ProgramId });
    }
}
