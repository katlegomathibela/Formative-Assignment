

using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public class RaceDayContext : DbContext
{
    public RaceDayContext(DbContextOptions<RaceDayContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RaceDay.Api.Models.Route> Routes
        => Set<RaceDay.Api.Models.Route>();
    public DbSet<EventEnrollment> EventEnrollments
        => Set<EventEnrollment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.FirstName).HasColumnName("first_name");
            entity.Property(x => x.LastName).HasColumnName("last_name");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.Role).HasColumnName("role");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.OrganiserId).HasColumnName("organiser_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.EventDate).HasColumnName("event_date")
                .HasColumnType("date");
            entity.Property(x => x.Location).HasColumnName("location");
            entity.Property(x => x.Distance).HasColumnName("distance")
                .HasColumnType("decimal(6,2)");
            entity.Property(x => x.EventType).HasColumnName("event_type");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Category");
            entity.HasKey(x => x.CategoryId);
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.CategoryType).HasColumnName("category_type");
            entity.Property(x => x.Value).HasColumnName("value");
        });

        modelBuilder.Entity<RaceDay.Api.Models.Route>(entity =>
        {
            entity.ToTable("Route");
            entity.HasKey(x => x.RouteId);
            entity.Property(x => x.RouteId).HasColumnName("route_id");
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.DistanceKm).HasColumnName("distance_km")
                .HasColumnType("decimal(6,2)");
            entity.Property(x => x.StartLocation).HasColumnName("start_location");
            entity.Property(x => x.FinishLocation).HasColumnName("finish_location");
            entity.Property(x => x.RouteData).HasColumnName("route_data");
        });

        modelBuilder.Entity<EventEnrollment>(entity =>
        {
            entity.ToTable("Event_Enrollment");
            entity.HasKey(x => x.EnrollmentId);
            entity.Property(x => x.EnrollmentId).HasColumnName("enrollment_id");
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.ParticipantId).HasColumnName("participant_id");
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.RegistrationDate).HasColumnName("registration_date");
            entity.Property(x => x.Status).HasColumnName("status");
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.ToTable("Result");
            entity.HasKey(x => x.ResultId);
            entity.Property(x => x.ResultId).HasColumnName("result_id");
            entity.Property(x => x.EnrollmentId).HasColumnName("enrollment_id");
            entity.Property(x => x.FinishTime).HasColumnName("finish_time");
            entity.Property(x => x.FinishPosition).HasColumnName("finish_position");
        });
    }
}
