using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks.Sources;
using TrigramBooking.API.Data;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {

        private readonly TrigramDbContext _context;
        public BookingController(TrigramDbContext context)
        {
            _context = context;
        }


        // POST: BookingController/Delete/5
        [HttpPost("bookings")]
        public async Task<IActionResult> CreateBooking(CreateBookingRequest bookingRequest)
        {
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
                UserId = 1,//todo change to actual user id
                ResourceId = bookingRequest.ResourceId,
                StartTimeUtc = bookingRequest.StartTimeUtc,
                EndTimeUtc = bookingRequest.EndTimeUtc,
                Status = BookingStatus.Confirmed
            };
            
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(booking);
        }

        public async Task<bool> HasConflictAsync(DateTime startTime, DateTime endTime, int resourceId)
        {
            return await _context.Bookings
                .AnyAsync(b => b.ResourceId == resourceId
                && b.Status == BookingStatus.Confirmed
                && startTime < b.EndTimeUtc
                && b.StartTimeUtc < endTime);
        }
    }
}
