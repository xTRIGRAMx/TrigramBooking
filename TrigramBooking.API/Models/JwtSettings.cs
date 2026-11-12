namespace TrigramBooking.API.Models
{
    public class JwtSettings
    {
        public string Key { get; set; } = default!;
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;
        public int ExpiryMinutes { get; set; }

        IList<Booking> bookings = new List<Booking>();
        void Book()
        {
            var theBook = bookings.OrderBy(b => b.StartTimeUtc).ToList();
        }
    }
}
