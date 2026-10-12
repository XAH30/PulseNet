using PulseNet.Api.Domain;

namespace PulseNet.Api.Data;

public interface IUserRepository
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(UserProfile profile, string passwordHash, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);
    Task<UserCredentials?> GetCredentialsByIdAsync(UserId id, CancellationToken cancellationToken = default);
}
