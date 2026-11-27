using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using TrigramBooking.API.Data;
using TrigramBooking.API.Extensions;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services;
using TrigramBooking.API.Services.Results;
using TrigramBooking.Share.DTOs;

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
                return resourceResult.ToErrorResponse();
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
                return resource.ToErrorResponse();
            }

            return CreatedAtAction(nameof(GetResourceById), new { id = resource.Data!.Id }, resource.Data);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateResource(int id, [FromBody] CreateResourceRequest updateResource)
        {
            var resource = await _resourceService.UpdateResourceAsync(id, updateResource);
            if (!resource.Success)
            {
                resource.ToErrorResponse();
            }
            //transfer to DTO
            return Ok(resource.Data);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteResource(int id)
        {
            //find resource
            var resource = await _resourceService.DeleteResourceAsync(id);

            //check if exists
            if(!resource.Success)
            {
                resource.ToErrorResponse();
            }
            return NoContent();
        }

        [HttpPatch("{id}/deactivate")]
        [Authorize]
        public async Task<IActionResult> DeactivateResource(int id)
        {
            var resource = await _resourceService.DeactivateResourceAsync(id);

            if (!resource.Success)
            {
                resource.ToErrorResponse();
            }
            return Ok(new { Message = $"Resource {id} has been deactivated successfully." });
        }
        

        [HttpGet("{id}/availability")]
        [Authorize]
        public async Task<IActionResult> GetResourceAvailability(
            int id,
            [FromQuery] DateOnly? date = null,
            [FromQuery] int durationMinutes = 60)
        {
            var resourceAvailabilityResult = await _resourceService.GetResourceAvailabilityAsync(id, date, durationMinutes);
            if(!resourceAvailabilityResult.Success)
            {
                resourceAvailabilityResult.ToErrorResponse();
            }
            
            return Ok(resourceAvailabilityResult.Data);
        }
    }
}