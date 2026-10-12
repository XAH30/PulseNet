using Npgsql;
using NpgsqlTypes;
using PulseNet.Api.Domain;

namespace PulseNet.Api.Data;

/// <summary>
/// Репозиторий пользователей поверх чистого ADO.NET (NpgsqlDataSource + NpgsqlCommand) без ORM.
/// Все SQL-запросы являются константами времени компиляции и используют строго типизированные параметры NpgsqlParameter.
/// </summary>
public sealed class PostgresUserRepository : IUserRepository
{
    private const string EnsureSchemaSql = """
        CREATE TABLE IF NOT EXISTS users (
            id            UUID PRIMARY KEY,
            first_name    VARCHAR(100) NOT NULL CHECK (char_length(btrim(first_name)) >= 1),
            second_name   VARCHAR(100) NOT NULL CHECK (char_length(btrim(second_name)) >= 1),
            birthdate     DATE NOT NULL CHECK (birthdate >= DATE '1900-01-01' AND birthdate <= CURRENT_DATE),
            gender        VARCHAR(16) NOT NULL DEFAULT 'unspecified' CHECK (gender IN ('male', 'female', 'unspecified')),
            biography     TEXT NOT NULL CHECK (char_length(btrim(biography)) >= 1 AND char_length(biography) <= 2000),
            city          VARCHAR(100) NOT NULL CHECK (char_length(btrim(city)) >= 1),
            password_hash VARCHAR(256) NOT NULL CHECK (password_hash LIKE 'pbkdf2_sha256$%'),
            created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """;

    private const string InsertUserSql = """
        INSERT INTO users (id, first_name, second_name, birthdate, gender, biography, city, password_hash, created_at)
        VALUES (@id, @first_name, @second_name, @birthdate, @gender, @biography, @city, @password_hash, @created_at);
        """;

    private const string SelectUserByIdSql = """
        SELECT id, first_name, second_name, birthdate, gender, biography, city, created_at
        FROM users
        WHERE id = @id
        LIMIT 1;
        """;

    private const string SelectCredentialsByIdSql = """
        SELECT id, password_hash
        FROM users
        WHERE id = @id
        LIMIT 1;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresUserRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(EnsureSchemaSql);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateAsync(
        UserProfile profile,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        await using var cmd = _dataSource.CreateCommand(InsertUserSql);
        cmd.Parameters.Add(new NpgsqlParameter<Guid>("id", NpgsqlDbType.Uuid) { TypedValue = profile.Id.Value });
        cmd.Parameters.Add(new NpgsqlParameter<string>("first_name", NpgsqlDbType.Varchar) { TypedValue = profile.FirstName });
        cmd.Parameters.Add(new NpgsqlParameter<string>("second_name", NpgsqlDbType.Varchar) { TypedValue = profile.SecondName });
        cmd.Parameters.Add(new NpgsqlParameter<DateOnly>("birthdate", NpgsqlDbType.Date) { TypedValue = profile.Birthdate });
        cmd.Parameters.Add(new NpgsqlParameter<string>("gender", NpgsqlDbType.Varchar) { TypedValue = profile.Gender });
        cmd.Parameters.Add(new NpgsqlParameter<string>("biography", NpgsqlDbType.Text) { TypedValue = profile.Biography });
        cmd.Parameters.Add(new NpgsqlParameter<string>("city", NpgsqlDbType.Varchar) { TypedValue = profile.City });
        cmd.Parameters.Add(new NpgsqlParameter<string>("password_hash", NpgsqlDbType.Varchar) { TypedValue = passwordHash });
        cmd.Parameters.Add(new NpgsqlParameter<DateTimeOffset>("created_at", NpgsqlDbType.TimestampTz) { TypedValue = profile.CreatedAt });

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<UserProfile?> GetByIdAsync(
        UserId id,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(SelectUserByIdSql);
        cmd.Parameters.Add(new NpgsqlParameter<Guid>("id", NpgsqlDbType.Uuid) { TypedValue = id.Value });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UserProfile(
            Id: new UserId(reader.GetGuid(0)),
            FirstName: reader.GetString(1),
            SecondName: reader.GetString(2),
            Birthdate: reader.GetFieldValue<DateOnly>(3),
            Gender: reader.GetString(4),
            Biography: reader.GetString(5),
            City: reader.GetString(6),
            CreatedAt: reader.GetFieldValue<DateTimeOffset>(7));
    }

    public async Task<UserCredentials?> GetCredentialsByIdAsync(
        UserId id,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(SelectCredentialsByIdSql);
        cmd.Parameters.Add(new NpgsqlParameter<Guid>("id", NpgsqlDbType.Uuid) { TypedValue = id.Value });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UserCredentials(
            Id: new UserId(reader.GetGuid(0)),
            PasswordHash: reader.GetString(1));
    }
}
