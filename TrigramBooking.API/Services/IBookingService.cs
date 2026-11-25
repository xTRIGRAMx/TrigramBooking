using Microsoft.AspNetCore.Mvc;
using TrigramBooking.API.DTOs;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Services
{
    public interface IBookingService
    {
        Task<ServiceResult<BookingDto>> GetBookingByIdAsync(int? id);
        Task<ServiceResult<List<BookingDto>>> GetBookingAsync(int? userId,
            int? resourceId,
            DateTime? startDateUtc,
            DateTime? endDateUtc);
        Task<ServiceResult<BookingDto>> CreateBookingAsync(int userId,CreateBookingRequest bookingRequest);
        Task<ServiceResult<BookingDto>> CancelBookingAsync(int id, int actingId);
        Task<ServiceResult<bool>> DeleteBookingAsync(int actingId,int id);
        Task<ServiceResult<BookingDto>> RescheduleBookingAsync(int actingId,int id, RescheduleBookingRequest rescheduleRequest);


    }
}
