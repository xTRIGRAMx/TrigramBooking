using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Extensions;
using TrigramBooking.API.Extentions;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/bookings")]
    public class BookingController : ControllerBase
    {

        private readonly TrigramDbContext _context;
        private readonly ILogger<BookingController> _logger;
        private readonly IBookingService _bookingService;

        public BookingController(
            TrigramDbContext context, 
            ILogger<BookingController> logger, 
            IBookingService bookingService)
        {
            _context = context;
            _logger = logger;
            _bookingService = bookingService;
        }

        // POST: BookingController/Delete/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateBooking(CreateBookingRequest bookingRequest)
        {
            int userId = User.GetUserId().Value;
            var createBookingResult = await _bookingService.CreateBookingAsync(userId, bookingRequest);
            
            if(!createBookingResult.Success)
            {
                return createBookingResult.ToErrorResponse();
            }
            #region Response Mapping

            return CreatedAtAction(
                nameof(GetBookingById),
                new { id = createBookingResult.Data?.Id },
                createBookingResult.Data);

            #endregion
        }

        [HttpPatch("{id}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelBooking(int id)
        {
            var cancelBookingResult = await _bookingService.CancelBookingAsync(id, User.GetUserId().Value);
            if (!cancelBookingResult.Success)
            { 
                return cancelBookingResult.ToErrorResponse();
            }
            return Ok(cancelBookingResult.Data);
        }

        // Single booking by ID
        //projection vs eager loading to be studied further

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingById(int id)
        {
            var bookingResult = await _bookingService.GetBookingByIdAsync(id);
            if (!bookingResult.Success)
            {
                return bookingResult.ToErrorResponse();
            } 
            return Ok(bookingResult.Data);
        }

        //projection vs eager loading to be studied further
        //TODO Add pagnation to the website
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetBookings(
            [FromQuery] int? userId,
            [FromQuery] int? resourceId,
            [FromQuery] DateTime? startDateUtc,
            [FromQuery] DateTime? endDateUtc)
        {
            var bookingResult = await _bookingService.GetBookingAsync(userId, resourceId, startDateUtc, endDateUtc);
            if (!bookingResult.Success)
            {
                return bookingResult.ToErrorResponse();
            }
            return Ok(bookingResult.Data);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            #region Authentication & Booking Retrieval

            var actingUserId = User.GetUserId();

            var booking = await _bookingService.DeleteBookingAsync(actingUserId.Value, id);

            #endregion

            if (!booking.Success)
            {
                return booking.ToErrorResponse();
            } 
            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> RescheduleBooking(
            int id,
            RescheduleBookingRequest rescheduleRequest)
        {
            var actingUserId = User.GetUserId();
            var rescheduleResult = await _bookingService.RescheduleBookingAsync(actingUserId.Value, id, rescheduleRequest);
            if (!rescheduleResult.Success)
            {
                return rescheduleResult.ToErrorResponse();
            }

            return Ok(rescheduleResult.Data);
        }
    }
}