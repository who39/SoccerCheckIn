using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Models;
using SoccerCheckin.Web.Services;

namespace SoccerCheckin.Web.Controllers;

[Authorize]
public class FamilyController(AppDbContext dbContext, ICurrentUserService currentUser) : Controller
{
    private const int InviteExpiryDays = 14;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var me = await currentUser.GetUserAsync();
        if (me == null) return Forbid();

        var family = await dbContext.Families
            .Include(f => f.Head)
            .Include(f => f.Members).ThenInclude(m => m.UserSession)
            .Include(f => f.Invites)
            .Include(f => f.Players)
            .FirstOrDefaultAsync(f => f.Members.Any(m => m.UserSessionId == me.Id));

        if (family == null) return View("Create", new Family { Name = "" });

        ViewBag.IsHead = family.HeadUserSessionId == me.Id;
        ViewBag.MeId = me.Id;
        return View(family);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        var me = await currentUser.GetUserAsync();
        if (me == null) return Forbid();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Family name is required.";
            return RedirectToAction(nameof(Index));
        }

        if (await dbContext.FamilyMembers.AnyAsync(m => m.UserSessionId == me.Id))
        {
            TempData["Error"] = "You are already in a family.";
            return RedirectToAction(nameof(Index));
        }

        var family = new Family
        {
            Name = name.Trim(),
            HeadUserSessionId = me.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        dbContext.Families.Add(family);
        await dbContext.SaveChangesAsync();

        dbContext.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = family.Id,
            UserSessionId = me.Id,
            JoinedAtUtc = DateTime.UtcNow,
        });

        // Pull the user's existing orphan players into the new family so check-in keeps working.
        await dbContext.Players
            .Where(p => p.OwnerUserSessionId == me.Id && p.FamilyId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FamilyId, (int?)family.Id));

        await dbContext.SaveChangesAsync();
        TempData["Success"] = $"Family \"{family.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(string email)
    {
        var (family, me) = await LoadHeadFamilyAsync();
        if (family == null) return RedirectToAction(nameof(Index));

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Email is required.";
            return RedirectToAction(nameof(Index));
        }
        email = email.Trim();

        var existing = await dbContext.UserSessions
            .FirstOrDefaultAsync(u => u.MicrosoftEmail != null && u.MicrosoftEmail.ToLower() == email.ToLower());

        if (existing != null)
        {
            if (await dbContext.FamilyMembers.AnyAsync(m => m.UserSessionId == existing.Id))
            {
                TempData["Error"] = $"{email} is already in a family.";
                return RedirectToAction(nameof(Index));
            }
            dbContext.FamilyMembers.Add(new FamilyMember
            {
                FamilyId = family.Id,
                UserSessionId = existing.Id,
                JoinedAtUtc = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync();
            TempData["Success"] = $"{email} added to family.";
            return RedirectToAction(nameof(Index));
        }

        // No account yet — create an email-bound invite.
        var invite = new FamilyInvite
        {
            FamilyId = family.Id,
            Token = Guid.NewGuid().ToString("N"),
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(InviteExpiryDays),
            CreatedByUserSessionId = me!.Id,
        };
        dbContext.FamilyInvites.Add(invite);
        await dbContext.SaveChangesAsync();
        var link = Url.Action(nameof(Join), "Family", new { token = invite.Token }, Request.Scheme);
        TempData["Success"] = $"No account found for {email}. Invite created — share this link: {link}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int userSessionId)
    {
        var (family, me) = await LoadHeadFamilyAsync();
        if (family == null) return RedirectToAction(nameof(Index));

        if (userSessionId == family.HeadUserSessionId)
        {
            TempData["Error"] = "The family head cannot be removed. Delete the family instead.";
            return RedirectToAction(nameof(Index));
        }

        var member = await dbContext.FamilyMembers
            .FirstOrDefaultAsync(m => m.FamilyId == family.Id && m.UserSessionId == userSessionId);
        if (member == null) return RedirectToAction(nameof(Index));

        dbContext.FamilyMembers.Remove(member);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Member removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInvite()
    {
        var (family, me) = await LoadHeadFamilyAsync();
        if (family == null) return RedirectToAction(nameof(Index));

        var invite = new FamilyInvite
        {
            FamilyId = family.Id,
            Token = Guid.NewGuid().ToString("N"),
            Email = null,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(InviteExpiryDays),
            CreatedByUserSessionId = me!.Id,
        };
        dbContext.FamilyInvites.Add(invite);
        await dbContext.SaveChangesAsync();
        var link = Url.Action(nameof(Join), "Family", new { token = invite.Token }, Request.Scheme);
        TempData["Success"] = $"Invite link created: {link}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeInvite(int id)
    {
        var (family, _) = await LoadHeadFamilyAsync();
        if (family == null) return RedirectToAction(nameof(Index));

        var invite = await dbContext.FamilyInvites.FirstOrDefaultAsync(i => i.Id == id && i.FamilyId == family.Id);
        if (invite == null) return RedirectToAction(nameof(Index));
        invite.RevokedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Invite revoked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Join(string token)
    {
        var me = await currentUser.GetUserAsync();
        if (me == null) return Forbid();

        var invite = await dbContext.FamilyInvites
            .Include(i => i.Family)
            .FirstOrDefaultAsync(i => i.Token == token);
        if (invite == null) return View("JoinError", "This invite is not valid.");
        if (invite.RevokedAtUtc.HasValue) return View("JoinError", "This invite has been revoked.");
        if (invite.AcceptedAtUtc.HasValue) return View("JoinError", "This invite has already been used.");
        if (invite.ExpiresAtUtc < DateTime.UtcNow) return View("JoinError", "This invite has expired.");

        if (invite.Email != null && !string.Equals(invite.Email, me.MicrosoftEmail, StringComparison.OrdinalIgnoreCase))
            return View("JoinError", $"This invite is for {invite.Email} only.");

        if (await dbContext.FamilyMembers.AnyAsync(m => m.UserSessionId == me.Id))
            return View("JoinError", "You are already in a family. Leave it before joining another.");

        dbContext.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = invite.FamilyId,
            UserSessionId = me.Id,
            JoinedAtUtc = DateTime.UtcNow,
        });
        invite.AcceptedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        TempData["Success"] = $"You've joined the {invite.Family.Name} family.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete()
    {
        var (family, _) = await LoadHeadFamilyAsync();
        if (family == null) return RedirectToAction(nameof(Index));

        // Detach players (set null), then EF cascades members & invites.
        await dbContext.Players
            .Where(p => p.FamilyId == family.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.FamilyId, (int?)null));

        dbContext.Families.Remove(family);
        await dbContext.SaveChangesAsync();
        TempData["Success"] = "Family deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Loads the current user's family and verifies they're the head. Sets TempData on failure.</summary>
    private async Task<(Family? family, UserSession? me)> LoadHeadFamilyAsync()
    {
        var me = await currentUser.GetUserAsync();
        if (me == null) return (null, null);
        var family = await dbContext.Families
            .Include(f => f.Members)
            .Include(f => f.Invites)
            .FirstOrDefaultAsync(f => f.Members.Any(m => m.UserSessionId == me.Id));
        if (family == null)
        {
            TempData["Error"] = "You are not in a family.";
            return (null, me);
        }
        if (family.HeadUserSessionId != me.Id)
        {
            TempData["Error"] = "Only the family head can do that.";
            return (null, me);
        }
        return (family, me);
    }
}
