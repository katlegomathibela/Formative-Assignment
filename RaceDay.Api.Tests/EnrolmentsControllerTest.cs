
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class EnrolmentsControllerTests
    {
        private static RaceDayContext CreateContext(
            SqliteConnection connection)
        {
            var options = new DbContextOptionsBuilder<RaceDayContext>()
                .UseSqlite(connection)
                .Options;

            var context = new RaceDayContext(options);
            context.Database.EnsureCreated();

            return context;
        }

        private static EnrolmentsController CreateController(
            RaceDayContext context,
            int userId,
            string role)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, role)
            };

            var controller = new EnrolmentsController(context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(claims, "Test"))
                }
            };

            return controller;
        }

        private static User CreateTestUser(
            RaceDayContext context,
            string email,
            string role)
        {
            var user = new User
            {
                FirstName = "Test",
                LastName = "User",
                Email = email,
                PasswordHash = "test-hash",
                Role = role
            };

            context.Users.Add(user);
            context.SaveChanges();

            return user;
        }

        private static Event CreateTestEvent(
            RaceDayContext context,
            int organiserId,
            DateTime? eventDate = null)
        {
            var raceEvent = new Event
            {
                OrganiserId = organiserId,
                Name = "City Fun Run",
                Description = "A test running event",
                EventDate = eventDate ?? DateTime.Today.AddDays(30),
                Location = "Johannesburg",
                Distance = 10m,
                EventType = "RUN"
            };

            context.Events.Add(raceEvent);
            context.SaveChanges();

            return raceEvent;
        }

        private static Category CreateTestCategory(
            RaceDayContext context,
            int eventId)
        {
            var category = new Category
            {
                EventId = eventId,
                Name = "10 km Adults",
                CategoryType = "DISTANCE",
                Value = "10 km"
            };

            context.Categories.Add(category);
            context.SaveChanges();

            return category;
        }

        private static CreateEnrolmentDto ValidEnrolmentDto(
            int categoryId)
        {
            return new CreateEnrolmentDto
            {
                CategoryId = categoryId
            };
        }

        private static EventEnrollment CreateTestEnrolment(
            RaceDayContext context,
            int eventId,
            int participantId,
            int categoryId,
            string status = "REGISTERED")
        {
            var enrolment = new EventEnrollment
            {
                EventId = eventId,
                ParticipantId = participantId,
                CategoryId = categoryId,
                RegistrationDate = DateTime.Now,
                Status = status
            };

            context.EventEnrollments.Add(enrolment);
            context.SaveChanges();

            return enrolment;
        }

        [Fact]
        public async Task CreateEnrolment_ValidDetails_CreatesEnrolment()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CreateEnrolment(
                raceEvent.EventId,
                ValidEnrolmentDto(category.CategoryId));

            Assert.IsType<CreatedResult>(result);

            var saved = await context.EventEnrollments.SingleAsync();
            Assert.Equal(raceEvent.EventId, saved.EventId);
            Assert.Equal(participant.UserId, saved.ParticipantId);
            Assert.Equal(category.CategoryId, saved.CategoryId);
            Assert.Equal("REGISTERED", saved.Status);
        }

        [Fact]
        public async Task CreateEnrolment_EventDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CreateEnrolment(
                999, ValidEnrolmentDto(1));

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateEnrolment_PastEvent_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(
                context, organiser.UserId, DateTime.Today.AddDays(-2));
            var category = CreateTestCategory(context, raceEvent.EventId);

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CreateEnrolment(
                raceEvent.EventId,
                ValidEnrolmentDto(category.CategoryId));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.EventEnrollments);
        }

        [Fact]
        public async Task CreateEnrolment_CategoryBelongsToAnotherEvent_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var firstEvent = CreateTestEvent(context, organiser.UserId);
            var secondEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, secondEvent.EventId);

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CreateEnrolment(
                firstEvent.EventId,
                ValidEnrolmentDto(category.CategoryId));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.EventEnrollments);
        }

        [Fact]
        public async Task CreateEnrolment_AlreadyRegistered_ReturnsConflict()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CreateEnrolment(
                raceEvent.EventId,
                ValidEnrolmentDto(category.CategoryId));

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Single(context.EventEnrollments);
        }

        [Fact]
        public async Task CreateEnrolment_CancelledEnrolment_RestoresEnrolment()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId, "CANCELLED");

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CreateEnrolment(
                raceEvent.EventId,
                ValidEnrolmentDto(category.CategoryId));

            Assert.IsType<OkObjectResult>(result);

            var restored = await context.EventEnrollments.SingleAsync();
            Assert.Equal("REGISTERED", restored.Status);
            Assert.Equal(category.CategoryId, restored.CategoryId);
        }

        [Fact]
        public async Task GetMyEnrolments_ReturnsCurrentParticipantsEnrolments()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");
            var otherParticipant = CreateTestUser(
                context, "other@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                otherParticipant.UserId, category.CategoryId);

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.GetMyEnrolments();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var enrolments = Assert.IsAssignableFrom<
                System.Collections.IEnumerable>(okResult.Value);

            Assert.Equal(1, enrolments.Cast<object>().Count());
        }

        [Fact]
        public async Task CancelEnrolment_ExistingEnrolment_CancelsIt()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CancelEnrolment(raceEvent.EventId);

            Assert.IsType<OkObjectResult>(result);

            var enrolment = await context.EventEnrollments.SingleAsync();
            Assert.Equal("CANCELLED", enrolment.Status);
        }

        [Fact]
        public async Task CancelEnrolment_NotEnrolled_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CancelEnrolment(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CancelEnrolment_AlreadyCancelled_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId, "CANCELLED");

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CancelEnrolment(raceEvent.EventId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CancelEnrolment_CompletedEnrolment_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId, "COMPLETED");

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.CancelEnrolment(raceEvent.EventId);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetEventEnrolments_EventDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.GetEventEnrolments(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetEventEnrolments_WrongOrganiser_ReturnsForbid()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var owner = CreateTestUser(
                context, "owner@example.com", "ORGANISER");
            var otherOrganiser = CreateTestUser(
                context, "other@example.com", "ORGANISER");

            var raceEvent = CreateTestEvent(context, owner.UserId);

            var controller = CreateController(
                context, otherOrganiser.UserId, "ORGANISER");

            var result = await controller.GetEventEnrolments(
                raceEvent.EventId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task GetEventEnrolments_Owner_ReturnsEnrolments()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var category = CreateTestCategory(context, raceEvent.EventId);

            CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.GetEventEnrolments(
                raceEvent.EventId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var enrolments = Assert.IsAssignableFrom<
                System.Collections.IEnumerable>(okResult.Value);

            Assert.Single(enrolments.Cast<object>());
        }
    }
}
