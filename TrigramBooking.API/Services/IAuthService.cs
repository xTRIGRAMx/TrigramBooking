using TrigramBooking.API.Services.Results;
using TrigramBooking.Share.DTOs;

namespace TrigramBooking.API.Services
{
    public interface IAuthService
    {
        Task<ServiceResult<AuthDto>> RegisterAsync(RegisterRequest request);
        Task<ServiceResult<AuthDto>> LoginAsync(LoginRequest request);

    }
}
