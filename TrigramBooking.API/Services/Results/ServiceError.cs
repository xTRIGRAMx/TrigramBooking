namespace TrigramBooking.API.Services.Results
{
    public class ServiceError
    {
        public required ServiceErrorCode Code { get; init; }

        public required string Message { get; init; }
    }

    public enum ServiceErrorCode
    {
        NotFound,
        AlreadyExists,
        Conflict,
        InvalidRequest,
        Unauthorized,
        Forbidden
    }
}
