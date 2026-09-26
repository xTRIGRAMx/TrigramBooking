using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public BookingController(TrigramDbContext context)
        {
            _context = context;
        }

        // POST: BookingController/Delete/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateBooking(CreateBookingRequest bookingRequest)
        {
            var userId = User.GetUserId();
            if (userId == null) return Unauthorized();

            bool hasConflict = await HasConflictAsync(bookingRequest.StartTimeUtc, bookingRequest.EndTimeUtc, bookingRequest.ResourceId);

            if (hasConflict)
            {
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

        //clearer routing
        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            //checks if the booking exists first
            if (booking == null)
            {
                return NotFound("The booking you are looking for does not exist or what not found");
            }

            //checks if the booking is already canceled
            if (booking.Status == BookingStatus.Cancelled)
            {
                return BadRequest("The booking you selected has already been cancled");
            }

            ////checks if it's past booking
            //if(booking.EndTimeUtc < DateTime.UtcNow)
            //{
            //    return BadRequest("The booking you tried to cancel has already ended");
            //}

            booking.Status = BookingStatus.Cancelled;

            await _context.SaveChangesAsync();

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
                return NotFound(new { Message = $"Booking with ID {id} not found." });

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

            return Ok(bookings);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            //Fetch the entity without hasNoTracking so EF tracks it
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound(new { Message = $"Booking with ID {id} was not found." });
            }

            //Mark the entity for removal in the change tracker
            _context.Bookings.Remove(booking);

            //execute SQL Delete
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> RescheduleBooking(int id,RescheduleBookingRequest rescheduleRequest)
        {
            if (rescheduleRequest.StartTimeUtc >= rescheduleRequest.EndTimeUtc) 
            {
                return BadRequest(new { Message = "End time must be after the start time"});
            }

            if(rescheduleRequest.StartTimeUtc < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "You can't change the past, you can only cancel it" });
            }

            //fetch tracked entity
            var booking = await _context.Bookings.FindAsync(id);

            //if booking doesn't exist
            if(booking == null)
            {
                return NotFound(new { Message = $"The booking with {id} was not found" });
            }

            if(booking.Status == BookingStatus.Cancelled)
            {
                return BadRequest(new { Message = " Can't reschedule a booking that has been cancelled " });
            }

            if(booking.EndTimeUtc < DateTime.UtcNow)
            {
                return BadRequest(new { Message = "You can't go back to the past, you can only cancel it" });
            }

            //use the resource ID from booking
            bool hasConflict = await HasConflictAsync(
                rescheduleRequest.StartTimeUtc,
                rescheduleRequest.EndTimeUtc,
                booking.ResourceId,
                ignorableId:id);

            if(hasConflict)
            {
                return Conflict(new { Message = "The selected resource is not available for the requested new time slot." });
            }

            //update changes
            booking.StartTimeUtc = rescheduleRequest.StartTimeUtc;
            booking.EndTimeUtc = rescheduleRequest.EndTimeUtc;

            await _context.SaveChangesAsync();


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
