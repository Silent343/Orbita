using DuckDB.NET.Data;
using Orbita.Files.Application;
using Orbita.Files.Domain;
using Orbita.SharedKernel;

namespace Orbita.Files.Infrastructure;

public sealed class TableConversionHandler : IFileActionHandler
{
    public bool CanHandle(string id) => id is "table.csv" or "table.json" or "table.parquet";

    public async Task<ActionResult> ExecuteAsync(ActionRequest request, CancellationToken token)
    {
        using var work = new ConversionWork();
        var source = await work.StageAsync(request.File.Path, "source" + request.File.Extension, token);
        var (extension, options) = request.ActionId switch
        {
            "table.csv" => (".csv", "FORMAT CSV, HEADER true"),
            "table.json" => (".json", "FORMAT JSON, ARRAY true"),
            "table.parquet" => (".parquet", "FORMAT PARQUET"),
            _ => throw new DomainException("ErrorTableConversion")
        };
        var output = work.PathFor("result" + extension);
        await Task.Run(() => Convert(request.File.Extension, work.DirectoryPath, source, output, options, token), token);
        return await work.PublishAsync(request, [work.RequireOutput(output, "ErrorTableConversion")], token);
    }

    private static void Convert(string extension, string directory, string source, string output, string options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var database = new DuckDBConnection("Data Source=:memory:");
            database.Open();
            using var command = database.CreateCommand();
            var allowed = SqlString(directory);
            command.CommandText = $"""
                SET allowed_directories = [{allowed}];
                SET autoinstall_known_extensions = false;
                SET autoload_known_extensions = false;
                SET memory_limit = '384MB';
                SET threads = 2;
                SET temp_directory = {allowed};
                SET max_temp_directory_size = '1GB';
                SET enable_external_access = false;
                """;
            command.ExecuteNonQuery();
            token.ThrowIfCancellationRequested();
            var input = SqlString(source);
            var reader = extension switch
            {
                ".csv" => $"read_csv({input}, header = true, all_varchar = true)",
                ".json" => $"read_json_auto({input})",
                ".parquet" => $"read_parquet({input})",
                _ => throw new DomainException("ErrorTableConversion")
            };
            command.CommandText = $"COPY (SELECT * FROM {reader}) TO {SqlString(output)} ({options})";
            // DuckDB's interrupt flag is scoped to this private connection.
            using var interrupt = token.Register(command.Cancel);
            command.ExecuteNonQuery();
            token.ThrowIfCancellationRequested();
        }
        catch (DuckDBException)
        {
            token.ThrowIfCancellationRequested();
            throw new DomainException("ErrorTableConversion");
        }
    }

    private static string SqlString(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
