
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
    public class UsersControllerTests
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

        private static UsersController CreateController(int userId)
        {
            var controller = new UsersController(null!);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "participant@example.com"),
                new Claim(ClaimTypes.Role, "PARTICIPANT")
            };

            var identity = new ClaimsIdentity(
                claims, "UnitTestAuthentication");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return controller;
        }

        private static UsersController CreateController(
            RaceDayContext context, int userId)
        {
            var controller = new UsersController(context);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "participant@example.com"),
                new Claim(ClaimTypes.Role, "PARTICIPANT")
            };

            var identity = new ClaimsIdentity(
                claims, "UnitTestAuthentication");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return controller;
        }

        private static async Task<User> CreateTestUser(
            RaceDayContext context,
            string email = "participant@example.com",
            string role = "PARTICIPANT")
        {
            var user = new User
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = email,
                PasswordHash = "test-hash",
                Role = role,
                CreatedAt = DateTime.Now
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            return user;
        }

        [Fact]
        public async Task GetMyProfile_ExistingUser_ReturnsProfile()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var user = await CreateTestUser(context);
            var controller = CreateController(context, user.UserId);

            var result = await controller.GetMyProfile();

            var response = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(response.Value);

            var profile = response.Value!;
            var type = profile.GetType();

            Assert.Equal(
                user.UserId,
                type.GetProperty("UserId")!.GetValue(profile));

            Assert.Equal(
                "Test",
                type.GetProperty("FirstName")!.GetValue(profile));

            Assert.Equal(
                "Participant",
                type.GetProperty("LastName")!.GetValue(profile));

            Assert.Equal(
                "participant@example.com",
                type.GetProperty("Email")!.GetValue(profile));

            Assert.Equal(
                "PARTICIPANT",
                type.GetProperty("Role")!.GetValue(profile));
        }

        [Fact]
        public async Task GetMyProfile_MissingUser_ReturnsNotFound()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var controller = CreateController(context, 999);

            var result = await controller.GetMyProfile();

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task UpdateMyProfile_ValidDetails_UpdatesProfile()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var user = await CreateTestUser(context);
            var controller = CreateController(context, user.UserId);

            var dto = new UpdateProfileDto
            {
                FirstName = "Updated",
                LastName = "Runner",
                Email = "updated@example.com"
            };

            var result = await controller.UpdateMyProfile(dto);

            Assert.IsType<OkObjectResult>(result);

            var updatedUser = await context.Users
                .SingleAsync(u => u.UserId == user.UserId);

            Assert.Equal("Updated", updatedUser.FirstName);
            Assert.Equal("Runner", updatedUser.LastName);
            Assert.Equal("updated@example.com", updatedUser.Email);

            // Updating the profile must not change the user's role.
            Assert.Equal("PARTICIPANT", updatedUser.Role);
        }

        [Fact]
        public async Task UpdateMyProfile_EmailAlreadyInUse_ReturnsConflict()
        {
            using var connection =
                new SqliteConnection("Data Source=:memory:");

            await connection.OpenAsync();
            using var context = CreateContext(connection);

            var user = await CreateTestUser(
                context, "participant@example.com");

            await CreateTestUser(
                context, "another@example.com");

            var controller = CreateController(context, user.UserId);

            var dto = new UpdateProfileDto
            {
                FirstName = "Test",
                LastName = "Participant",
                Email = "another@example.com"
            };

            var result = await controller.UpdateMyProfile(dto);

            Assert.IsType<ConflictObjectResult>(result);

            var unchangedUser = await context.Users
                .SingleAsync(u => u.UserId == user.UserId);

            Assert.Equal(
                "participant@example.com",
                unchangedUser.Email);
        }
    }
}
