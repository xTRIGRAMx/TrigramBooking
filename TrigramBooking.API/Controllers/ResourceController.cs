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

        public ResourceController(TrigramDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetResources(
            [FromQuery] int? capacity,
            [FromQuery] string? category,
            [FromQuery] bool? isActive)
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
            return Ok(resources);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetResourceById(int id)
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

            if(resourceDto == null)
            {
                return NotFound(new { Message = $"Resource with ID {id} not found." });
            }
            return Ok(resourceDto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateResource([FromBody] CreateResourceRequest resourceRequest)
        {

            //check if a resource name exists(optional)
            bool exists = await _context.Resources.AnyAsync(r => r.Name == resourceRequest.Name);
            if(exists)
            {
                return Conflict(new { Message = $"The resource name:{resourceRequest.Name} is already in use, please change it"});
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
            return CreatedAtAction(nameof (GetResourceById),new {id = resource.Id},resourceDto);
        }

        [HttpPut("{id}")]

        public async Task<IActionResult> UpdateResource(int id, [FromBody] CreateResourceRequest updateResource)
        {
            var resource = await _context.Resources.FindAsync(id);
            if(resource == null)
            {
                return NotFound(new {Message = $"The resource with id:{id} was not found"});
            }

            //now check for the resource name
            bool exists = await _context.Resources.AnyAsync(r => r.Name == updateResource.Name && r.Id != id);
            if(exists)
            {
                return Conflict(new {Message = $"A resource named '{updateResource.Name}' already exists." });
            }

            //update changes
            resource.Name = updateResource.Name;
            resource.Description = updateResource.Description;
            resource.IsActive = updateResource.IsActive;
            resource.Category = updateResource.Category;
            resource.Capacity = updateResource.Capacity;
            
            //persist
            await _context.SaveChangesAsync();
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
            //find resource
            var resource = await _context.Resources.FindAsync(id);

            //check if exists
            if (resource == null)
            {
                return NotFound(new { Message = $"The resource:{id} was not found" });
            }

            if (resource.IsActive)
            {
                return BadRequest(new { Message = "Please deactivate the resource before deleting it." });
            }

            bool hasActiveBookings = await _context.Bookings.AnyAsync(b =>
                b.ResourceId == id
                && b.EndTimeUtc >= DateTime.UtcNow
                && b.Status != BookingStatus.Cancelled);

            if (hasActiveBookings)
            {
                return Conflict(new { Message = "Cannot delete this resource because it has active or upcoming bookings." });
            }

            //remove
            _context.Resources.Remove(resource);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPatch("{id}/deactivate")]
        public async Task<IActionResult> DeactivateResource(int id)
        {
            var resource = await _context.Resources.FindAsync(id);

            if(resource == null)
            {
                return NotFound(new { Message = $"Resource with ID:{id} was not found"});
            }

            if(!resource.IsActive)
            {
                return BadRequest(new { Message = "Resource is already deactivated." });
            }

            resource.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { Message = $"Resource {id} has been deactivated successfully." });
        }                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         
    }
}
