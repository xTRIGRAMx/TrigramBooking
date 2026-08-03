using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks.Sources;
using TrigramBooking.API.Data;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Controllers
{
    public class BookingController : Controller
    {

        private readonly TrigramDbContext _context;

        // POST: BookingController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
        //        bool hasConflict = await _context.Bookings
        //.AnyAsync(b => b.ResourceId == resourceId
        //        && b.IsActive // Or whatever status flag your index uses
        //        && newStart < b.ExistingEnd
        //        && b.ExistingStart < newEnd);
        public async Task<bool> HasConflict(Booking booking)
        {
            return await _context.Bookings
                .AnyAsync(b => b.ResourceId == booking.ResourceId
                && b.Status == BookingStatus.Confirmed
                && booking.StartTimeUtc < b.EndTimeUtc
                && b.StartTimeUtc < booking.EndTimeUtc);
        }
    }
}
