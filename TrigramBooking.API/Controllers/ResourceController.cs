using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/resources")]
    public class ResourceController : ControllerBase
    {
        private readonly TrigramDbContext _context;
        private readonly ILogger<ResourceController> _logger;
        private readonly IResourceService _resourceService;

        public ResourceController(TrigramDbContext context, 
            ILogger<ResourceController> logger,
            IResourceService resourceService)
        {
            _context = context;
            _logger = logger;
            _resourceService = resourceService;
        }

        [HttpGet]
        [Authorize]

        public async Task<IActionResult> GetResources(
            [FromQuery] int? capacity,
            [FromQuery] string? category,
            [FromQuery] bool? isActive)
        {
            var resources = await _resourceService.GetResourcesAsync(capacity, category, isActive);
            return Ok(resources.Data);
        }

        [HttpGet("{id}")]
        [Authorize]

        public async Task<IActionResult> GetResourceById(int id)
        {
            var resourceResult = await _resourceService.GetResourcesByIdAsync(id);

            if (!resourceResult.Success)
            {
                return NotFound(new { Message = resourceResult.Error?.Message });
            }

            return Ok(resourceResult.Data);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest resourceRequest)
        {
            var resource = await _resourceService.CreateResourceAsync(resourceRequest);

            if(!resource.Success)
            {
                return Conflict(new { Message = resource.Error!.Message });
            }

            return CreatedAtAction(nameof(GetResourceById), new { id = resource.Data!.Id }, resource.Data);
        }

        [HttpPut("{id}")]
        [Authorize]

        public async Task<IActionResult> UpdateResource(int id, [FromBody] CreateResourceRequest updateResource)
        {
            var resource = await _context.Resources.FindAsync(id);
            if (resource == null)
            {
                _logger.LogInformation("Resource update failed: resource {ResourceId} not found", id);
                return NotFound(new { Message = $"The resource with id:{id} was not found" });
            }

            //now check for the resource name
            bool exists = await _context.Resources.AnyAsync(r => r.Name == updateResource.Name && r.Id != id);
            if (exists)
            {
                _logger.LogInformation(
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
        [Authorize]
        public async Task<IActionResult> DeleteResource(int id)
        {
            _logger.LogInformation("Deleting resource {ResourceId}", id);

            //find resource
            var resource = await _context.Resources.FindAsync(id);

            //check if exists
            if (resource == null)
            {
                _logger.LogInformation("Resource deletion failed: resource {ResourceId} not found", id);
                return NotFound(new { Message = $"The resource:{id} was not found" });
            }

            if (resource.IsActive)
            {
                _logger.LogInformation(
                    "Resource deletion rejected: resource {ResourceId} is still active and must be deactivated first",
                    id);
                return Conflict(new { Message = "Please deactivate the resource before deleting it." });
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
        [Authorize]
        public async Task<IActionResult> DeactivateResource(int id)
        {
            var resource = await _context.Resources.FindAsync(id);

            if (resource == null)
            {
                _logger.LogInformation("Resource deactivation failed: resource {ResourceId} not found", id);
                return NotFound(new { Message = $"Resource with ID:{id} was not found" });
            }

            if (!resource.IsActive)
            {
                _logger.LogInformation(
                    "Resource deactivation rejected: resource {ResourceId} is already deactivated",
                    id);
                return Conflict(new { Message = "Resource is already deactivated." });
            }

            resource.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Resource {ResourceId} deactivated", id);
            return Ok(new { Message = $"Resource {id} has been deactivated successfully." });
        }

        [HttpGet("{id}/availability")]
        [Authorize]
        public async Task<IActionResult> GetResourceAvailability(
            int id,
            [FromQuery] DateOnly? date = null,
            [FromQuery] int durationMinutes = 60)
        {
            if (durationMinutes <= 0 || durationMinutes > 1440)
            {
                _logger.LogInformation(
                    "Availability check rejected for resource {ResourceId}: invalid duration {DurationMinutes} minutes",
                    id, durationMinutes);
                return BadRequest(new { Message = "Duration must be between 1 and 1440 minutes." });
            }

            var resourceExists = await _context.Resources
                .AsNoTracking()
                .AnyAsync(r => r.Id == id && r.IsActive);

            if (!resourceExists)
            {
                _logger.LogInformation(
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

            _logger.LogInformation(
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