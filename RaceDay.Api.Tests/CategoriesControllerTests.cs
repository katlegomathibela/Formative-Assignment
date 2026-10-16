
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using Xunit;

namespace RaceDay.Api.Tests
{
    public class CategoriesControllerTests
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

        private static CategoriesController CreateController(
            RaceDayContext context,
            int userId = 1,
            string role = "ORGANISER")
        {
            var controller = new CategoriesController(context);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "test@example.com"),
                new Claim(ClaimTypes.Role, role)
            };

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
            string email = "organiser@example.com",
            string role = "ORGANISER")
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

        private static CreateCategoryDto ValidCategoryDto()
        {
            return new CreateCategoryDto
            {
                Name = "10 km Adults",
                CategoryType = "DISTANCE",
                Value = "10 km"
            };
        }

        private static Category CreateTestCategory(
            RaceDayContext context,
            int eventId,
            string name = "10 km Adults")
        {
            var category = new Category
            {
                EventId = eventId,
                Name = name,
                CategoryType = "DISTANCE",
                Value = "10 km"
            };

            context.Categories.Add(category);
            context.SaveChanges();

            return category;
        }

        [Fact]
        public async Task GetCategories_EventDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var controller = CreateController(context);

            var result = await controller.GetCategories(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task GetCategories_EventExists_ReturnsCategories()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var raceEvent = CreateTestEvent(context, user.UserId);
            CreateTestCategory(context, raceEvent.EventId);

            var controller = CreateController(context);

            var result = await controller.GetCategories(raceEvent.EventId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var categories = Assert.IsAssignableFrom<
                System.Collections.Generic.IEnumerable<Category>>(
                okResult.Value);

            Assert.Single(categories);
        }

        [Fact]
        public async Task CreateCategory_ValidDetails_CreatesCategory()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var raceEvent = CreateTestEvent(context, user.UserId);
            var controller = CreateController(context, user.UserId);

            var result = await controller.CreateCategory(
                raceEvent.EventId, ValidCategoryDto());

            Assert.IsType<CreatedResult>(result);

            var category = await context.Categories.SingleAsync();

            Assert.Equal("10 km Adults", category.Name);
            Assert.Equal("DISTANCE", category.CategoryType);
            Assert.Equal(user.UserId, raceEvent.OrganiserId);
        }

        [Fact]
        public async Task CreateCategory_EventDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var controller = CreateController(context, user.UserId);

            var result = await controller.CreateCategory(
                999, ValidCategoryDto());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateCategory_WrongOrganiser_ReturnsForbid()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var owner = CreateTestUser(context);
            var otherUser = CreateTestUser(
                context, "other@example.com");

            var raceEvent = CreateTestEvent(context, owner.UserId);
            var controller = CreateController(
                context, otherUser.UserId);

            var result = await controller.CreateCategory(
                raceEvent.EventId, ValidCategoryDto());

            Assert.IsType<ForbidResult>(result);
            Assert.Empty(context.Categories);
        }

        [Fact]
        public async Task CreateCategory_InvalidCategoryType_ReturnsBadRequest()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var raceEvent = CreateTestEvent(context, user.UserId);
            var dto = ValidCategoryDto();
            dto.CategoryType = "INVALID";

            var controller = CreateController(context, user.UserId);

            var result = await controller.CreateCategory(
                raceEvent.EventId, dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreateCategory_DuplicateName_ReturnsConflict()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var raceEvent = CreateTestEvent(context, user.UserId);
            CreateTestCategory(context, raceEvent.EventId);

            var controller = CreateController(context, user.UserId);

            var result = await controller.CreateCategory(
                raceEvent.EventId, ValidCategoryDto());

            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task UpdateCategory_ValidDetails_UpdatesCategory()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var raceEvent = CreateTestEvent(context, user.UserId);
            var category = CreateTestCategory(
                context, raceEvent.EventId);

            var dto = ValidCategoryDto();
            dto.Name = "Adult Runners";
            dto.CategoryType = "AGE";
            dto.Value = "18-40";

            var controller = CreateController(context, user.UserId);

            var result = await controller.UpdateCategory(
                category.CategoryId, dto);

            Assert.IsType<OkObjectResult>(result);

            var updated = await context.Categories
                .FindAsync(category.CategoryId);

            Assert.Equal("Adult Runners", updated!.Name);
            Assert.Equal("AGE", updated.CategoryType);
            Assert.Equal("18-40", updated.Value);
        }

        [Fact]
        public async Task UpdateCategory_CategoryDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var controller = CreateController(context, user.UserId);

            var result = await controller.UpdateCategory(
                999, ValidCategoryDto());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task UpdateCategory_WrongOrganiser_ReturnsForbid()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var owner = CreateTestUser(context);
            var otherUser = CreateTestUser(
                context, "other@example.com");

            var raceEvent = CreateTestEvent(context, owner.UserId);
            var category = CreateTestCategory(
                context, raceEvent.EventId);

            var dto = ValidCategoryDto();
            dto.Name = "Changed Category";

            var controller = CreateController(
                context, otherUser.UserId);

            var result = await controller.UpdateCategory(
                category.CategoryId, dto);

            Assert.IsType<ForbidResult>(result);

            var unchanged = await context.Categories
                .FindAsync(category.CategoryId);

            Assert.Equal("10 km Adults", unchanged!.Name);
        }

        [Fact]
        public async Task DeleteCategory_CategoryDoesNotExist_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var controller = CreateController(context, user.UserId);

            var result = await controller.DeleteCategory(999);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteCategory_WrongOrganiser_ReturnsForbid()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var owner = CreateTestUser(context);
            var otherUser = CreateTestUser(
                context, "other@example.com");

            var raceEvent = CreateTestEvent(context, owner.UserId);
            var category = CreateTestCategory(
                context, raceEvent.EventId);

            var controller = CreateController(
                context, otherUser.UserId);

            var result = await controller.DeleteCategory(
                category.CategoryId);

            Assert.IsType<ForbidResult>(result);

            Assert.NotNull(await context.Categories
                .FindAsync(category.CategoryId));
        }

        [Fact]
        public async Task DeleteCategory_CategoryInUse_ReturnsConflict()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var organiser = CreateTestUser(context);
            var participant = CreateTestUser(
                context, "participant@example.com", "PARTICIPANT");

            var raceEvent = CreateTestEvent(
                context, organiser.UserId);
            var category = CreateTestCategory(
                context, raceEvent.EventId);

            context.EventEnrollments.Add(new EventEnrollment
            {
                EventId = raceEvent.EventId,
                ParticipantId = participant.UserId,
                CategoryId = category.CategoryId,
                RegistrationDate = DateTime.Now,
                Status = "REGISTERED"
            });

            await context.SaveChangesAsync();

            var controller = CreateController(
                context, organiser.UserId);

            var result = await controller.DeleteCategory(
                category.CategoryId);

            Assert.IsType<ConflictObjectResult>(result);
            Assert.NotNull(await context.Categories
                .FindAsync(category.CategoryId));
        }

        [Fact]
        public async Task DeleteCategory_CategoryNotInUse_DeletesCategory()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            using var context = CreateContext(connection);
            var user = CreateTestUser(context);
            var raceEvent = CreateTestEvent(context, user.UserId);
            var category = CreateTestCategory(
                context, raceEvent.EventId);

            var controller = CreateController(context, user.UserId);

            var result = await controller.DeleteCategory(
                category.CategoryId);

            Assert.IsType<OkObjectResult>(result);
            Assert.Null(await context.Categories
                .FindAsync(category.CategoryId));
        }
    }
}
