using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using OceanFixture.Domain;

namespace OceanFixture.Api.Data;

public sealed record StoredState(List<Company> Companies, List<CargoOffer> Offers);

public interface IStatePersistence
{
    StoredState? Load();
    void Save(StoredState state);
}

internal static class StateCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        IgnoreReadOnlyProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(StoredState state) => JsonSerializer.Serialize(state, Options);
    public static StoredState Deserialize(string json)
    {
        var state = JsonSerializer.Deserialize<StoredState>(json, Options);
        if (state?.Companies is null || state.Offers is null)
            throw new InvalidDataException("Stored application state is invalid. Restore a backup; existing data was not replaced.");
        if (state.Companies.Any(company => company is null || company.Id <= 0)
            || state.Companies.Select(company => company.Id).Distinct().Count() != state.Companies.Count
            || state.Offers.Any(offer => offer is null || offer.Id == Guid.Empty || offer.Reference <= 0
                || !Enum.IsDefined(offer.Stage) || offer.Terms is null || offer.Bids is null
                || offer.InvitedShipownerIds is null || offer.ChartererClauses is null
                || offer.ShipownerClauses is null || offer.Confirmations is null || offer.NegotiationHistory is null
                || offer.NegotiationDrafts is null)
            || state.Offers.Select(offer => offer.Id).Distinct().Count() != state.Offers.Count
            || state.Offers.Select(offer => offer.Reference).Distinct().Count() != state.Offers.Count)
            throw new InvalidDataException("Stored application state contains invalid or duplicate entities. Existing data was not replaced.");
        return state;
    }
    public static StoredState Clone(StoredState state) => Deserialize(Serialize(state));
}

// Local, single-API persistence. An exclusive lease prevents two cached instances
// from overwriting one another. A normalized multi-instance repository can replace
// this adapter without changing the public offer contracts.
public sealed class SqliteStatePersistence : IStatePersistence, IDisposable
{
    private readonly string connectionString;
    private readonly FileStream lease;
    private bool disposed;

    public SqliteStatePersistence(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        lease = new FileStream(fullPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
        try
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS ApplicationState (Id INTEGER PRIMARY KEY CHECK (Id = 1), SchemaVersion INTEGER NOT NULL, Payload TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }
        catch { lease.Dispose(); throw; }
    }

    public StoredState? Load()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SchemaVersion, Payload FROM ApplicationState WHERE Id = 1";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        if (reader.GetInt32(0) != 1)
            throw new InvalidDataException("Unsupported database schema. Existing data was not replaced.");
        return StateCodec.Deserialize(reader.GetString(1));
    }

    public void Save(StoredState state)
    {
        var payload = StateCodec.Serialize(state);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO ApplicationState (Id, SchemaVersion, Payload) VALUES (1, 1, $payload) ON CONFLICT(Id) DO UPDATE SET Payload = excluded.Payload";
        command.Parameters.AddWithValue("$payload", payload);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var connection = new SqliteConnection(connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    public void Dispose() { disposed = true; lease.Dispose(); }
}

public sealed class PersistenceWriteException(Exception innerException)
    : Exception("The change could not be saved. No success was recorded.", innerException);
