namespace TrigramBooking.API.DTOs
{
    public class RegisterRequest
    {
        //TODO Data annotations to be added
        public string Username { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
