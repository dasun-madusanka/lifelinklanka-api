using System.Security.Claims;
using LifeLinkLanka.Application.DTOs.Auth;
using LifeLinkLanka.Application.Interfaces;
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
    private readonly IAuditService _auditService;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IAuditService auditService)
    {
        _db = db;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var users = await _db.Users
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new 
            { 
                u.Id, 
                u.FullName, 
                u.Email, 
                u.District, 
                u.AccountStatus, 
                u.IsActive, 
                u.IsMfaEnabled,
                u.VerificationDocumentUrl,
                u.VerificationDocumentName,
                u.VerificationDocumentType,
                u.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var pendingUsers = await _db.Users
            .Include(u => u.DonorProfile)
            .Where(u => u.AccountStatus == VerificationStatus.Pending)
            .OrderByDescending(u => u.CreatedAtUtc)
            .ToListAsync();

        var list = new List<PendingUserApprovalDto>();

        foreach (var u in pendingUsers)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var role = roles.FirstOrDefault() ?? "Donor";

            string? facilityName = null;
            if (role == Roles.HospitalStaff)
            {
                var h = await _db.Hospitals.FirstOrDefaultAsync(h => h.CreatedByUserId == u.Id);
                facilityName = h?.Name;
            }
            else if (role == Roles.BloodBank)
            {
                var b = await _db.BloodBanks.FirstOrDefaultAsync(b => b.District == u.District);
                facilityName = b?.Name;
            }

            list.Add(new PendingUserApprovalDto(
                u.Id,
                u.FullName,
                u.Email ?? "",
                u.PhoneNumber,
                u.NicNumber,
                u.District,
                u.DateOfBirth,
                role,
                u.AccountStatus.ToString(),
                u.CreatedAtUtc,
                u.VerificationDocumentUrl,
                u.VerificationDocumentName,
                u.VerificationDocumentType,
                facilityName,
                u.DonorProfile?.BloodType.ToString(),
                u.DonorProfile?.WeightKg
            ));
        }

        return Ok(list);
    }

    [HttpPost("users/{userId:guid}/approve")]
    public async Task<IActionResult> ApproveUser(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { message = "User not found." });

        user.AccountStatus = VerificationStatus.Verified;
        user.VerifiedAtUtc = DateTime.UtcNow;
        user.RejectionReason = null;
        await _userManager.UpdateAsync(user);

        // If Hospital Staff, verify affiliated hospital
        var hospital = await _db.Hospitals.FirstOrDefaultAsync(h => h.CreatedByUserId == user.Id);
        if (hospital is not null)
        {
            hospital.VerificationStatus = VerificationStatus.Verified;
        }

        // If Blood Bank officer, verify affiliated blood bank
        var bloodBank = await _db.BloodBanks.FirstOrDefaultAsync(b => b.District == user.District && b.VerificationStatus == VerificationStatus.Pending);
        if (bloodBank is not null)
        {
            bloodBank.VerificationStatus = VerificationStatus.Verified;
        }

        await _db.SaveChangesAsync();

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "admin";
        await _auditService.LogAsync(user.Id, "REGISTRATION_APPROVED_BY_ADMIN", $"Approved by Admin {adminId}");

        return Ok(new { message = $"User {user.FullName} ({user.Email}) has been approved and verified. Account is now active." });
    }

    [HttpPost("users/{userId:guid}/reject")]
    public async Task<IActionResult> RejectUser(Guid userId, [FromBody] RejectUserDto? dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(new { message = "User not found." });

        user.AccountStatus = VerificationStatus.Rejected;
        user.RejectionReason = dto?.Reason ?? "Application credentials did not meet verification criteria.";
        await _userManager.UpdateAsync(user);

        var hospital = await _db.Hospitals.FirstOrDefaultAsync(h => h.CreatedByUserId == user.Id);
        if (hospital is not null)
        {
            hospital.VerificationStatus = VerificationStatus.Rejected;
        }

        var bloodBank = await _db.BloodBanks.FirstOrDefaultAsync(b => b.District == user.District && b.VerificationStatus == VerificationStatus.Pending);
        if (bloodBank is not null)
        {
            bloodBank.VerificationStatus = VerificationStatus.Rejected;
        }

        await _db.SaveChangesAsync();

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "admin";
        await _auditService.LogAsync(user.Id, "REGISTRATION_REJECTED_BY_ADMIN", $"Rejected by Admin {adminId}. Reason: {user.RejectionReason}");

        return Ok(new { message = $"User {user.FullName} ({user.Email}) registration has been rejected." });
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
            CriticalRequests = await _db.BloodRequests.CountAsync(r => r.Status == RequestStatus.Open && r.Urgency == UrgencyLevel.Critical),
            PendingApprovals = await _db.Users.CountAsync(u => u.AccountStatus == VerificationStatus.Pending)
        });
    }
}