using System.Text.Json.Serialization;
using PulseNet.Api.Domain;

namespace PulseNet.Api.Contracts;

public sealed record LoginRequest(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("password")] string? Password);

public sealed record LoginResponse(
    [property: JsonPropertyName("token")] string Token);

public sealed record RegisterUserRequest(
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("second_name")] string? SecondName,
    [property: JsonPropertyName("birthdate")] string? Birthdate,
    [property: JsonPropertyName("biography")] string? Biography,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("password")] string? Password,
    [property: JsonPropertyName("gender")] string? Gender = null);

public sealed record RegisterUserResponse(
    [property: JsonPropertyName("user_id")] string UserId);

public sealed record UserDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("second_name")] string SecondName,
    [property: JsonPropertyName("birthdate")] string Birthdate,
    [property: JsonPropertyName("gender")] string Gender,
    [property: JsonPropertyName("biography")] string Biography,
    [property: JsonPropertyName("city")] string City)
{
    public static UserDto FromDomain(UserProfile profile) => new(
        Id: profile.Id.ToString(),
        FirstName: profile.FirstName,
        SecondName: profile.SecondName,
        Birthdate: profile.Birthdate.ToString("yyyy-MM-dd"),
        Gender: profile.Gender,
        Biography: profile.Biography,
        City: profile.City);
}

public sealed record ErrorResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("code")] int Code);
