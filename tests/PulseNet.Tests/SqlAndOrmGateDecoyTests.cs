using PulseNet.Api.Security;
using Xunit;

namespace PulseNet.Tests;

/// <summary>
/// Набор приманок, нейтральных контролей и мутантов для статического гейта SqlAndOrmArchitectureGate.
/// </summary>
public sealed class SqlAndOrmGateDecoyTests
{
    [Fact]
    public void ProductionSourceTree_PassesArchitectureAndSqlGate()
    {
        var repoSrc = FindProductionSrcDirectory();
        var result = SqlAndOrmArchitectureGate.ScanDirectory(repoSrc);

        Assert.True(
            result.IsPassed,
            $"Гейт обнаружил нарушения в боевом коде: {string.Join("; ", result.Violations.Select(v => $"{v.RuleId}@{v.FilePath}:{v.LineNumber}"))}");
        Assert.True(result.ScannedFilesCount >= 5);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Gate_IsFailClosed_OnMissingDirectoryOrEmptySources()
    {
        var missingDirResult = SqlAndOrmArchitectureGate.ScanDirectory("/nonexistent/fakepii/path/900000001");
        Assert.False(missingDirResult.IsPassed);
        Assert.Contains(missingDirResult.Violations, v => v.RuleId == "FAIL_CLOSED_DIR_NOT_FOUND");

        var emptySourcesResult = SqlAndOrmArchitectureGate.ScanSources([]);
        Assert.False(emptySourcesResult.IsPassed);
        Assert.Contains(emptySourcesResult.Violations, v => v.RuleId == "FAIL_CLOSED_EMPTY_SOURCES");

        var blankFileResult = SqlAndOrmArchitectureGate.ScanSources([("Blank.cs", "   \n  ")]);
        Assert.False(blankFileResult.IsPassed);
        Assert.Contains(blankFileResult.Violations, v => v.RuleId == "FAIL_CLOSED_EMPTY_FILE");
    }

    [Theory]
    [InlineData("using Microsoft.EntityFrameworkCore;\npublic class C {}")]
    [InlineData("using dApPeR;\npublic class C {}")]
    [InlineData("public class AppCtx : DbContext {}")]
    [InlineData("public class Repo { void M() => conn.QueryAsync<User>(\"SELECT 1\"); }")]
    [InlineData("using NHibernate;\npublic class C {}")]
    public void Gate_CatchesPositiveDecoys_ForbiddenOrmVariants_AndMutantProvesRuleSensitivity(string decoyCode)
    {
        var activeResult = SqlAndOrmArchitectureGate.ScanSources([("DecoyOrm.cs", decoyCode)]);
        Assert.False(activeResult.IsPassed);
        Assert.Contains(activeResult.Violations, v => v.RuleId == "FORBIDDEN_ORM");

        // Мутант правила: отключение CheckOrmUsage доказывает, что именно это правило блокировало приманку
        var mutantResult = SqlAndOrmArchitectureGate.ScanSources(
            [("DecoyOrm.cs", decoyCode)],
            new GateRuleOptions(CheckOrmUsage: false));
        Assert.True(mutantResult.IsPassed);
    }

    [Theory]
    [InlineData("var sql = $\"SELECT * FROM users WHERE id = '{userId}'\";")]
    [InlineData("var sql = $@\"select id from users where city = '{city}'\";")]
    [InlineData("var sql = $raw\"\"\"\nINSERT INTO users (id, first_name) VALUES ('{id}', '{name}')\n\"\"\";")]
    [InlineData("var sql = string.Format(\"SELECT * FROM users WHERE id = '{0}'\", userId);")]
    [InlineData("var sql = string.Concat(\"DELETE FROM users WHERE id = '\", userId, \"'\");")]
    public void Gate_CatchesPositiveDecoys_InterpolatedAndFormattedSql_AndMutantProvesSensitivity(string decoyCode)
    {
        // Убираем слово raw в 3-м примере если нужно, проверим точную интерполяцию C# 11
        var normalizedCode = decoyCode.Replace("$raw\"\"\"", "$\"\"\"", StringComparison.Ordinal);

        var activeResult = SqlAndOrmArchitectureGate.ScanSources([("DecoySqliInterp.cs", normalizedCode)]);
        Assert.False(activeResult.IsPassed);
        Assert.Contains(
            activeResult.Violations,
            v => v.RuleId is "SQLI_INTERPOLATION" or "SQLI_STRING_FORMAT");

        // Мутант правила: отключение CheckSqlInterpolation
        var mutantResult = SqlAndOrmArchitectureGate.ScanSources(
            [("DecoySqliInterp.cs", normalizedCode)],
            new GateRuleOptions(
                CheckSqlInterpolation: false,
                CheckSqlConcatenation: false,
                CheckRawCommandCreation: false));
        Assert.True(mutantResult.IsPassed);
    }

    [Theory]
    [InlineData("var sql = \"SELECT id, first_name FROM users WHERE id = '\" + rawId + \"'\";")]
    [InlineData("var cmd = dataSource.CreateCommand(\"SELECT * FROM users WHERE id = \" + rawId);")]
    public void Gate_CatchesPositiveDecoys_ConcatenatedSql(string decoyCode)
    {
        var activeResult = SqlAndOrmArchitectureGate.ScanSources([("DecoyConcat.cs", decoyCode)]);
        Assert.False(activeResult.IsPassed);
        Assert.Contains(
            activeResult.Violations,
            v => v.RuleId is "SQLI_CONCATENATION" or "SQLI_DYNAMIC_COMMAND");
    }

    [Fact]
    public void Gate_AllowsNeutralControls_ParameterizedSqlAndNormalLogInterpolation()
    {
        const string safeCode = """
            using Npgsql;
            using NpgsqlTypes;

            namespace PulseNet.SafeControl;

            public sealed class SafeRepo
            {
                private const string SelectByIdSql = "SELECT id, first_name FROM users WHERE id = @id LIMIT 1;";

                public NpgsqlCommand BuildCommand(NpgsqlDataSource ds, Guid userId)
                {
                    var logMsg = $"Fetching profile for request_id={userId:D}";
                    _ = logMsg;
                    var cmd = ds.CreateCommand(SelectByIdSql);
                    cmd.Parameters.Add(new NpgsqlParameter<Guid>("id", NpgsqlDbType.Uuid) { TypedValue = userId });
                    return cmd;
                }
            }
            """;

        var result = SqlAndOrmArchitectureGate.ScanSources([("SafeRepo.cs", safeCode)]);
        Assert.True(result.IsPassed);
        Assert.Empty(result.Violations);
    }

    private static string FindProductionSrcDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "PulseNet.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Не удалось найти каталог src/PulseNet.Api.");
    }
}
