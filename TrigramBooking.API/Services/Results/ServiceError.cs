namespace TrigramBooking.API.Services.Results
{
    public class ServiceError
    {
        public ServiceErrorCode Code { get; set; }

        public string Message { get; set; }
    }

    public enum ServiceErrorCode
    {
        None,
        EmailAlreadyExists,
        UsernameAlreadyExists,
        ResourceNotFound,
        ResourceStillActive,
        ResourceHasActiveBookings,
        BookingNotFound,
        BookingConflict,
        BookingAlreadyCancelled,
        InvalidRequest
    }
}
