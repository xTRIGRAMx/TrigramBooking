using Microsoft.AspNetCore.Mvc;
using TrigramBooking.API.Services.Results;
using TrigramBooking.Share.DTOs;

namespace TrigramBooking.API.Services
{
    public interface IResourceService
    {
        Task<ServiceResult<List<ResourceDto>>> GetResourcesAsync(
            int? capacity,
            string? category,
            bool? isActive);

        Task<ServiceResult<ResourceDto>> GetResourcesByIdAsync(int? id);

        Task<ServiceResult<ResourceDto>> CreateResourceAsync(CreateResourceRequest createResourceRequest);

        Task<ServiceResult<ResourceDto>> UpdateResourceAsync(int id,CreateResourceRequest updateResourceRequest);

        Task<ServiceResult<bool>> DeleteResourceAsync(int id);

        Task<ServiceResult<bool>> DeactivateResourceAsync(int? id);

        Task<ServiceResult<List<AvailableWindowDto>>> GetResourceAvailabilityAsync(int resourceId, DateOnly? startDate, int durationMinutes);
    }
}
