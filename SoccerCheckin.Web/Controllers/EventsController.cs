using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Models;
using SoccerCheckin.Web.Services;

namespace SoccerCheckin.Web.Controllers;

[Authorize]
public class EventsController(AppDbContext dbContext, ICurrentUserService currentUser) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ev = await dbContext.Events
            .Include(e => e.Program)
            .Include(e => e.Attendances).ThenInclude(a => a.Player).ThenInclude(p => p.OwnerUserSession)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();

        if (!await currentUser.HasProgramAccessAsync(ev.ProgramId)) return Forbid();

        var me = await currentUser.GetUserAsync();
        var family = await currentUser.GetFamilyAsync();
        var myPlayers = me == null
            ? new List<Player>()
            : await (family != null
                ? dbContext.Players.Where(p => p.ProgramId == ev.ProgramId
                    && (p.FamilyId == family.Id || (p.FamilyId == null && p.OwnerUserSessionId == me.Id)))
                : dbContext.Players.Where(p => p.ProgramId == ev.ProgramId && p.OwnerUserSessionId == me.Id))
              .ToListAsync();

        ViewBag.MyPlayers = myPlayers;
        ViewBag.MyAttendance = ev.Attendances
            .Where(a => myPlayers.Any(mp => mp.Id == a.PlayerId))
            .ToDictionary(a => a.PlayerId, a => a.IsAttending);
        ViewBag.CanManage = await currentUser.CanManageProgramAsync(ev.ProgramId);
        return View(ev);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int programId)
    {
        if (!await currentUser.CanManageProgramAsync(programId)) return Forbid();
        var program = await dbContext.Programs.FindAsync(programId);
        if (program == null) return NotFound();
        ViewBag.Program = program;
        return View(new Event { ProgramId = programId, StartUtc = DateTime.Now.AddDays(1) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Event input)
    {
        if (!await currentUser.CanManageProgramAsync(input.ProgramId)) return Forbid();
        ModelState.Remove(nameof(Event.Program));
        if (!ModelState.IsValid)
        {
            ViewBag.Program = await dbContext.Programs.FindAsync(input.ProgramId);
            return View(input);
        }
        // The form posts a local-time value; treat it as local and convert to UTC for storage.
        input.StartUtc = DateTime.SpecifyKind(input.StartUtc, DateTimeKind.Local).ToUniversalTime();
        input.CreatedAtUtc = DateTime.UtcNow;
        dbContext.Events.Add(input);
        await dbContext.SaveChangesAsync();
        return RedirectToAction("Details", "Programs", new { id = input.ProgramId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await dbContext.Events.FindAsync(id);
        if (ev == null) return NotFound();
        if (!await currentUser.CanManageProgramAsync(ev.ProgramId)) return Forbid();
        // Show the value in local time so the datetime-local input renders correctly.
        ev.StartUtc = ev.StartUtc.ToLocalTime();
        return View(ev);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Event input)
    {
        if (id != input.Id) return BadRequest();
        var ev = await dbContext.Events.FindAsync(id);
        if (ev == null) return NotFound();
        if (!await currentUser.CanManageProgramAsync(ev.ProgramId)) return Forbid();
        ModelState.Remove(nameof(Event.Program));
        if (!ModelState.IsValid) return View(input);

        ev.Title = input.Title;
        ev.Description = input.Description;
        ev.Location = input.Location;
        ev.StartUtc = DateTime.SpecifyKind(input.StartUtc, DateTimeKind.Local).ToUniversalTime();
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = ev.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await dbContext.Events.FindAsync(id);
        if (ev == null) return NotFound();
        if (!await currentUser.CanManageProgramAsync(ev.ProgramId)) return Forbid();

        var programId = ev.ProgramId;
        dbContext.Events.Remove(ev);
        await dbContext.SaveChangesAsync();
        return RedirectToAction("Details", "Programs", new { id = programId });
    }

    /// <summary>Toggle/set a single player's attendance for this event. The user must own the player or be in its family.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAttendance(int id, int playerId, bool isAttending)
    {
        var ev = await dbContext.Events.FindAsync(id);
        if (ev == null) return NotFound();

        var player = await dbContext.Players.FindAsync(playerId);
        if (player == null) return NotFound();
        if (player.ProgramId != ev.ProgramId) return BadRequest();

        if (!await currentUser.CanCheckInPlayerAsync(player)) return Forbid();

        var attendance = await dbContext.Attendances
            .FirstOrDefaultAsync(a => a.EventId == id && a.PlayerId == playerId);
        if (attendance == null)
        {
            attendance = new Attendance
            {
                EventId = id,
                PlayerId = playerId,
                IsAttending = isAttending,
                UpdatedAtUtc = DateTime.UtcNow
            };
            dbContext.Attendances.Add(attendance);
        }
        else
        {
            attendance.IsAttending = isAttending;
            attendance.UpdatedAtUtc = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Clear an existing attendance vote so the status returns to "Not set".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAttendance(int id, int playerId)
    {
        var ev = await dbContext.Events.FindAsync(id);
        if (ev == null) return NotFound();

        var player = await dbContext.Players.FindAsync(playerId);
        if (player == null) return NotFound();
        if (player.ProgramId != ev.ProgramId) return BadRequest();

        if (!await currentUser.CanCheckInPlayerAsync(player)) return Forbid();

        var attendance = await dbContext.Attendances
            .FirstOrDefaultAsync(a => a.EventId == id && a.PlayerId == playerId);
        if (attendance != null)
        {
            dbContext.Attendances.Remove(attendance);
            await dbContext.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
