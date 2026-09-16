using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLinkLanka.Application.DTOs.Auth;
using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtService _jwtService;
    private readonly IMfaService _mfaService;
    private readonly IConfiguration _config;
    private readonly IAuditService _auditService;      
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _db;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IJwtService jwtService, IMfaService mfaService, IConfiguration config,
        IAuditService auditService, IEmailService emailService, ApplicationDbContext db)   
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _mfaService = mfaService;
        _config = config;
        _auditService = auditService;
        _emailService = emailService;
        _db = db;
    }

    [HttpPost("upload-verification-doc")]
    [AllowAnonymous]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<IActionResult> UploadVerificationDoc([FromForm] IFormFile file, [FromForm] string? documentType)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided or file is empty." });

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new { message = "Unsupported file format. Please upload a valid PDF, JPG, or PNG document." });

        var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads");
        if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

        var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var localFilePath = Path.Combine(uploadsDir, uniqueFileName);

        await using (var stream = new FileStream(localFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var documentUrl = $"/uploads/{uniqueFileName}";

        return Ok(new
        {
            documentUrl,
            fileName = file.FileName,
            sizeBytes = file.Length,
            documentType = documentType ?? "VerificationDocument"
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var targetRole = dto.Role switch
        {
            "HospitalStaff" or "hospitalstaff" => Roles.HospitalStaff,
            "BloodBank" or "bloodbank" => Roles.BloodBank,
            "Donor" or "donor" => Roles.Donor,
            _ => Roles.Donor
        };

        if (dto.Role != null && dto.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Self-registration as System Administrator is not permitted.");
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            NicNumber = dto.NicNumber,
            District = dto.District,
            DateOfBirth = dto.DateOfBirth,
            PhoneNumber = dto.PhoneNumber,
            EmailConfirmed = true,
            IsActive = true,
            AccountStatus = VerificationStatus.Pending,
            VerificationDocumentUrl = dto.VerificationDocumentUrl,
            VerificationDocumentName = dto.VerificationDocumentName,
            VerificationDocumentType = dto.VerificationDocumentType ?? (targetRole == Roles.Donor ? "MedicalCertificate" : (targetRole == Roles.HospitalStaff ? "HospitalLicense" : "BloodBankLicense"))
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, targetRole);

        if (targetRole == Roles.Donor)
        {
            var bloodType = Enum.TryParse<BloodType>(dto.BloodType, true, out var bt) ? bt : BloodType.OPositive;
            var profile = new DonorProfile
            {
                UserId = user.Id,
                BloodType = bloodType,
                WeightKg = dto.WeightKg ?? 60,
                IsEligibleToDonate = true,
                ConsentToBeContacted = true,
                DonorCardNumber = $"LLL-DONOR-{Random.Shared.Next(100000, 999999)}"
            };
            _db.DonorProfiles.Add(profile);
        }
        else if (targetRole == Roles.HospitalStaff && !string.IsNullOrWhiteSpace(dto.HospitalName))
        {
            var hospital = new Hospital
            {
                Name = dto.HospitalName,
                RegistrationNumber = !string.IsNullOrWhiteSpace(dto.HospitalRegistrationNumber) 
                    ? dto.HospitalRegistrationNumber 
                    : $"HOSP-{dto.District.ToUpperInvariant()[..Math.Min(3, dto.District.Length)]}-{Random.Shared.Next(100, 999)}",
                District = !string.IsNullOrWhiteSpace(dto.HospitalDistrict) ? dto.HospitalDistrict : dto.District,
                Address = !string.IsNullOrWhiteSpace(dto.HospitalAddress) ? dto.HospitalAddress : $"{dto.HospitalName}, {dto.District}",
                ContactPhone = dto.HospitalContactPhone ?? dto.PhoneNumber ?? "0112691111",
                CreatedByUserId = user.Id,
                VerificationStatus = VerificationStatus.Pending
            };
            _db.Hospitals.Add(hospital);
        }
        else if (targetRole == Roles.BloodBank && !string.IsNullOrWhiteSpace(dto.BloodBankName))
        {
            var bank = new BloodBank
            {
                Name = dto.BloodBankName,
                District = !string.IsNullOrWhiteSpace(dto.BloodBankDistrict) ? dto.BloodBankDistrict : dto.District,
                ContactPhone = dto.BloodBankContactPhone ?? dto.PhoneNumber ?? "0112691111",
                VerificationStatus = VerificationStatus.Pending
            };
            _db.BloodBanks.Add(bank);
        }

        if (!string.IsNullOrWhiteSpace(dto.VerificationDocumentUrl))
        {
            var doc = new UploadedDocument
            {
                OwnerUserId = user.Id,
                DocumentType = user.VerificationDocumentType ?? "VerificationDocument",
                SupabaseBucket = "verification-documents",
                StoragePath = dto.VerificationDocumentUrl,
                PublicUrl = dto.VerificationDocumentUrl,
                FileName = dto.VerificationDocumentName ?? "verification_doc",
                SizeBytes = 0
            };
            _db.UploadedDocuments.Add(doc);
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(user.Id, "USER_REGISTERED_PENDING_APPROVAL", $"Role: {targetRole}, District: {dto.District}, Doc: {user.VerificationDocumentName}");

        return Ok(new { message = $"Registration submitted successfully as {targetRole}! Your account credentials and uploaded verification documents are now pending administrative review. You will be able to log in once approved by the System Administrator." });
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();

        var result = await _userManager.ConfirmEmailAsync(user, Uri.UnescapeDataString(token));
        if (!result.Succeeded) return BadRequest(result.Errors);

        await _auditService.LogAsync(userId, "EMAIL_CONFIRMED");
        return Ok(new { message = "Email confirmed successfully." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResultDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive) return Unauthorized("Invalid credentials.");

        var check = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            await _auditService.LogAsync(user.Id, "LOGIN_FAILED", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            return Unauthorized("Invalid credentials.");
        }

        // Check Administrative Verification Gating
        if (user.AccountStatus == VerificationStatus.Pending)
        {
            await _auditService.LogAsync(user.Id, "LOGIN_BLOCKED_PENDING_APPROVAL", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            return StatusCode(403, new
            {
                message = "Account Pending Administrator Verification: Your account details and uploaded verification documents are currently under review by the Administrator. Access will be unlocked once approved."
            });
        }

        if (user.AccountStatus == VerificationStatus.Rejected)
        {
            await _auditService.LogAsync(user.Id, "LOGIN_BLOCKED_REJECTED", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            var reason = string.IsNullOrWhiteSpace(user.RejectionReason) ? "" : $" Reason: {user.RejectionReason}";
            return StatusCode(403, new
            {
                message = $"Account Registration Rejected: Your account application was not approved by the administrator.{reason}"
            });
        }

        if (user.IsMfaEnabled)
        {
            var challengeToken = GenerateMfaChallengeToken(user.Id);
            return Ok(new LoginResultDto(true, challengeToken, null));
        }

        var tokens = await IssueTokensAsync(user);
        await _auditService.LogAsync(user.Id, "USER_LOGIN", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new LoginResultDto(false, null, tokens));
    }


    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var subClaim = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
            return Unauthorized();

        var user = await _userManager.Users
            .Include(u => u.DonorProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return NotFound("User not found.");

        var roles = await _userManager.GetRolesAsync(user);

        object? facility = null;
        if (roles.Contains(Roles.HospitalStaff) || roles.Contains(Roles.Admin))
        {
            var hospital = await _db.Hospitals.FirstOrDefaultAsync(h => h.CreatedByUserId == user.Id && h.District == user.District && h.Name.Contains("NHSL"))
                        ?? await _db.Hospitals.FirstOrDefaultAsync(h => h.CreatedByUserId == user.Id && h.District == user.District)
                        ?? await _db.Hospitals.FirstOrDefaultAsync(h => h.CreatedByUserId == user.Id)
                        ?? await _db.Hospitals.FirstOrDefaultAsync(h => h.District == user.District && h.VerificationStatus == VerificationStatus.Verified)
                        ?? await _db.Hospitals.FirstOrDefaultAsync(h => h.VerificationStatus == VerificationStatus.Verified);
            if (hospital != null)
            {
                facility = new
                {
                    Type = "Hospital",
                    hospital.Id,
                    hospital.Name,
                    hospital.District,
                    hospital.Address,
                    hospital.ContactPhone,
                    hospital.RegistrationNumber
                };
            }
        }
        else if (roles.Contains(Roles.BloodBank))
        {
            var bank = await _db.BloodBanks.FirstOrDefaultAsync(b => b.District == user.District && b.VerificationStatus == VerificationStatus.Verified)
                    ?? await _db.BloodBanks.FirstOrDefaultAsync(b => b.VerificationStatus == VerificationStatus.Verified);
            if (bank != null)
            {
                facility = new
                {
                    Type = "BloodBank",
                    bank.Id,
                    bank.Name,
                    bank.District,
                    bank.ContactPhone
                };
            }
        }

        object? donorProfile = null;
        if (user.DonorProfile != null)
        {
            donorProfile = new
            {
                user.DonorProfile.BloodType,
                user.DonorProfile.WeightKg,
                user.DonorProfile.IsEligibleToDonate,
                user.DonorProfile.LastDonationDateUtc,
                user.DonorProfile.DonationsCompletedCount,
                TotalVolumeDonatedMl = user.DonorProfile.TotalVolumeMl,
                user.DonorProfile.DonorCardNumber,
                user.DonorProfile.ConsentToBeContacted
            };
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.District,
            user.NicNumber,
            user.DateOfBirth,
            user.PhoneNumber,
            user.IsMfaEnabled,
            AccountStatus = user.AccountStatus.ToString(),
            Roles = roles,
            AffiliatedFacility = facility,
            DonorProfile = donorProfile
        });
    }


    [HttpPost("mfa/verify")]
    public async Task<ActionResult<TokenResponseDto>> VerifyMfa(MfaVerifyDto dto)
    {
        var principal = ValidateMfaChallengeToken(dto.MfaChallengeToken);
        if (principal is null) return Unauthorized("Challenge expired or invalid.");

        var userId = Guid.Parse(principal.FindFirstValue("sub")!);
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrEmpty(user.MfaSecretKey)) return Unauthorized();

        if (!_mfaService.ValidateCode(user.MfaSecretKey, dto.Code))
        {
            await _auditService.LogAsync(userId, "MFA_VERIFY_FAILED");
            return Unauthorized("Invalid MFA code.");
        }

        var tokens = await IssueTokensAsync(user);
        await _auditService.LogAsync(userId, "USER_LOGIN_MFA");
        return Ok(tokens);
    }

    [HttpPost("mfa/enable")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> EnableMfa([FromBody] string code)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null || string.IsNullOrEmpty(user.MfaSecretKey)) return BadRequest();

        if (!_mfaService.ValidateCode(user.MfaSecretKey, code)) return BadRequest("Invalid code.");

        user.IsMfaEnabled = true;
        user.MfaEnabledAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(user.Id, "MFA_ENABLED");
        return Ok(new { message = "MFA enabled successfully." });
    }

    [HttpPost("mfa/setup")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<MfaSetupResponseDto>> SetupMfa()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var secret = _mfaService.GenerateSecretKey();
        user.MfaSecretKey = secret; // encrypt before persisting in production (e.g. Data Protection API)
        await _userManager.UpdateAsync(user);

        var qrUri = _mfaService.GenerateQrCodeUri(user.Email!, secret);
        var qrPng = _mfaService.GenerateQrCodePng(qrUri);

        return Ok(new MfaSetupResponseDto(secret, Convert.ToBase64String(qrPng)));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponseDto>> Refresh(RefreshTokenDto dto)
    {
        var principal = GetPrincipalFromExpiredToken(dto.AccessToken);
        var userId = Guid.Parse(principal.FindFirstValue("sub")!);
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null || user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiryUtc < DateTime.UtcNow)
            return Unauthorized("Invalid refresh token.");

        var tokens = await IssueTokensAsync(user);
        return Ok(tokens);
    }

    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Logout()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is not null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryUtc = null;
            await _userManager.UpdateAsync(user);
        }
        return Ok(new { message = "Logged out." });
    }

    // ---- helpers ----
    private async Task<TokenResponseDto> IssueTokensAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (access, expires) = _jwtService.GenerateAccessToken(user, roles);
        var refresh = _jwtService.GenerateRefreshToken();

        user.RefreshToken = refresh;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return new TokenResponseDto(access, expires, refresh);
    }

    private string GenerateMfaChallengeToken(Guid userId)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_config["Jwt:Secret"]!));
        var token = new JwtSecurityToken(
            claims: new[] { new Claim("sub", userId.ToString()), new Claim("amr", "mfa_pending") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }

    private ClaimsPrincipal? ValidateMfaChallengeToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Convert.FromBase64String(_config["Jwt:Secret"]!));
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                IssuerSigningKey = key
            }, out var validated);

            if (principal.FindFirstValue("amr") != "mfa_pending") return null;
            return principal;
        }
        catch { return null; }
    }

    private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_config["Jwt:Secret"]!));
        var validationParams = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            IssuerSigningKey = key,
            ValidateLifetime = false
        };
        return new JwtSecurityTokenHandler().ValidateToken(token, validationParams, out _);
    }
}