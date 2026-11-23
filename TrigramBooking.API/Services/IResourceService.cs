using Microsoft.AspNetCore.Mvc;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Services
{
    public interface IResourceService
    {
        Task<ServiceResult<List<ResourceDto>>> GetResourcesAsync(
            int? capacity,
            string? category,
            bool? isActive);

        Task<ServiceResult<ResourceDto>> GetResourcesByIdAsync(int? id);

        Task<ServiceResult<ResourceDto>?> CreateResourceAsync(CreateResourceRequest createResourceRequest);

        Task<ServiceResult<ResourceDto>?> UpdateResourceAsync(CreateResourceRequest updateResourceRequest);

        Task<bool?> DeleteResourceAsync(int? id);
    }
}
