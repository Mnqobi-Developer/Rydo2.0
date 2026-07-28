using Rydo.Domain.Identity;

namespace Rydo.Api.Hubs;

public static class RealtimeGroups
{
    public static string User(Guid userId)
    {
        return $"user:{userId:N}";
    }

    public static string Role(UserRole role)
    {
        return $"role:{role.ToString().ToLowerInvariant()}";
    }
}
