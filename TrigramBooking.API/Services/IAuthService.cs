using TrigramBooking.API.DTOs;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Services
{
    public interface IAuthService
    {
        Task<ServiceResult<ResourceDto>> GetResourcesByIdAsync(int? id);
        Task<ServiceResult<ResourceDto>> GetResourcesByIdAsync(int? id);

    }
}
