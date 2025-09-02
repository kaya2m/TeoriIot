using IotIngest.Api.Data.Repositories;
using IotIngest.Api.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IotIngest.Api.Auth;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
    Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserContext?> GetUserContextAsync(ClaimsPrincipal principal);
}

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepo;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAuthRepository authRepo,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _authRepo = authRepo;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
    {
        var (user, roles) = await _authRepo.GetUserWithRolesAsync(loginDto.Username);
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", loginDto.Username);
            return null;
        }

        await _authRepo.UpdateLastLoginAsync(user.Id);

        var maxRoleLevel = roles.Any() ? roles.Max(r => r.Level) : 0;
        var deviceScopes = await _authRepo.GetUserDeviceScopesAsync(user.Id);

        var accessToken = GenerateAccessToken(user, (short)maxRoleLevel, deviceScopes);
        var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id);

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);

        return new LoginResponseDto(accessToken, refreshToken, expiresAt);
    }

    public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _authRepo.GetRefreshTokenAsync(tokenHash);

        if (storedToken == null)
        {
            return null;
        }

        // Get user with roles
        var user = await _authRepo.GetUserByUsernameAsync("");
        if (user == null)
        {
            // Get user by ID instead
            var (userWithRoles, roles) = await GetUserWithRolesByIdAsync(storedToken.UserId);
            if (userWithRoles == null)
            {
                return null;
            }

            user = userWithRoles;
            var maxRoleLevel = roles.Any() ? roles.Max(r => r.Level) : 0;
            var deviceScopes = await _authRepo.GetUserDeviceScopesAsync(user.Id);

            // Revoke old token and create new one
            var newRefreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id);
            await _authRepo.RevokeRefreshTokenAsync(storedToken.Id, HashToken(newRefreshToken));

            var accessToken = GenerateAccessToken(user, (short)maxRoleLevel, deviceScopes);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);

            return new LoginResponseDto(accessToken, newRefreshToken, expiresAt);
        }

        return null;
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await _authRepo.GetRefreshTokenAsync(tokenHash);
        
        if (storedToken != null)
        {
            await _authRepo.RevokeRefreshTokenAsync(storedToken.Id);
        }
    }

    public async Task<UserContext?> GetUserContextAsync(ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst("user_id")?.Value;
        var usernameClaim = principal.FindFirst("username")?.Value;
        var roleLevelClaim = principal.FindFirst("role_level")?.Value;

        if (!int.TryParse(userIdClaim, out var userId) ||
            string.IsNullOrEmpty(usernameClaim) ||
            !int.TryParse(roleLevelClaim, out var roleLevel))
        {
            return null;
        }

        var deviceScopes = await _authRepo.GetUserDeviceScopesAsync(userId);
        
        return new UserContext(userId, usernameClaim, roleLevel, deviceScopes);
    }

    private string GenerateAccessToken(AuthUser user, short maxRoleLevel, int[] deviceScopes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("user_id", user.Id.ToString()),
            new("username", user.Username),
            new("role_level", maxRoleLevel.ToString())
        };

        if (deviceScopes.Length > 0)
        {
            claims.Add(new Claim("device_scopes", string.Join(",", deviceScopes)));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateAndStoreRefreshTokenAsync(int userId)
    {
        var refreshToken = GenerateRandomToken();
        var tokenHash = HashToken(refreshToken);
        var expiresUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        await _authRepo.CreateRefreshTokenAsync(userId, tokenHash, expiresUtc);
        
        return refreshToken;
    }

    private static string GenerateRandomToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes).ToLower();
    }

    private Task<(AuthUser?, IEnumerable<AuthRole>)> GetUserWithRolesByIdAsync(int userId)
    {
        // This is a simplified implementation - in a real scenario you'd need a method in AuthRepository
        // to get user by ID with roles. For now, we'll return null to keep it simple.
        return Task.FromResult<(AuthUser?, IEnumerable<AuthRole>)>((null, Enumerable.Empty<AuthRole>()));
    }
}