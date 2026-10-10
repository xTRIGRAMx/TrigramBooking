namespace TrigramBooking.API.DTOs
{
    public class AvailableWindowDto
    {
        public DateTime WindowStartUtc { get; set; }
        public DateTime LatestStartTimeUtc { get; set; }
    }
}
