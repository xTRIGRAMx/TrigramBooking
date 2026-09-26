namespace TrigramBooking.API.DTOs
{
    public class LoginRequest
    {
        //TODO Data annotations
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
