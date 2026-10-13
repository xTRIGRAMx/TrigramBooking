using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/resources")]
    public class ResourceController : ControllerBase
    {
        private readonly TrigramDbContext _context;
        private readonly ILogger<ResourceController> _logger;

        public ResourceController(TrigramDbContext context, ILogger<ResourceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetResources(
            [FromQuery] int? capacity,
            [FromQuery] string? category,
            [FromQuery] bool? isActive)
        {
            _logger.LogDebug(
                "Fetching resources with filters: Capacity={Capacity}, Category={Category}, IsActive={IsActive}",
                capacity, category, isActive);

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
            return Ok(resources);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetResourceById(int id)
        {
            _logger.LogDebug("Fetching resource {ResourceId}", id);

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
                _logger.LogWarning("Resource {ResourceId} not found", id);
                return NotFound(new { Message = $"Resource with ID {id} not found." });
            }

            _logger.LogDebug("Resource {ResourceId} retrieved successfully", id);
            return Ok(resourceDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest resourceRequest)
        {
            _logger.LogDebug(
                "Creating resource: Name={ResourceName}, Category={Category}, Capacity={Capacity}, IsActive={IsActive}",
                resourceRequest.Name, resourceRequest.Category, resourceRequest.Capacity, resourceRequest.IsActive);

            //check if a resource name exists(optional)
            bool exists = await _context.Resources.AnyAsync(r => r.Name == resourceRequest.Name);
            if (exists)
            {
                _logger.LogWarning(
                    "Resource creation rejected: name {ResourceName} is already in use",
                    resourceRequest.Name);
                return Conflict(new { Message = $"The resource name:{resourceRequest.Name} is already in use, please change it" });
            }

            //Map requestDto to EF core entity
            Resource resource = new Resource()
            {
                Name = resourceRequest.Name,
                Capacity = resourceRequest.Capacity,
                Category = resourceRequest.Category,
                Description = resourceRequest.Description,
                IsActive = resourceRequest.IsActive
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
            //Return 201 Created with Location header pointing to GetResourceById
            return CreatedAtAction(nameof(GetResourceById), new { id = resource.Id }, resourceDto);
        }

        [HttpPut("{id}")]

        public async Task<IActionResult> UpdateResource(int id, [FromBody] CreateResourceRequest updateResource)
        {
            _logger.LogDebug(
                "Updating resource {ResourceId}: Name={ResourceName}, Category={Category}, Capacity={Capacity}, IsActive={IsActive}",
                id, updateResource.Name, updateResource.Category, updateResource.Capacity, updateResource.IsActive);

            var resource = await _context.Resources.FindAsync(id);
            if (resource == null)
            {
                _logger.LogWarning("Resource update failed: resource {ResourceId} not found", id);
                return NotFound(new { Message = $"The resource with id:{id} was not found" });
            }

            //now check for the resource name
            bool exists = await _context.Resources.AnyAsync(r => r.Name == updateResource.Name && r.Id != id);
            if (exists)
            {
                _logger.LogWarning(
                    "Resource update rejected: name {ResourceName} is already used by another resource (updating {ResourceId})",
                    updateResource.Name, id);
                return Conflict(new { Message = $"A resource named '{updateResource.Name}' already exists." });
            }

            //update changes
            resource.Name = updateResource.Name;
            resource.Description = updateResource.Description;
            resource.IsActive = updateResource.IsActive;
            resource.Category = updateResource.Category;
            resource.Capacity = updateResource.Capacity;

            //persist
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Resource {ResourceId} updated: Name={ResourceName}, Category={Category}, Capacity={Capacity}, IsActive={IsActive}",
                resource.Id, resource.Name, resource.Category, resource.Capacity, resource.IsActive);

            //transfer to DTO
            return Ok(new ResourceDto
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                Capacity = resource.Capacity,
                IsActive = resource.IsActive,
                Category = resource.Category
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResource(int id)
        {
            _logger.LogDebug("Deleting resource {ResourceId}", id);

            //find resource
            var resource = await _context.Resources.FindAsync(id);

            //check if exists
            if (resource == null)
            {
                _logger.LogWarning("Resource deletion failed: resource {ResourceId} not found", id);
                return NotFound(new { Message = $"The resource:{id} was not found" });
            }

            if (resource.IsActive)
            {
                _logger.LogWarning(
                    "Resource deletion rejected: resource {ResourceId} is still active and must be deactivated first",
                    id);
                return BadRequest(new { Message = "Please deactivate the resource before deleting it." });
            }

            bool hasActiveBookings = await _context.Bookings.AnyAsync(b =>
                b.ResourceId == id
                && b.EndTimeUtc >= DateTime.UtcNow
                && b.Status != BookingStatus.Cancelled);

            if (hasActiveBookings)
            {
                _logger.LogWarning(
                    "Resource deletion rejected: resource {ResourceId} has active or upcoming bookings",
                    id);
                return Conflict(new { Message = "Cannot delete this resource because it has active or upcoming bookings." });
            }

            //remove
            _context.Resources.Remove(resource);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Resource {ResourceId} deleted (Name={ResourceName})",
                id, resource.Name);
            return NoContent();
        }

        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateResource(int id)
        {
            _logger.LogDebug("Deactivating resource {ResourceId}", id);

            var resource = await _context.Resources.FindAsync(id);

            if (resource == null)
            {
                _logger.LogWarning("Resource deactivation failed: resource {ResourceId} not found", id);
                return NotFound(new { Message = $"Resource with ID:{id} was not found" });
            }

            if (!resource.IsActive)
            {
                _logger.LogWarning(
                    "Resource deactivation rejected: resource {ResourceId} is already deactivated",
                    id);
                return BadRequest(new { Message = "Resource is already deactivated." });
            }

            resource.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Resource {ResourceId} deactivated", id);
            return Ok(new { Message = $"Resource {id} has been deactivated successfully." });
        }

        [HttpGet("{id}/availability")]
        public async Task<IActionResult> GetResourceAvailability(
            int id,
            [FromQuery] DateOnly? date = null,
            [FromQuery] int durationMinutes = 60)
        {
            _logger.LogDebug(
                "Checking availability for resource {ResourceId}: Date={Date}, DurationMinutes={DurationMinutes}",
                id, date, durationMinutes);

            if (durationMinutes <= 0 || durationMinutes > 1440)
            {
                _logger.LogWarning(
                    "Availability check rejected for resource {ResourceId}: invalid duration {DurationMinutes} minutes",
                    id, durationMinutes);
                return BadRequest(new { Message = "Duration must be between 1 and 1440 minutes." });
            }

            var resourceExists = await _context.Resources
                .AsNoTracking()
                .AnyAsync(r => r.Id == id && r.IsActive);

            if (!resourceExists)
            {
                _logger.LogWarning(
                    "Availability check failed: active resource {ResourceId} not found",
                    id);
                return NotFound(new { Message = $"Active resource with ID {id} was not found." });
            }

            DateOnly targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            DateTime dayStartUtc = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime dayEndUtc = targetDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            DateTime nowUtc = DateTime.UtcNow;

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.ResourceId == id
                         && b.Status != BookingStatus.Cancelled
                         && b.StartTimeUtc < dayEndUtc
                         && b.EndTimeUtc > dayStartUtc)
                .OrderBy(b => b.StartTimeUtc)
                .Select(b => new { b.StartTimeUtc, b.EndTimeUtc })
                .ToListAsync();

            _logger.LogDebug(
                "Found {BookingCount} non-cancelled bookings for resource {ResourceId} on {TargetDate}",
                bookings.Count, id, targetDate);

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
                id, targetDate, durationMinutes, validStartWindows.Count);
            return Ok(validStartWindows);
        }
    }
}