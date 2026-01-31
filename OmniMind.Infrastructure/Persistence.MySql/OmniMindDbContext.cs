using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OmniMind.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection.Emit;
using System.Text;

namespace OmniMind.Persistence.MySql
{
    public class OmniMindDbContext : IdentityDbContext<User, Role, string, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
    {



        public OmniMindDbContext(DbContextOptions options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            //modelBuilder.Entity<Meeting>(build =>
            //{
            //    build.Property(p => p.ConcurrencyToken).IsConcurrencyToken();
            //});
            modelBuilder.Entity<User>(build =>
            {
                build.HasIndex(p => p.PhoneNumber);
                //build.HasMany(p => p.Articles)
                //.WithOne(p => p.User)
                //.HasForeignKey(p => p.UserId);
                //build.HasOne(p => p.DoctorCetificate)
                //.WithOne(p => p.User)
                //.HasForeignKey(p => p.);
                //build.HasMany(x => x.Articles)
                //.WithOne(x => x.User)
                //.HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });
            //modelBuilder.Entity<Article>(build =>
            //{
            //    build.HasOne(x => x.User)
            //    //.WithMany(x => x.Articles)
            //    .HasForeignKey(x => x.UserId)
            //    .OnDelete(DeleteBehavior.Cascade);
            //});

        }
    }
}
