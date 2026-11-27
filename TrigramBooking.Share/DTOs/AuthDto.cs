namespace TrigramBooking.Share.DTOs
{
    public class AuthDto
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public DateTime TokenExpiration { get; set; } = DateTime.UtcNow.AddHours(1);
    }
}
