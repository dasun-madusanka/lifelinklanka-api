using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LifeLinkLanka.Application.DTOs.Auth;
using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

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

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        IJwtService jwtService, IMfaService mfaService, IConfiguration config)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _mfaService = mfaService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FullName = dto.FullName,
            NicNumber = dto.NicNumber,
            District = dto.District,
            DateOfBirth = dto.DateOfBirth
        };

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        await _emailService.SendEmailConfirmationAsync(user.Email!, user.Id, token);

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);

        // Only allow self-registration as Donor; Hospital/BloodBank roles require Admin approval
        await _userManager.AddToRoleAsync(user, LifeLinkLanka.Domain.Constants.Roles.Donor);

        // TODO: send email confirmation link via EmailService

        return Ok(new { message = "Registration successful. Please verify your email." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResultDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive) return Unauthorized("Invalid credentials.");

        var check = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!check.Succeeded) return Unauthorized("Invalid credentials.");

        if (user.IsMfaEnabled)
        {
            var challengeToken = GenerateMfaChallengeToken(user.Id);
            return Ok(new LoginResultDto(true, challengeToken, null));
        }

        var tokens = await IssueTokensAsync(user);

        await _auditService.LogAsync(user.Id, "USER_LOGIN", ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
        
        return Ok(new LoginResultDto(false, null, tokens));
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
            return Unauthorized("Invalid MFA code.");

        var tokens = await IssueTokensAsync(user);
        return Ok(tokens);
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
        return Ok(new { message = "MFA enabled successfully." });
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

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();

        var result = await _userManager.ConfirmEmailAsync(user, Uri.UnescapeDataString(token));
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "Email confirmed successfully." });
    }
}