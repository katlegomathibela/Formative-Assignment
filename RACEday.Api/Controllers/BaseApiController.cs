
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Controllers
{
    public abstract class BaseApiController : ControllerBase
    {
        protected int? CurrentUserId
        {
            get
            {
                var userId = User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

                if (int.TryParse(userId, out var id))
                {
                    return id;
                }

                return null;
            }
        }
    }
}
