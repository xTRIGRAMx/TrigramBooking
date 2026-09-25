namespace TrigramBooking.API.DTOs
{
    public class RescheduleBookingRequest
    {
        public DateTime StartTimeUtc { get; set; }

        public DateTime EndTimeUtc { get; set; }
    }
}
