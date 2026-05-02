using Microsoft.AspNetCore.Authorization;

namespace SoccerCheckin.Web.Services;

public class AdminRequirement : IAuthorizationRequirement { }

public class AdminAuthorizationHandler(ICurrentUserService currentUser) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (await currentUser.IsAdminAsync())
        {
            context.Succeed(requirement);
        }
    }
}
