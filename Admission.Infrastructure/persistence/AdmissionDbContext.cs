using Admission.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Admission.Infrastructure.persistence
{
    public class AdmissionDbContext:DbContext

    {

        public AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : base(options) { }

        public DbSet<Student> students => Set<Student>();
        public DbSet<Batch> batchs => Set<Batch>();
        public DbSet<Course> courses => Set<Course>();
        public DbSet<Registration > registrations => Set<Registration>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureStudent(modelBuilder);
            ConfigureRegistration(modelBuilder);
            ConfigureBatch(modelBuilder);
        }

        private void ConfigureStudent(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(x => x.StudentId);

                entity.Property(x => x.Name).IsRequired()
                   .HasMaxLength(150);

                entity.Property(x => x.Email).IsRequired()
                   .HasMaxLength(150);

                entity.Property(x => x.Phone).IsRequired()
                   .HasMaxLength(10);

                entity.HasIndex(x => x.Email).IsUnique();



            });
        }

            private void ConfigureBatch(ModelBuilder modelBuilder) {

            modelBuilder.Entity<Batch>(entity =>
            {
                entity.HasKey(x => x.BatchId);
                entity.Property(x => x.Capacity).IsRequired();
                entity.Property(x => x.CurrentStrength).IsRequired();
                entity.HasOne<Course>().WithMany()
                .HasForeignKey(x => x.CourseId);

            });


        }

        private void ConfigureRegistration(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Registration>(entity =>
            {
                entity.HasKey(x => x.RegistrationId);
                entity.Property(x => x.ReceiptId).IsRequired(false);
                entity.Property(x => x.Status).IsRequired();
                entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Batch>().WithMany().HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
            });

        }


    }
}
