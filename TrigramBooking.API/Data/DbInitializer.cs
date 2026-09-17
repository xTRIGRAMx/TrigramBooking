using Microsoft.EntityFrameworkCore;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Data
{
    public class DbInitializer
    {
        public static void Initialize(TrigramDbContext context)
        {
            // Apply any pending migrations
            context.Database.Migrate();

            // Check whether the database already contains users
            if (!context.Users.Any())
            {
                var users = new List<User>
                {
                    new User
                    {
                        UserName = "John Doe",
                        Email = "user@gmail.com",
                        CreatedAtUtc = DateTime.UtcNow,
                        PasswordHash = "Hashbrown"
                    }
                };
                context.Users.AddRange(users);
            }
            if (!context.Resources.Any())
            {
                var resources = new List<Resource>
                {
                    new Resource
                    {
                        Name = "Boardroom 2",
                        Capacity = 15,
                        Category = "Room",
                        Description = "Lorem Ipsum is simply dummy text of the printing and" +
                        " typesetting industry. Lorem Ipsum has been the industry's standard " +
                        "dummy text ever since 1966, when designers at Letraset and James Mosley, " +
                        "the librarian at St Bride Printing Library in London, took a 1914 Cicero" +
                        " translation and scrambled it to make dummy text for Letraset's Body Type sheets.",
                        IsActive = false,
                    },
                    new Resource
                    {
                        Name = "Training Room",
                        Capacity = 25,
                        Category = "Training",
                        Description = "Spacious room suitable for training sessions and workshops.",
                        IsActive = true
                    },

                    new Resource
                    {
                        Name = "Boardroom",
                        Capacity = 16,
                        Category = "Meeting Room",
                        Description = "Professional boardroom for management meetings and presentations.",
                        IsActive = true
                    },

                    new Resource
                    {
                        Name = "Computer Lab",
                        Capacity = 20,
                        Category = "Technology",
                        Description = "Computer-equipped room suitable for technical training and workshops.",
                        IsActive = false
                    },

                    new Resource
                    {
                        Name = "Presentation Hall",
                        Capacity = 50,
                        Category = "Event Space",
                        Description = "Large space suitable for presentations, seminars, and company events.",
                        IsActive = false
                    }
                };
                context.Resources.AddRange(resources);
            }
            context.SaveChanges();

        }
    }
}
