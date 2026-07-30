namespace TrigramBooking.API.Models
{
    public class User
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;


        //Note: BCrypt only, never store/log plain text
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
