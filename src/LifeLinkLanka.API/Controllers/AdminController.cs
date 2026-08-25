using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var users = await _db.Users
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.Id, u.FullName, u.Email, u.District, u.AccountStatus, u.IsActive, u.IsMfaEnabled })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users/{userId:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();
        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        return Ok(new { user.Id, user.IsActive });
    }

    [HttpPost("users/{userId:guid}/assign-role")]
    public async Task<IActionResult> AssignRole(Guid userId, [FromBody] string role)
    {
        if (!Roles.All.Contains(role)) return BadRequest("Invalid role.");
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, role);

        return Ok(new { message = $"Role updated to {role}" });
    }

    [HttpGet("hospitals/pending")]
    public async Task<IActionResult> GetPendingHospitals() =>
        Ok(await _db.Hospitals.Where(h => h.VerificationStatus == VerificationStatus.Pending).ToListAsync());

    [HttpPost("hospitals/{id:guid}/verify")]
    public async Task<IActionResult> VerifyHospital(Guid id, [FromQuery] bool approve)
    {
        var hospital = await _db.Hospitals.FindAsync(id);
        if (hospital is null) return NotFound();
        hospital.VerificationStatus = approve ? VerificationStatus.Verified : VerificationStatus.Rejected;
        await _db.SaveChangesAsync();
        return Ok(hospital);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await _db.AuditLogs.OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());

    [HttpGet("dashboard-stats")]
    public async Task<IActionResult> GetStats()
    {
        return Ok(new
        {
            TotalDonors = await _db.DonorProfiles.CountAsync(),
            TotalHospitals = await _db.Hospitals.CountAsync(h => h.VerificationStatus == VerificationStatus.Verified),
            OpenRequests = await _db.BloodRequests.CountAsync(r => r.Status == RequestStatus.Open),
            CriticalRequests = await _db.BloodRequests.CountAsync(r => r.Status == RequestStatus.Open && r.Urgency == UrgencyLevel.Critical)
        });
    }
}