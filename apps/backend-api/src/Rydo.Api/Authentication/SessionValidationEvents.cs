using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Authentication;

public sealed class SessionValidationEvents(
    RydoDbContext database,
    TimeProvider timeProvider) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirst("sub")?.Value;
        var sessionClaim = context.Principal?.FindFirst("sid")?.Value;

        if (!Guid.TryParse(subject, out var userId) ||
            !Guid.TryParse(sessionClaim, out var sessionId))
        {
            context.Fail("Required session claims are missing.");
            return;
        }

        var now = timeProvider.GetUtcNow();
        var sessionIsActive = await database.AuthSessions.AnyAsync(
            session => session.Id == sessionId &&
                session.UserId == userId &&
                session.RevokedAt == null &&
                session.ExpiresAt > now &&
                session.User.IsActive,
            context.HttpContext.RequestAborted);

        if (!sessionIsActive)
        {
            context.Fail("The session is no longer active.");
        }
    }
}
