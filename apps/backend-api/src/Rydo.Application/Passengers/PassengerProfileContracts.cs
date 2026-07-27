namespace Rydo.Application.Passengers;

public sealed record PassengerProfileResult(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public interface IPassengerProfileService
{
    Task<PassengerProfileResult?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<PassengerProfileResult?> UpsertAsync(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken);
}
