using Microsoft.EntityFrameworkCore;
using TrigramBooking.API.Controllers;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Models;
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
                        ServiceErrorCode.NotFound,
                        $"Resource with ID {id} not found."
                    );
            }
            _logger.LogInformation("Resource {ResourceId} retrieved successfully", id);
            return ServiceResult<ResourceDto>.Successful(resourceDto);
        }

        public async Task<ServiceResult<ResourceDto>> CreateResourceAsync(CreateResourceRequest createResourceRequest)
        {
            //check if a resource name exists(optional)
            bool exists = await _context.Resources.AnyAsync(r => r.Name == createResourceRequest.Name);
            if (exists)
            {
                _logger.LogInformation(
                    "Resource creation rejected: name {ResourceName} is already in use",
                    createResourceRequest.Name);
                return ServiceResult<ResourceDto>.Failed(
                    ServiceErrorCode.AlreadyExists,
                    $"Resource with name '{createResourceRequest.Name}' already exists.");
            }

            //Map requestDto to EF core entity
            Resource resource = new Resource()
            {
                Name = createResourceRequest.Name,
                Capacity = createResourceRequest.Capacity,
                Category = createResourceRequest.Category,
                Description = createResourceRequest.Description,
                IsActive = createResourceRequest.IsActive
            };

            //Add and Persist
            _context.Resources.Add(resource);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Resource {ResourceId} created: Name={ResourceName}, Category={Category}, Capacity={Capacity}, IsActive={IsActive}",
                resource.Id, resource.Name, resource.Category, resource.Capacity, resource.IsActive);

            ResourceDto resourceDto = new ResourceDto()
            {
                Id = resource.Id,
                Name = resource.Name,
                Capacity = resource.Capacity,
                Category = resource.Category,
                Description = resource.Description,
                IsActive = resource.IsActive
            };
            return ServiceResult<ResourceDto>.Successful(resourceDto);
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
