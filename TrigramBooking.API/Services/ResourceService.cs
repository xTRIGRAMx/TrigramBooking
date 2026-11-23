using Microsoft.EntityFrameworkCore;
using TrigramBooking.API.Controllers;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Services
{
    public class ResourceService : IResourceService
    {
        private readonly TrigramDbContext _context;
        private readonly ILogger<ResourceService> _logger;

        public ResourceService(TrigramDbContext context, ILogger<ResourceService> logger)        
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<List<ResourceDto>>> GetResourcesAsync(int? capacity, string? category, bool? isActive)
        {
            var query = _context.Resources.AsNoTracking();
            if (capacity.HasValue)
            {
                query = query.Where(c => c.Capacity >= capacity);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(c => c.Category == category);
            }

            if (isActive.HasValue)
            {
                query = query.Where(c => c.IsActive == isActive);
            }

            var resources = await query
                .Select(r => new ResourceDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Capacity = r.Capacity,
                    Category = r.Category,
                    Description = r.Description,
                    IsActive = r.IsActive

                }).ToListAsync();

            _logger.LogInformation("Retrieved {ResourceCount} resources", resources.Count);
            return ServiceResult<List<ResourceDto>>.Successful(resources);
        }

        public async Task<ServiceResult<ResourceDto>> GetResourcesByIdAsync(int? id)
        {
            var resourceDto = await _context.Resources
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ResourceDto
            {
                Id = r.Id,
                Name = r.Name,
                Capacity = r.Capacity,
                Category = r.Category,
                Description = r.Description,
                IsActive = r.IsActive
            })
            .FirstOrDefaultAsync();

            if (resourceDto == null)
            {
                _logger.LogInformation("Resource {ResourceId} not found", id);
                return ServiceResult<ResourceDto>.Failed
                    (
                        ServiceErrorCode.ResourceNotFound,
                        $"Resource with ID {id} not found."
                    );
            }
            _logger.LogInformation("Resource {ResourceId} retrieved successfully", id);
            return ServiceResult<ResourceDto>.Successful(resourceDto);
        }

        public Task<ServiceResult<ResourceDto>?> CreateResourceAsync(CreateResourceRequest createResourceRequest)
        {
            throw new NotImplementedException();
        }

        public Task<bool?> DeleteResourceAsync(int? id)
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult<ResourceDto>?> UpdateResourceAsync(CreateResourceRequest updateResourceRequest)
        {
            throw new NotImplementedException();
        }
    }
}
