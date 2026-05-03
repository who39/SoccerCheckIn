using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Models;
using SoccerCheckin.Web.Services;

namespace SoccerCheckin.Web.Controllers;

[Authorize]
public class PlayersController(AppDbContext dbContext, ICurrentUserService currentUser) : Controller
{
    /// <summary>List players the current user can manage in this program (family's players + legacy owned players).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int programId)
    {
        if (!await currentUser.HasProgramAccessAsync(programId)) return Forbid();
        var me = await currentUser.GetUserAsync();
        if (me == null) return Forbid();

        var program = await dbContext.Programs.FindAsync(programId);
        if (program == null) return NotFound();

        var family = await currentUser.GetFamilyAsync();
        var query = dbContext.Players.Where(p => p.ProgramId == programId);
        query = family != null
            ? query.Where(p => p.FamilyId == family.Id || (p.FamilyId == null && p.OwnerUserSessionId == me.Id))
            : query.Where(p => p.OwnerUserSessionId == me.Id);

        var players = await query.OrderBy(p => p.Name).ToListAsync();

        ViewBag.Program = program;
        return View(players);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int programId)
    {
        if (!await currentUser.HasProgramAccessAsync(programId)) return Forbid();
        var program = await dbContext.Programs.FindAsync(programId);
        if (program == null) return NotFound();
        ViewBag.Program = program;
        return View(new Player { ProgramId = programId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Player input)
    {
        if (!await currentUser.HasProgramAccessAsync(input.ProgramId)) return Forbid();
        var me = await currentUser.GetUserAsync();
        if (me == null) return Forbid();
        ModelState.Remove(nameof(Player.Program));
        ModelState.Remove(nameof(Player.OwnerUserSession));
        ModelState.Remove(nameof(Player.Family));
        if (!ModelState.IsValid)
        {
            ViewBag.Program = await dbContext.Programs.FindAsync(input.ProgramId);
            return View(input);
        }

        var family = await currentUser.GetFamilyAsync();
        input.OwnerUserSessionId = me.Id;
        input.FamilyId = family?.Id;
        input.CreatedAtUtc = DateTime.UtcNow;
        dbContext.Players.Add(input);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { programId = input.ProgramId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var player = await dbContext.Players.FindAsync(id);
        if (player == null) return NotFound();
        if (!await CanManagePlayerAsync(player)) return Forbid();
        ViewBag.Program = await dbContext.Programs.FindAsync(player.ProgramId);
        return View(player);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Player input)
    {
        if (id != input.Id) return BadRequest();
        var player = await dbContext.Players.FindAsync(id);
        if (player == null) return NotFound();
        if (!await CanManagePlayerAsync(player)) return Forbid();
        ModelState.Remove(nameof(Player.Program));
        ModelState.Remove(nameof(Player.OwnerUserSession));
        ModelState.Remove(nameof(Player.Family));
        if (!ModelState.IsValid)
        {
            ViewBag.Program = await dbContext.Programs.FindAsync(player.ProgramId);
            return View(input);
        }

        player.Name = input.Name;
        player.Notes = input.Notes;
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { programId = player.ProgramId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var player = await dbContext.Players.FindAsync(id);
        if (player == null) return NotFound();
        if (!await CanManagePlayerAsync(player)) return Forbid();

        var programId = player.ProgramId;
        dbContext.Players.Remove(player);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { programId });
    }

    /// <summary>Edit/delete authorization: program manager, OR family member of the player, OR (legacy) owner.</summary>
    private async Task<bool> CanManagePlayerAsync(Player player)
    {
        if (await currentUser.CanManageProgramAsync(player.ProgramId)) return true;
        return await currentUser.CanCheckInPlayerAsync(player);
    }
}
