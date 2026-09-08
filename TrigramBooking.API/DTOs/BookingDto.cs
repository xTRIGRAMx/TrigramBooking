namespace TrigramBooking.API.DTOs
{
    public class BookingDto
    {
        // TODO:data annotations to DTO's, particularly checking if start date is less than end date, fair
        public int Id { get; set; }

        public int ResourceId { get; set; }

        public DateTime StartTimeUtc { get; set; }

        public DateTime EndTimeUtc { get; set; }
    }
}
