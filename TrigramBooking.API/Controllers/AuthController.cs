using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly TrigramDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            TrigramDbContext context,
            IOptions<JwtSettings> jwtSettings,
            ILogger<AuthController> logger)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
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
                return Conflict(new { Message = "That email is already registered." });
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
                return Conflict(new { Message = "That username is already taken." });
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

            return Created(string.Empty, new { user.Id, user.UserName, user.Email });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                _logger.LogWarning(
                    "Login failed: no account found for email");
                return Unauthorized(new { Message = "Invalid email or password." });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning(
                    "Login failed: incorrect password for user {UserId}",
                    user.Id);
                return Unauthorized(new { Message = "Invalid email or password." });
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

            return Ok(new { Token = tokenString, ExpiresAtUtc = expires });
        }
    }
}