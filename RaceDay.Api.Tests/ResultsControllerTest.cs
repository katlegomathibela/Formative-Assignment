
using System;
using System.Collections;
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
    public class ResultsControllerTests
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

        private static ResultsController CreateController(
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

            var controller = new ResultsController(context);

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
            int organiserId)
        {
            var raceEvent = new Event
            {
                OrganiserId = organiserId,
                Name = "City Fun Run",
                Description = "A test running event",
                EventDate = DateTime.Today.AddDays(30),
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

        private static CreateResultDto ValidResultDto(int enrollmentId)
        {
            return new CreateResultDto
            {
                EnrollmentId = enrollmentId,
                FinishTime = new TimeSpan(0, 45, 30),
                FinishPosition = 1
            };
        }

        [Fact]
        public async Task CreateResult_ValidDetails_CreatesResultAndCompletesEnrolment()
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

            var enrolment = CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                raceEvent.EventId,
                ValidResultDto(enrolment.EnrollmentId));

            Assert.IsType<CreatedResult>(result);

            var savedResult = await context.Results.SingleAsync();
            Assert.Equal(enrolment.EnrollmentId, savedResult.EnrollmentId);
            Assert.Equal(new TimeSpan(0, 45, 30), savedResult.FinishTime);
            Assert.Equal(1, savedResult.FinishPosition);

            var savedEnrolment = await context.EventEnrollments
                .FindAsync(enrolment.EnrollmentId);

            Assert.Equal("COMPLETED", savedEnrolment!.Status);
        }

        [Fact]
        public async Task CreateResult_EventDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                999, ValidResultDto(1));

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateResult_WrongOrganiser_ReturnsForbid()
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

            var result = await controller.CreateResult(
                raceEvent.EventId, ValidResultDto(1));

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(context.Results);
        }

        [Fact]
        public async Task CreateResult_InvalidFinishPosition_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var dto = ValidResultDto(1);
            dto.FinishPosition = 0;

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                raceEvent.EventId, dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateResult_InvalidFinishTime_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(
                context, "organiser@example.com", "ORGANISER");

            var raceEvent = CreateTestEvent(context, organiser.UserId);
            var dto = ValidResultDto(1);
            dto.FinishTime = TimeSpan.FromDays(1);

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                raceEvent.EventId, dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateResult_EnrolmentDoesNotBelongToEvent_ReturnsNotFound()
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

            var enrolment = CreateTestEnrolment(
                context, secondEvent.EventId,
                participant.UserId, category.CategoryId);

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                firstEvent.EventId,
                ValidResultDto(enrolment.EnrollmentId));

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateResult_CancelledEnrolment_ReturnsBadRequest()
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

            var enrolment = CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId, "CANCELLED");

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                raceEvent.EventId,
                ValidResultDto(enrolment.EnrollmentId));

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.Results);
        }

        [Fact]
        public async Task CreateResult_DuplicateResult_ReturnsConflict()
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

            var enrolment = CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            context.Results.Add(new Result
            {
                EnrollmentId = enrolment.EnrollmentId,
                FinishTime = new TimeSpan(0, 50, 0),
                FinishPosition = 2
            });
            await context.SaveChangesAsync();

            var controller = CreateController(
                context, organiser.UserId, "ORGANISER");

            var result = await controller.CreateResult(
                raceEvent.EventId,
                ValidResultDto(enrolment.EnrollmentId));

            Assert.IsType<ConflictObjectResult>(result);
            Assert.Single(context.Results);
        }

        [Fact]
        public async Task GetMyResults_ReturnsOnlyCurrentParticipantsResults()
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

            var firstEnrolment = CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId);

            var secondEnrolment = CreateTestEnrolment(
                context, raceEvent.EventId,
                otherParticipant.UserId, category.CategoryId);

            context.Results.AddRange(
                new Result
                {
                    EnrollmentId = firstEnrolment.EnrollmentId,
                    FinishTime = new TimeSpan(0, 45, 0),
                    FinishPosition = 1
                },
                new Result
                {
                    EnrollmentId = secondEnrolment.EnrollmentId,
                    FinishTime = new TimeSpan(0, 50, 0),
                    FinishPosition = 2
                });

            await context.SaveChangesAsync();

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.GetMyResults();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var results = Assert.IsAssignableFrom<IEnumerable>(okResult.Value);

            Assert.Single(results.Cast<object>());
        }

        [Fact]
        public async Task GetEventResults_EventDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.GetEventResults(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetEventResults_EventExists_ReturnsEventDetailsAndResults()
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

            var enrolment = CreateTestEnrolment(
                context, raceEvent.EventId,
                participant.UserId, category.CategoryId, "COMPLETED");

            context.Results.Add(new Result
            {
                EnrollmentId = enrolment.EnrollmentId,
                FinishTime = new TimeSpan(0, 45, 0),
                FinishPosition = 1
            });
            await context.SaveChangesAsync();

            var controller = CreateController(
                context, participant.UserId, "PARTICIPANT");

            var result = await controller.GetEventResults(raceEvent.EventId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var resultProperty = okResult.Value.GetType()
                .GetProperty("results");

            Assert.NotNull(resultProperty);

            var results = Assert.IsAssignableFrom<IEnumerable>(
                resultProperty!.GetValue(okResult.Value));

            Assert.Single(results.Cast<object>());
        }
    }
}
