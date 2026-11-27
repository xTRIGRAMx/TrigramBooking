using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TrigramBooking.Share.DTOs;

namespace TrigramBooking.Console
{
    internal class Program
    {
        public const string url = "https://localhost:7260/api/";
        public static readonly HttpClient client = new HttpClient()
        {
            BaseAddress = new Uri(url),
            
        };
        static async Task Main(string[] args)
        {

            // This is the JWT you received after logging in.
            string token = "your-jwt-token";
            
            // Attach the JWT to the Authorization header.
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            List<ResourceDto>? Characters = await client.GetFromJsonAsync<List<ResourceDto>>(url);
            var Register = await client.PostAsJsonAsync(url, new BookingDto
            {

            });
            System.Console.WriteLine("Hello World");
        }
    }
}
