namespace TrigramBooking.API.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int ResourceId {  get; set; }

        public Resource? Resource { get; set; }

        public int UserId {  get; set; }

        public User? User { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public DateTime EndTimeUtc { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Confirmed;

        public string? RecurrenceRule {  get; set; }
        
    }
}
