using Microsoft.EntityFrameworkCore;
using System.Data;
using TrigramBooking.API.Controllers;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services.Results;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        public async Task<ServiceResult<bool>> DeleteResourceAsync(int id)
        {
            //find resource
            var resource = await _context.Resources.FindAsync(id);

            //check if exists
            if (resource == null)
            {
                _logger.LogInformation("Resource deletion failed: resource {ResourceId} not found", id);
                return ServiceResult<bool>.Failed(
                    ServiceErrorCode.NotFound,
                    $"Resource with ID {id} not found.");
            }

            if (resource.IsActive)
            {
                _logger.LogInformation(
                    "Resource deletion rejected: resource {ResourceId} is still active and must be deactivated first",
                    id);
                return ServiceResult<bool>.Failed(
                    ServiceErrorCode.Conflict,
                    "Please deactivate the resource before deleting it.");
            }

            bool hasActiveBookings = await _context.Bookings.AnyAsync(b =>
                b.ResourceId == id
                && b.EndTimeUtc >= DateTime.UtcNow
                && b.Status != BookingStatus.Cancelled);

            if (hasActiveBookings)
            {
                _logger.LogInformation(
                    "Resource deletion rejected: resource {ResourceId} has active or upcoming bookings",
                    id);
                return ServiceResult<bool>.Failed(
                    ServiceErrorCode.Conflict,
                    "Cannot delete this resource because it has active or upcoming bookings.");
            }

            //remove
            _context.Resources.Remove(resource);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Resource {ResourceId} deleted (Name={ResourceName})",
                id, resource.Name);
                return ServiceResult<bool>.Successful(true);
        }

        public async Task<ServiceResult<ResourceDto>> UpdateResourceAsync(int id ,CreateResourceRequest updateResourceRequest)
        {
            var resource = await _context.Resources.FindAsync(id);
            if (resource == null)
            {
                _logger.LogInformation("Resource update failed: resource {ResourceId} not found", id);
                return ServiceResult<ResourceDto>.Failed(
                    ServiceErrorCode.NotFound,
                    $"Resource with ID {id} not found.");
            }

            //now check for the resource name
            bool exists = await _context.Resources.AnyAsync(r => r.Name == updateResourceRequest.Name && r.Id != id);
            if (exists)
            {
                _logger.LogInformation(
                    "Resource update rejected: name {ResourceName} is already used by another resource (updating {ResourceId})",
                    updateResourceRequest.Name, id);
                return ServiceResult<ResourceDto>.Failed(
                    ServiceErrorCode.AlreadyExists,
                    $"Resource with name '{updateResourceRequest.Name}' already exists.");
            }

            //update changes
            resource.Name = updateResourceRequest.Name;
            resource.Description = updateResourceRequest.Description;
            resource.IsActive = updateResourceRequest.IsActive;
            resource.Category = updateResourceRequest.Category;
            resource.Capacity = updateResourceRequest.Capacity;

            //persist
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Resource {ResourceId} updated: Name={ResourceName}, Category={Category}, Capacity={Capacity}, IsActive={IsActive}",
                resource.Id, resource.Name, resource.Category, resource.Capacity, resource.IsActive);
            return ServiceResult<ResourceDto>.Successful(new ResourceDto
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Capacity = resource.Capacity,
                IsActive = resource.IsActive,
                Category = resource.Category
            });
        }

        public async Task<ServiceResult<bool>> DeactivateResourceAsync(int? id)
        {

            var resource = await _context.Resources.FindAsync(id);

            if (resource == null)
            {
                _logger.LogInformation("Resource deactivation failed: resource {ResourceId} not found", id);
                return ServiceResult<bool>.Failed(
                    ServiceErrorCode.NotFound,
                    $"Resource with ID:{id} was not found");
            }

            if (!resource.IsActive)
            {
                _logger.LogInformation(
                    "Resource deactivation rejected: resource {ResourceId} is already deactivated",
                    id);
                return ServiceResult<bool>.Failed(
                    ServiceErrorCode.Conflict,
                    "Resource is already deactivated.");
            }

            resource.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Resource {ResourceId} deactivated", id);
            return ServiceResult<bool>.Successful(true);
        }

        public async Task<ServiceResult<List<AvailableWindowDto>>> GetResourceAvailabilityAsync(int resourceId, DateOnly? date = null, int durationMinutes = 60)
        {
            if (durationMinutes <= 0 || durationMinutes > 1440)
            {
                _logger.LogInformation(
                    "Availability check rejected for resource {ResourceId}: invalid duration {DurationMinutes} minutes",
                    resourceId, durationMinutes);
                return ServiceResult<List<AvailableWindowDto>>.Failed(
                    ServiceErrorCode.InvalidRequest, 
                    "Duration must be between 1 and 1440 minutes.");
            }

            var resourceExists = await _context.Resources
                .AsNoTracking()
                .AnyAsync(r => r.Id == resourceId && r.IsActive);
            if (!resourceExists)
            {
                _logger.LogInformation(
                    "Availability check failed: active resource {ResourceId} not found",
                    resourceId);
                return ServiceResult<List<AvailableWindowDto>>.Failed(
                    ServiceErrorCode.NotFound, 
                    $"Active resource with ID {resourceId} was not found.");
            }

            DateOnly targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            DateTime dayStartUtc = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime dayEndUtc = targetDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime nowUtc = DateTime.UtcNow;

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.ResourceId == resourceId
                         && b.Status != BookingStatus.Cancelled
                         && b.StartTimeUtc < dayEndUtc
                         && b.EndTimeUtc > dayStartUtc)
                .OrderBy(b => b.StartTimeUtc)
                .Select(b => new { b.StartTimeUtc, b.EndTimeUtc })
                .ToListAsync();

            _logger.LogInformation(
                "Found {BookingCount} non-cancelled bookings for resource {ResourceId} on {TargetDate}",
                bookings.Count, resourceId, targetDate);

            var validStartWindows = new List<AvailableWindowDto>();
            DateTime currentPointer = dayStartUtc > nowUtc ? dayStartUtc : nowUtc;

            foreach (var booking in bookings)
            {
                if (booking.EndTimeUtc <= currentPointer) continue;

                if (booking.StartTimeUtc > currentPointer)
                {
                    // Calculate max allowed start time for this gap
                    DateTime maxStartTime = booking.StartTimeUtc.AddMinutes(-durationMinutes);

                    if (maxStartTime >= currentPointer)
                    {
                        validStartWindows.Add(new AvailableWindowDto
                        {
                            WindowStartUtc = currentPointer,
                            LatestStartTimeUtc = maxStartTime
                        });
                    }
                }

                if (booking.EndTimeUtc > currentPointer)
                {
                    currentPointer = booking.EndTimeUtc;
                }
            }

            // Evaluate tail end of the day
            DateTime maxTailStartTime = dayEndUtc.AddMinutes(-durationMinutes);
            if (maxTailStartTime >= currentPointer)
            {
                validStartWindows.Add(new AvailableWindowDto
                {
                    WindowStartUtc = currentPointer,
                    LatestStartTimeUtc = maxTailStartTime
                });
            }

            _logger.LogInformation(
                "Availability for resource {ResourceId} on {TargetDate} ({DurationMinutes} min): {WindowCount} window(s) found",
                resourceId, targetDate, durationMinutes, validStartWindows.Count);

            return ServiceResult<List<AvailableWindowDto>>.Successful(validStartWindows);
        }
    }
}
