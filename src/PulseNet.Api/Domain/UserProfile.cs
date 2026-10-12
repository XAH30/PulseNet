namespace PulseNet.Api.Domain;

public sealed record UserProfile(
    UserId Id,
    string FirstName,
    string SecondName,
    DateOnly Birthdate,
    string Gender,
    string Biography,
    string City,
    DateTimeOffset CreatedAt);

public sealed record UserCredentials(
    UserId Id,
    string PasswordHash);
