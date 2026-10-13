using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Extentions;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    public class BookingController : ControllerBase
    {

        private readonly TrigramDbContext _context;
        private readonly ILogger<BookingController> _logger;

        public BookingController(TrigramDbContext context, ILogger<BookingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // POST: BookingController/Delete/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateBooking(CreateBookingRequest bookingRequest)
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                _logger.LogWarning(
                    "Booking creation rejected: authenticated request has no resolvable user ID (RemoteIp={RemoteIp})",
                    HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { Message = "Please login before commiting this action" });
            }

            _logger.LogDebug(
                "User {UserId} creating booking: ResourceId={ResourceId}, StartTimeUtc={StartTimeUtc:o}, EndTimeUtc={EndTimeUtc:o}",
                userId, bookingRequest.ResourceId, bookingRequest.StartTimeUtc, bookingRequest.EndTimeUtc);

            bool hasConflict = await HasConflictAsync(bookingRequest.StartTimeUtc, bookingRequest.EndTimeUtc, bookingRequest.ResourceId);

            if (hasConflict)
            {
                _logger.LogWarning(
                    "Booking creation rejected for user {UserId}: conflict on resource {ResourceId} between {StartTimeUtc:o} and {EndTimeUtc:o}",
                    userId, bookingRequest.ResourceId, bookingRequest.StartTimeUtc, bookingRequest.EndTimeUtc);

                // 409 Conflict is the standard status code for resource overlap/double-booking!
                return Conflict(new
                {
                    Message = "You're resource is either unavailable " +
                    "or has a schedule conflict with this timeslot",
                });
            }
            // Instantiate and save the new Booking object 
            var booking = new Booking
            {
                UserId = userId.Value,//no more hardcoded 1
                ResourceId = bookingRequest.ResourceId,
                StartTimeUtc = bookingRequest.StartTimeUtc,
                EndTimeUtc = bookingRequest.EndTimeUtc,
                Status = BookingStatus.Confirmed
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} created by user {UserId}: ResourceId={ResourceId}, StartTimeUtc={StartTimeUtc:o}, EndTimeUtc={EndTimeUtc:o}",
                booking.Id, booking.UserId, booking.ResourceId, booking.StartTimeUtc, booking.EndTimeUtc);

            // Map straight to DTO using data in memory
            var bookingDto = new BookingDto
            {
                Id = booking.Id,
                UserId = booking.UserId,
                ResourceId = booking.ResourceId,
                StartTimeUtc = booking.StartTimeUtc,
                EndTimeUtc = booking.EndTimeUtc,
                // ResourceName and UserName left null/omitted to save an extra DB lookup
            };

            //return 201
            return CreatedAtAction(nameof(GetBookingById), new { id = booking.Id }, bookingDto);
        }

        [HttpPatch("{id}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var actingUserId = User.GetUserId();
            _logger.LogDebug("User {ActingUserId} cancelling booking {BookingId}", actingUserId, id);

            var booking = await _context.Bookings.FindAsync(id);

            //checks if the booking exists first
            if (booking == null)
            {
                _logger.LogWarning(
                    "Booking cancellation failed: booking {BookingId} not found (ActingUserId={ActingUserId})",
                    id, actingUserId);
                return NotFound("The booking you are looking for does not exist or what not found");
            }

            //checks if the booking is already canceled
            if (booking.Status == BookingStatus.Cancelled)
            {
                _logger.LogWarning(
                    "Booking cancellation rejected: booking {BookingId} is already cancelled (ActingUserId={ActingUserId})",
                    id, actingUserId);
                return BadRequest("The booking you selected has already been cancled");
            }

            ////checks if it's past booking
            //if(booking.EndTimeUtc < DateTime.UtcNow)
            //{
            //    return BadRequest("The booking you tried to cancel has already ended");
            //}

            booking.Status = BookingStatus.Cancelled;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} cancelled by user {ActingUserId} (OwnerUserId={OwnerUserId}, ResourceId={ResourceId})",
                booking.Id, actingUserId, booking.UserId, booking.ResourceId);

            var bookingDto = new BookingDto
            {
                Id = booking.Id,
                StartTimeUtc = booking.StartTimeUtc,
                EndTimeUtc = booking.EndTimeUtc,
                Status = booking.Status,
            };
            return Ok(bookingDto);
        }


        // Single booking by ID
        //projection vs eager loading to be studied further

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingById(int id)
        {
            _logger.LogDebug("Fetching booking {BookingId}", id);

            var bookingDto = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    ResourceId = b.ResourceId,
                    StartTimeUtc = b.StartTimeUtc,
                    EndTimeUtc = b.EndTimeUtc,
                    UserName = b.User.UserName,
                    ResourceName = b.Resource.Name
                })
                .FirstOrDefaultAsync();

            if (bookingDto == null)
            {
                _logger.LogWarning("Booking {BookingId} not found", id);
                return NotFound(new { Message = $"Booking with ID {id} not found." });
            }

            _logger.LogDebug("Booking {BookingId} retrieved successfully", id);
            return Ok(bookingDto);
        }

        //projection vs eager loading to be studied further
        [HttpGet]
        public async Task<IActionResult> GetBookings(
            [FromQuery] int? userId,
            [FromQuery] int? resourceId,
            [FromQuery] DateTime? startDateUtc,
            [FromQuery] DateTime? endDateUtc)
        {
            _logger.LogDebug(
                "Fetching bookings with filters: UserId={UserId}, ResourceId={ResourceId}, StartDateUtc={StartDateUtc:o}, EndDateUtc={EndDateUtc:o}",
                userId, resourceId, startDateUtc, endDateUtc);

            var query = _context.Bookings.AsNoTracking();

            if (userId.HasValue)
                query = query.Where(b => b.UserId == userId.Value);

            if (resourceId.HasValue)
                query = query.Where(b => b.ResourceId == resourceId.Value);

            // Correct overlap condition
            if (startDateUtc.HasValue)
                query = query.Where(b => b.EndTimeUtc > startDateUtc.Value);

            if (endDateUtc.HasValue)
                query = query.Where(b => b.StartTimeUtc < endDateUtc.Value);

            var bookings = await query
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    ResourceId = b.ResourceId,
                    ResourceName = b.Resource.Name,   // assuming Resource has Name
                    UserId = b.UserId,
                    UserName = b.User.UserName,           // assuming User has Name
                    StartTimeUtc = b.StartTimeUtc,
                    EndTimeUtc = b.EndTimeUtc,
                })
                .ToListAsync();

            _logger.LogInformation("Retrieved {BookingCount} bookings", bookings.Count);
            return Ok(bookings);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var actingUserId = User.GetUserId();
            _logger.LogDebug("User {ActingUserId} deleting booking {BookingId}", actingUserId, id);

            //Fetch the entity without hasNoTracking so EF tracks it
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null)
            {
                _logger.LogWarning(
                    "Booking deletion failed: booking {BookingId} not found (ActingUserId={ActingUserId})",
                    id, actingUserId);
                return NotFound(new { Message = $"Booking with ID {id} was not found." });
            }

            //Mark the entity for removal in the change tracker
            _context.Bookings.Remove(booking);

            //execute SQL Delete
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} deleted by user {ActingUserId} (OwnerUserId={OwnerUserId}, ResourceId={ResourceId}, Status={Status})",
                id, actingUserId, booking.UserId, booking.ResourceId, booking.Status);
            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> RescheduleBooking(int id, RescheduleBookingRequest rescheduleRequest)
        {
            var actingUserId = User.GetUserId();
            _logger.LogDebug(
                "User {ActingUserId} rescheduling booking {BookingId} to StartTimeUtc={StartTimeUtc:o}, EndTimeUtc={EndTimeUtc:o}",
                actingUserId, id, rescheduleRequest.StartTimeUtc, rescheduleRequest.EndTimeUtc);

            if (rescheduleRequest.StartTimeUtc >= rescheduleRequest.EndTimeUtc)
            {
                _logger.LogWarning(
                    "Reschedule rejected for booking {BookingId}: end time {EndTimeUtc:o} is not after start time {StartTimeUtc:o}",
                    id, rescheduleRequest.EndTimeUtc, rescheduleRequest.StartTimeUtc);
                return BadRequest(new { Message = "End time must be after the start time" });
            }

            if (rescheduleRequest.StartTimeUtc < DateTime.UtcNow)
            {
                _logger.LogWarning(
                    "Reschedule rejected for booking {BookingId}: requested start time {StartTimeUtc:o} is in the past",
                    id, rescheduleRequest.StartTimeUtc);
                return BadRequest(new { Message = "You can't change the past, you can only cancel it" });
            }

            //fetch tracked entity
            var booking = await _context.Bookings.FindAsync(id);

            //if booking doesn't exist
            if (booking == null)
            {
                _logger.LogWarning(
                    "Reschedule failed: booking {BookingId} not found (ActingUserId={ActingUserId})",
                    id, actingUserId);
                return NotFound(new { Message = $"The booking with {id} was not found" });
            }

            if (booking.Status == BookingStatus.Cancelled)
            {
                _logger.LogWarning(
                    "Reschedule rejected: booking {BookingId} is cancelled",
                    id);
                return BadRequest(new { Message = " Can't reschedule a booking that has been cancelled " });
            }

            if (booking.EndTimeUtc < DateTime.UtcNow)
            {
                _logger.LogWarning(
                    "Reschedule rejected: booking {BookingId} has already ended at {EndTimeUtc:o}",
                    id, booking.EndTimeUtc);
                return BadRequest(new { Message = "You can't go back to the past, you can only cancel it" });
            }

            //use the resource ID from booking
            bool hasConflict = await HasConflictAsync(
                rescheduleRequest.StartTimeUtc,
                rescheduleRequest.EndTimeUtc,
                booking.ResourceId,
                ignorableId: id);

            if (hasConflict)
            {
                _logger.LogWarning(
                    "Reschedule rejected for booking {BookingId}: conflict on resource {ResourceId} between {StartTimeUtc:o} and {EndTimeUtc:o}",
                    id, booking.ResourceId, rescheduleRequest.StartTimeUtc, rescheduleRequest.EndTimeUtc);
                return Conflict(new { Message = "The selected resource is not available for the requested new time slot." });
            }

            // keep the previous times so the audit log shows what changed
            var previousStartTimeUtc = booking.StartTimeUtc;
            var previousEndTimeUtc = booking.EndTimeUtc;

            //update changes
            booking.StartTimeUtc = rescheduleRequest.StartTimeUtc;
            booking.EndTimeUtc = rescheduleRequest.EndTimeUtc;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} rescheduled by user {ActingUserId} (OwnerUserId={OwnerUserId}, ResourceId={ResourceId}): " +
                "{PreviousStartTimeUtc:o}-{PreviousEndTimeUtc:o} -> {NewStartTimeUtc:o}-{NewEndTimeUtc:o}",
                booking.Id, actingUserId, booking.UserId, booking.ResourceId,
                previousStartTimeUtc, previousEndTimeUtc, booking.StartTimeUtc, booking.EndTimeUtc);

            return Ok(new BookingDto
            {
                Id = booking.Id,
                StartTimeUtc = booking.StartTimeUtc,
                EndTimeUtc = booking.EndTimeUtc,
                Status = booking.Status,
                UserId = booking.UserId,
                ResourceId = booking.ResourceId
            });
        }



        private async Task<bool> HasConflictAsync(DateTime startTime, DateTime endTime, int resourceId, int? ignorableId = null)
        {
            return await _context.Bookings
                .AnyAsync(b => b.ResourceId == resourceId
                && b.Status == BookingStatus.Confirmed
                && (!ignorableId.HasValue || ignorableId.Value != b.Id)
                && startTime < b.EndTimeUtc
                && b.StartTimeUtc < endTime);
        }
    }
}