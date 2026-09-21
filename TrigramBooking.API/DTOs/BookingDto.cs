using TrigramBooking.API.Models;

namespace TrigramBooking.API.DTOs
{
    public class BookingDto
    {
        public int Id { get; set; }
        public int ResourceId { get; set; }
        public string ResourceName { get; set; }   // from Resource
        public int UserId { get; set; }
        public string UserName { get; set; }       // from User
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; }
        public BookingStatus Status { get; set; }
    }
}
