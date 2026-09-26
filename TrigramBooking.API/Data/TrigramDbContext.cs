using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using TrigramBooking.API.Models;

namespace TrigramBooking.API.Data
{
    public class TrigramDbContext : DbContext
    {
        public TrigramDbContext(DbContextOptions<TrigramDbContext> options) : base(options) { }


        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Booking>(entity =>
            {
                entity.HasIndex(e => e.Id).IsUnique();//can be remove, but kept for transparency
                entity.HasOne(e => e.User)
                .WithMany(b=> b.Bookings)
                .HasForeignKey(u=> u.UserId)
                .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Resource)
                .WithMany(b => b.Bookings)
                .HasForeignKey(u => u.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);

                //define conflict check behavior

                entity.HasIndex(x => new {x.ResourceId, x.Status, x.StartTimeUtc,x.EndTimeUtc });
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u=> u.Id).IsUnique();//can be removed, but kept for transparency
                entity.HasIndex(u => u.UserName).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();


                entity.Property(n=> n.UserName).HasMaxLength(128).IsRequired();
                entity.Property(n => n.Email).HasMaxLength(128).IsRequired();
                entity.Property(p => p.PasswordHash).IsRequired();
            });

            modelBuilder.Entity<Resource>(entity =>
            {
                entity.Property(n => n.Name).HasMaxLength(128).IsRequired();
                entity.Property(n => n.Category).HasMaxLength(128).IsRequired();
            });
        }
    }
}
