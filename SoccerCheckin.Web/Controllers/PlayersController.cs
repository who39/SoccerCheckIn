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
    /// <summary>List the current user's own players inside a program.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int programId)
    {
        if (!await currentUser.HasProgramAccessAsync(programId)) return Forbid();
        var me = await currentUser.GetUserAsync();
        if (me == null) return Forbid();

        var program = await dbContext.Programs.FindAsync(programId);
        if (program == null) return NotFound();

        var players = await dbContext.Players
            .Where(p => p.ProgramId == programId && p.OwnerUserSessionId == me.Id)
            .OrderBy(p => p.Name)
            .ToListAsync();

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
        if (!ModelState.IsValid)
        {
            ViewBag.Program = await dbContext.Programs.FindAsync(input.ProgramId);
            return View(input);
        }

        input.OwnerUserSessionId = me.Id;
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
        var me = await currentUser.GetUserAsync();
        var canManage = await currentUser.CanManageProgramAsync(player.ProgramId);
        if (!canManage && (me == null || player.OwnerUserSessionId != me.Id)) return Forbid();
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
        var me = await currentUser.GetUserAsync();
        var canManage = await currentUser.CanManageProgramAsync(player.ProgramId);
        if (!canManage && (me == null || player.OwnerUserSessionId != me.Id)) return Forbid();
        ModelState.Remove(nameof(Player.Program));
        ModelState.Remove(nameof(Player.OwnerUserSession));
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
        var me = await currentUser.GetUserAsync();
        var canManage = await currentUser.CanManageProgramAsync(player.ProgramId);
        if (!canManage && (me == null || player.OwnerUserSessionId != me.Id)) return Forbid();

        var programId = player.ProgramId;
        dbContext.Players.Remove(player);
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { programId });
    }
}
