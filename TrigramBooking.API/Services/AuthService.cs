using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TrigramBooking.API.Data;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services.Results;
using TrigramBooking.Share.DTOs;

namespace TrigramBooking.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly TrigramDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly JwtSettings _jwtSettings;


        public AuthService(TrigramDbContext context, ILogger<AuthService> logger, IOptions<JwtSettings> jwtSettings)
        {
            _context = context;
            _logger = logger;
            _jwtSettings = jwtSettings.Value;
        }
        public async Task<ServiceResult<AuthDto>> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                _logger.LogWarning(
                    "Login failed: no account found for email");
                return ServiceResult<AuthDto>.Failed(ServiceErrorCode.Unauthorized, "Invalid email or password.");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning(
                    "Login failed: incorrect password for user {UserId}",
                    user.Id);
                return ServiceResult<AuthDto>.Failed(ServiceErrorCode.Unauthorized, "Invalid email or password.");
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            _logger.LogInformation(
                "User {UserId} logged in; token issued, expires at {ExpiresAtUtc:o}",
                user.Id, expires);

            return ServiceResult<AuthDto>.Successful(
                new AuthDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Token = tokenString,
                    TokenExpiration = expires
                });
        }

        public async Task<ServiceResult<AuthDto>> RegisterAsync(RegisterRequest request)
        {
            var existingEmail = await _context.Users
    .AsNoTracking()
    .Where(u => u.Email == request.Email)
    .Select(u => (int?)u.Id)
    .FirstOrDefaultAsync();

            if (existingEmail != null)
            {
                _logger.LogInformation(
                    "Registration rejected: email is already taken by: {UserId}", existingEmail);
                return ServiceResult<AuthDto>.Failed(ServiceErrorCode.AlreadyExists,
                    "Try a different username or email");
            }

            var existingUsername = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserName == request.Username)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();

            if (existingUsername != null)
            {
                _logger.LogInformation(
                    "Registration rejected: username is already taken by: User {UserId}",
                    existingUsername);
                return ServiceResult<AuthDto>.Failed(ServiceErrorCode.AlreadyExists,
                    "Try a different username or password");
            }

            var user = new User
            {
                UserName = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "User {UserId} registered",
                user.Id);

            return ServiceResult<AuthDto>.Successful(
                new AuthDto {
                    Id = user.Id,
                    UserName = user.UserName, 
                    Email = user.Email, 
                    Token = string.Empty });
        }
    }
}
