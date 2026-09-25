namespace TrigramBooking.API.DTOs
{
    public class CreateResourceRequest
    {
        public string Name { get; set; } = string.Empty;

        public int Capacity { get; set; } = 1;

        public string Category { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
