using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TrigramBooking.API.Data;
using TrigramBooking.API.Extensions;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services;
using TrigramBooking.Share.DTOs;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly TrigramDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthController> _logger;
        private readonly IAuthService _authService;

        public AuthController(
            TrigramDbContext context,
            IOptions<JwtSettings> jwtSettings,
            ILogger<AuthController> logger,
            IAuthService authService)
        {
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var registerResult = await _authService.RegisterAsync(request);

            if (!registerResult.Success)
            {
                return registerResult.ToErrorResponse();
            }

            return Created(string.Empty, registerResult.Data);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var loginResult = await _authService.LoginAsync(request);
            if (!loginResult.Success)
            {
                return loginResult.ToErrorResponse();
            }
            return Ok(loginResult.Data);
        }
    }
}