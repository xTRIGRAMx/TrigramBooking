namespace TrigramBooking.API.Models
{
    public class Resource
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Capacity { get; set; } = 1;

        public string Category {  get; set; } = string.Empty;

        public string? Description { get; set; } 

        public bool IsActive { get; set; } = true;

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    }
}
