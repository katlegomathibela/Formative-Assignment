
using System.Security.Claims;
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
    public class EventsControllerTests
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

        private static EventsController CreateController(
            RaceDayContext context,
            int userId = 1,
            string role = "ORGANISER")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(
                claims, "UnitTestAuthentication");

            var controller = new EventsController(context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity)
                    }
                }
            };

            return controller;
        }

        private static async Task<User> CreateTestUser(
            RaceDayContext context,
            string email = "organiser@example.com",
            string role = "ORGANISER")
        {
            var user = new User
            {
                FirstName = "Test",
                LastName = "Organiser",
                Email = email,
                PasswordHash = "test-hash",
                Role = role,
                CreatedAt = DateTime.Now
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private static CreateEventDto ValidEventDto()
        {
            return new CreateEventDto
            {
                Name = "City Fun Run",
                Description = "A test running event",
                EventDate = DateTime.Today.AddDays(30),
                Location = "Johannesburg",
                Distance = 10,
                EventType = "RUN"
            };
        }

        private static async Task<Event> CreateTestEvent(
            RaceDayContext context,
            int organiserId)
        {
            var raceEvent = new Event
            {
                OrganiserId = organiserId,
                Name = "City Fun Run",
                Description = "A test running event",
                EventDate = DateTime.Today.AddDays(30),
                Location = "Johannesburg",
                Distance = 10,
                EventType = "RUN"
            };

            context.Events.Add(raceEvent);
            await context.SaveChangesAsync();
            return raceEvent;
        }

        [Fact]
        public async Task GetAllEvents_NoEvents_ReturnsEmptyList()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var controller = CreateController(context);

            var result = await controller.GetAllEvents();

            var response = Assert.IsType<OkObjectResult>(result);
            var events = Assert.IsAssignableFrom<IEnumerable<Event>>(
                response.Value);

            Assert.Empty(events);
        }

        [Fact]
        public async Task GetAllEvents_WithEvents_ReturnsEvents()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            await CreateTestEvent(context, organiser.UserId);

            var controller = CreateController(context);

            var result = await controller.GetAllEvents();

            var response = Assert.IsType<OkObjectResult>(result);
            var events = Assert.IsAssignableFrom<IEnumerable<Event>>(
                response.Value);

            Assert.Single(events);
        }

        [Fact]
        public async Task GetEventById_ExistingEvent_ReturnsEvent()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            var raceEvent = await CreateTestEvent(context, organiser.UserId);

            var controller = CreateController(context);

            var result = await controller.GetEventById(raceEvent.EventId);

            var response = Assert.IsType<OkObjectResult>(result);
            var returnedEvent = Assert.IsType<Event>(response.Value);

            Assert.Equal(raceEvent.EventId, returnedEvent.EventId);
            Assert.Equal("City Fun Run", returnedEvent.Name);
        }

        [Fact]
        public async Task GetEventById_MissingEvent_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var controller = CreateController(context);

            var result = await controller.GetEventById(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateEvent_ValidDetails_CreatesEvent()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            var controller = CreateController(context, organiser.UserId);

            var result = await controller.CreateEvent(ValidEventDto());

            var response = Assert.IsType<CreatedAtActionResult>(result);
            var createdEvent = Assert.IsType<Event>(response.Value);

            Assert.Equal(organiser.UserId, createdEvent.OrganiserId);
            Assert.Equal("City Fun Run", createdEvent.Name);
            Assert.Equal("RUN", createdEvent.EventType);
            Assert.Single(context.Events);
        }

        [Fact]
        public async Task CreateEvent_InvalidEventType_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            var controller = CreateController(context, organiser.UserId);

            var dto = ValidEventDto();
            dto.EventType = "SWIM";

            var result = await controller.CreateEvent(dto);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.Events);
        }

        [Fact]
        public async Task CreateEvent_ZeroDistance_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            var controller = CreateController(context, organiser.UserId);

            var dto = ValidEventDto();
            dto.Distance = 0;

            var result = await controller.CreateEvent(dto);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.Events);
        }

        [Fact]
        public async Task UpdateEvent_OtherOrganisersEvent_ReturnsForbid()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var owner = await CreateTestUser(context);
            var otherOrganiser = await CreateTestUser(
                context, "other@example.com");

            var raceEvent = await CreateTestEvent(context, owner.UserId);
            var controller = CreateController(
                context, otherOrganiser.UserId);

            var dto = ValidEventDto();
            dto.Name = "Changed Name";

            var result = await controller.UpdateEvent(
                raceEvent.EventId, dto);

            Assert.IsType<ForbidResult>(result);

            var unchangedEvent = await context.Events
                .SingleAsync(e => e.EventId == raceEvent.EventId);

            Assert.Equal("City Fun Run", unchangedEvent.Name);
        }

        [Fact]
        public async Task DeleteEvent_OtherOrganisersEvent_ReturnsForbid()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var owner = await CreateTestUser(context);
            var otherOrganiser = await CreateTestUser(
                context, "other@example.com");

            var raceEvent = await CreateTestEvent(context, owner.UserId);
            var controller = CreateController(
                context, otherOrganiser.UserId);

            var result = await controller.DeleteEvent(raceEvent.EventId);

            Assert.IsType<ForbidResult>(result);
            Assert.Single(context.Events);
        }

        [Fact]
        public async Task DeleteEvent_EventWithCategory_ReturnsConflict()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            var raceEvent = await CreateTestEvent(
                context, organiser.UserId);

            context.Categories.Add(new Category
            {
                EventId = raceEvent.EventId,
                Name = "Open",
                CategoryType = "AGE",
                Value = "18+"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(
                context, organiser.UserId);

            var result = await controller.DeleteEvent(raceEvent.EventId);

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Single(context.Events);
        }

        [Fact]
        public async Task DeleteEvent_EventWithoutDependencies_DeletesEvent()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = await CreateTestUser(context);
            var raceEvent = await CreateTestEvent(
                context, organiser.UserId);

            var controller = CreateController(
                context, organiser.UserId);

            var result = await controller.DeleteEvent(raceEvent.EventId);

            Assert.IsType<OkObjectResult>(result);
            Assert.Empty(context.Events);
        }
    }
}
