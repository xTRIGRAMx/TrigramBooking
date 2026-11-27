using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;
using TrigramBooking.API.Data;
using TrigramBooking.API.Extentions;
using TrigramBooking.API.Models;
using TrigramBooking.API.Services.Results;
using TrigramBooking.Share.DTOs;



namespace TrigramBooking.API.Services
{
    public class BookingService: IBookingService
    {
        private readonly TrigramDbContext _context;
        private readonly ILogger<BookingService> _logger;

        public BookingService(TrigramDbContext context, ILogger<BookingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<BookingDto>> CreateBookingAsync(int userId, CreateBookingRequest bookingRequest)
        {
            #region Date Validation

            if (bookingRequest.StartTimeUtc >= bookingRequest.EndTimeUtc)
            {
                _logger.LogInformation("Invalid booking time range");
                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.InvalidRequest,
                    "End time must be after the start time");
            }

            if (bookingRequest.StartTimeUtc < DateTime.UtcNow)
            {
                _logger.LogInformation("Booking start time is in the past");

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.InvalidRequest, 
                    "Booking start time cannot be in the past");
            }

            #endregion

            #region Resource Validation

            var resource = await _context.Resources
                .AsNoTracking()
                .Where(r => r.Id == bookingRequest.ResourceId)
                .Select(r => new { r.IsActive })
                .FirstOrDefaultAsync();

            if (resource == null)
            {
                _logger.LogInformation("Resource {ResourceId} doesn't exist", bookingRequest.ResourceId);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.NotFound, 
                    "Resource doesn't exist");
            }

            if (!resource.IsActive)
            {
                _logger.LogInformation("Resource is currently unavailable"); 

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, 
                    "Resource is currently unavailable");
            }

            #endregion

            #region Booking Conflict Validation

            bool hasConflict = await HasConflictAsync(
                bookingRequest.StartTimeUtc,
                bookingRequest.EndTimeUtc,
                bookingRequest.ResourceId);

            if (hasConflict)
            {
                _logger.LogInformation(
                    "Booking creation rejected for user {UserId}: conflict on resource {ResourceId}",
                    userId, bookingRequest.ResourceId);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, 
                    "Resource already has a booking during this timeslot");
            }

            #endregion

            #region Booking Creation

            var booking = new Booking
            {
                UserId = userId,
                ResourceId = bookingRequest.ResourceId,
                StartTimeUtc = bookingRequest.StartTimeUtc,
                EndTimeUtc = bookingRequest.EndTimeUtc,
                Status = Models.BookingStatus.Confirmed
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} created by user {UserId}",
                booking.Id, booking.UserId);

            #endregion

            #region Response Mapping

            var bookingDto = new BookingDto
            {
                Id = booking.Id,
                UserId = booking.UserId,
                ResourceId = booking.ResourceId,
                StartTimeUtc = booking.StartTimeUtc,
                EndTimeUtc = booking.EndTimeUtc
            };
            
            return ServiceResult<BookingDto>.Successful(bookingDto);
            #endregion
        }

        public async Task<ServiceResult<BookingDto>> CancelBookingAsync(int id,int actingId)
        {
            #region Authentication & Booking Retrieval

            var actingUserId = actingId;
            var booking = await _context.Bookings.FindAsync(id);

            #endregion

            #region Booking Existence Validation

            if (booking == null)
            {
                _logger.LogInformation(
                    "Booking cancellation failed: booking {BookingId} not found (ActingUserId={ActingUserId})",
                    id, actingUserId);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.NotFound, 
                    "The requested booking was not found.");
            }

            #endregion

            #region Cancellation Validation

            // Check if the booking is already cancelled
            if (booking.Status == Models.BookingStatus.Cancelled)
            {
                _logger.LogInformation(
                    "Booking cancellation rejected: booking {BookingId} is already cancelled (ActingUserId={ActingUserId})",
                    id, actingUserId);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, 
                    "The booking you selected has already been cancelled.");
            }

            #endregion

            #region Booking Cancellation

            booking.Status = Models.BookingStatus.Cancelled;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} cancelled by user {ActingUserId} (OwnerUserId={OwnerUserId}, ResourceId={ResourceId})",
                booking.Id, actingUserId, booking.UserId, booking.ResourceId);

            #endregion

            #region Response Mapping

            var bookingDto = new BookingDto
            {
                Id = booking.Id,
                StartTimeUtc = booking.StartTimeUtc,
                EndTimeUtc = booking.EndTimeUtc,
                Status = (Share.DTOs.BookingStatus)booking.Status,
            };

            return ServiceResult<BookingDto>.Successful(bookingDto);
            #endregion
        }

        public async Task<ServiceResult<bool>> DeleteBookingAsync(int actingId,int id)
        {
            #region Authentication & Booking Retrieval

            var actingUserId = actingId;

            var booking = await _context.Bookings.FindAsync(id);

            #endregion

            #region Booking Existence Validation

            if (booking == null)
            {
                _logger.LogInformation(
                    "Booking deletion failed: booking {BookingId} not found (ActingUserId={ActingUserId})",
                    id, actingUserId);

                return ServiceResult<bool>.Failed(ServiceErrorCode.NotFound, 
                    $"Booking with ID {id} was not found.");
            }

            #endregion

            #region Booking Deletion

            // Mark the entity for removal in the change tracker
            _context.Bookings.Remove(booking);

            // Execute SQL DELETE
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Booking {BookingId} deleted by user {ActingUserId} (OwnerUserId={OwnerUserId}, ResourceId={ResourceId}, Status={Status})",
                id, actingUserId, booking.UserId, booking.ResourceId, booking.Status);

            #endregion

            #region Response

            return ServiceResult<bool>.Successful(true);

            #endregion
        }

        public async Task<ServiceResult<List<BookingDto>>> GetBookingAsync(int? userId, int? resourceId, DateTime? startDateUtc, DateTime? endDateUtc)
        {
            #region Query Initialization

            var query = _context.Bookings.AsNoTracking();

            #endregion

            #region Optional Filters

            if (userId.HasValue)
                query = query.Where(b => b.UserId == userId.Value);

            if (resourceId.HasValue)
                query = query.Where(b => b.ResourceId == resourceId.Value);

            #endregion

            #region Date Range Validation
            if (startDateUtc.HasValue && endDateUtc.HasValue)
            {
                if (startDateUtc.Value >= endDateUtc.Value)
                {
                    return ServiceResult<List<BookingDto>>.Failed(ServiceErrorCode.InvalidRequest, 
                    "Start date must be earlier than end date.");
                }
            }

            #endregion

            #region Date Range Filtering

            // Retrieve bookings that overlap with the requested date range.
            if (startDateUtc.HasValue)
                query = query.Where(b => b.EndTimeUtc > startDateUtc.Value);

            if (endDateUtc.HasValue)
                query = query.Where(b => b.StartTimeUtc < endDateUtc.Value);

            #endregion

            #region Booking Retrieval & Projection

            var bookings = await query
                .OrderBy(b => b.StartTimeUtc)
                .Select(b => new BookingDto
                {
                    Id = b.Id,
                    ResourceId = b.ResourceId,
                    ResourceName = b.Resource.Name,
                    UserId = b.UserId,
                    UserName = b.User.UserName,
                    StartTimeUtc = b.StartTimeUtc,
                    EndTimeUtc = b.EndTimeUtc,
                    Status = (Share.DTOs.BookingStatus)b.Status
                })
                .ToListAsync();

            #endregion

            #region Response

            _logger.LogInformation("Retrieved {BookingCount} bookings", bookings.Count);
            return ServiceResult<List<BookingDto>>.Successful(bookings);

            #endregion
        }

        public async Task<ServiceResult<BookingDto>> GetBookingByIdAsync(int? id)
        {
            #region Booking Retrieval

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
                    ResourceName = b.Resource.Name,
                    Status = (Share.DTOs.BookingStatus)b.Status
                })
                .FirstOrDefaultAsync();

            #endregion

            #region Booking Existence Validation

            if (bookingDto == null)
            {
                _logger.LogInformation("Booking {BookingId} not found", id);
                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.NotFound, 
                    $"Booking with ID {id} not found.");
            }

            #endregion

            #region Response

            _logger.LogInformation("Booking {BookingId} retrieved successfully", id);
            return ServiceResult<BookingDto>.Successful(bookingDto);

            #endregion
        }

        public async Task<ServiceResult<BookingDto>> RescheduleBookingAsync(int actingId,int id, RescheduleBookingRequest rescheduleRequest)
        {
            var actingUserId = actingId;
            var now = DateTime.UtcNow;

            #region Date Validation

            if (rescheduleRequest.StartTimeUtc >= rescheduleRequest.EndTimeUtc)
            {
                _logger.LogInformation(
                    "Reschedule rejected for booking {BookingId}: end time {EndTimeUtc:o} is not after start time {StartTimeUtc:o}",
                    id, rescheduleRequest.EndTimeUtc, rescheduleRequest.StartTimeUtc);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.InvalidRequest, "End time must be after the start time");
            }

            if (rescheduleRequest.StartTimeUtc < now)
            {
                _logger.LogInformation(
                    "Reschedule rejected for booking {BookingId}: requested start time {StartTimeUtc:o} is in the past",
                    id, rescheduleRequest.StartTimeUtc);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.InvalidRequest,
                    "You can't change the past, you can only cancel it");
            }

            #endregion


            #region Booking Retrieval

            // Fetch tracked entity
            var booking = await _context.Bookings.FindAsync(id);

            #endregion

            #region Booking Existence Validation

            if (booking == null)
            {
                _logger.LogInformation(
                    "Reschedule failed: booking {BookingId} not found (ActingUserId={ActingUserId})",
                    id, actingUserId);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.NotFound, $"The booking with {id} was not found"); 

            }
            #endregion

            #region Resource Validation
            var resource = await _context.Resources
                .AsNoTracking()
                .Where(r => r.Id == booking.ResourceId)
                .Select(r => new { r.IsActive })
                .FirstOrDefaultAsync();
            if (resource == null)
            {
                _logger.LogInformation(
                    "Reschedule failed: resource {ResourceId} associated with booking {BookingId} was not found",
                    booking.ResourceId, id);

                return ServiceResult<BookingDto>.Failed(
                    ServiceErrorCode.NotFound,
                    "The resource associated with this booking was not found.");
            }
            if (!resource.IsActive)
            {
                _logger.LogInformation(
                    "Reschedule rejected for booking {BookingId}: resource {ResourceId} is inactive",
                    id, booking.ResourceId);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, "The selected resource is currently inactive and cannot be booked.");
            }
            #endregion

            #region Booking Status Validation
            if (booking.Status == Models.BookingStatus.Cancelled)
            {
                _logger.LogInformation(
                    "Reschedule rejected: booking {BookingId} is cancelled",
                    id);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, "Can't reschedule a booking that has been cancelled.");
            }

            if (booking.EndTimeUtc < now)
            {
                _logger.LogInformation(
                    "Reschedule rejected: booking {BookingId} has already ended at {EndTimeUtc:o}",
                    id, booking.EndTimeUtc);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, "Can't reschedule a booking that has already ended.");
            }

            #endregion

            #region Booking Conflict Validation

            bool hasConflict = await HasConflictAsync(
                rescheduleRequest.StartTimeUtc,
                rescheduleRequest.EndTimeUtc,
                booking.ResourceId,
                ignorableId: id);

            if (hasConflict)
            {
                _logger.LogInformation(
                    "Reschedule rejected for booking {BookingId}: conflict on resource {ResourceId} between {StartTimeUtc:o} and {EndTimeUtc:o}",
                    id, booking.ResourceId, rescheduleRequest.StartTimeUtc, rescheduleRequest.EndTimeUtc);

                return ServiceResult<BookingDto>.Failed(ServiceErrorCode.Conflict, "The selected resource is not available for the requested new time slot.");
            }

            #endregion

            #region Booking Update

            // Keep previous times for audit logging
            var previousStartTimeUtc = booking.StartTimeUtc;
            var previousEndTimeUtc = booking.EndTimeUtc;

            // Update booking times
            booking.StartTimeUtc = rescheduleRequest.StartTimeUtc;
            booking.EndTimeUtc = rescheduleRequest.EndTimeUtc;

            await _context.SaveChangesAsync();

            #endregion

            #region Audit Logging

            _logger.LogInformation(
                "Booking {BookingId} rescheduled by user {ActingUserId} (OwnerUserId={OwnerUserId}, ResourceId={ResourceId}): " +
                "{PreviousStartTimeUtc:o}-{PreviousEndTimeUtc:o} -> {NewStartTimeUtc:o}-{NewEndTimeUtc:o}",
                booking.Id, actingUserId, booking.UserId, booking.ResourceId,
                previousStartTimeUtc, previousEndTimeUtc, booking.StartTimeUtc, booking.EndTimeUtc);

            #endregion

            #region Response Mapping

            return ServiceResult<BookingDto>.Successful(new BookingDto
            {
                Id = booking.Id,
                StartTimeUtc = booking.StartTimeUtc,
                EndTimeUtc = booking.EndTimeUtc,
                Status = (Share.DTOs.BookingStatus)booking.Status,
                UserId = booking.UserId,
                ResourceId = booking.ResourceId
            });
            #endregion
        }

        private async Task<bool> HasConflictAsync(DateTime startTime, DateTime endTime, int resourceId, int? ignorableId = null)
        {
            return await _context.Bookings
                .AnyAsync(b => b.ResourceId == resourceId
                && b.Status == Models.BookingStatus.Confirmed
                && (!ignorableId.HasValue || ignorableId.Value != b.Id)
                && startTime < b.EndTimeUtc
                && b.StartTimeUtc < endTime);
        }
    }
}

