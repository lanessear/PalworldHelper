using System.Text.Json;
using Microsoft.Data.Sqlite;
using PalworldHelper.Models;

namespace PalworldHelper.Data;

public sealed class Database
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Database(IHostEnvironment environment)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PalworldHelper");
        Directory.CreateDirectory(root);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "palworld-helper.db") }.ToString();
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
        PRAGMA journal_mode=WAL;
        CREATE TABLE IF NOT EXISTS server_profiles (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            host TEXT NOT NULL,
            port INTEGER NOT NULL,
            username TEXT NOT NULL,
            remote_save_path TEXT NOT NULL,
            player_name TEXT NOT NULL,
            private_key_path TEXT NULL,
            last_sync_utc TEXT NULL
        );
        CREATE TABLE IF NOT EXISTS owned_pals (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            server_profile_id INTEGER NOT NULL,
            instance_id TEXT NOT NULL,
            species_id TEXT NOT NULL,
            display_name TEXT NOT NULL,
            nickname TEXT NOT NULL,
            level INTEGER NULL,
            gender TEXT NOT NULL,
            rank INTEGER NOT NULL,
            talent_hp INTEGER NULL,
            talent_attack INTEGER NULL,
            talent_defense INTEGER NULL,
            passive_skills_json TEXT NOT NULL,
            UNIQUE(server_profile_id, instance_id)
        );
        CREATE INDEX IF NOT EXISTS ix_owned_pals_profile_species ON owned_pals(server_profile_id, species_id);
        """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ServerProfile>> GetServerProfilesAsync()
    {
        var results = new List<ServerProfile>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,host,port,username,remote_save_path,player_name,private_key_path,last_sync_utc FROM server_profiles ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(ReadProfile(reader));
        return results;
    }

    public async Task<ServerProfile?> GetServerProfileAsync(long id)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id,name,host,port,username,remote_save_path,player_name,private_key_path,last_sync_utc FROM server_profiles WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadProfile(reader) : null;
    }

    public async Task<long> UpsertServerProfileAsync(ServerProfile profile)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        if (profile.Id <= 0)
        {
            command.CommandText = """
            INSERT INTO server_profiles(name,host,port,username,remote_save_path,player_name,private_key_path,last_sync_utc)
            VALUES($name,$host,$port,$username,$path,$player,$key,$sync); SELECT last_insert_rowid();
            """;
        }
        else
        {
            command.CommandText = """
            UPDATE server_profiles SET name=$name,host=$host,port=$port,username=$username,
              remote_save_path=$path,player_name=$player,private_key_path=$key,last_sync_utc=$sync WHERE id=$id;
            SELECT $id;
            """;
            command.Parameters.AddWithValue("$id", profile.Id);
        }
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$host", profile.Host);
        command.Parameters.AddWithValue("$port", profile.Port);
        command.Parameters.AddWithValue("$username", profile.Username);
        command.Parameters.AddWithValue("$path", profile.RemoteSavePath);
        command.Parameters.AddWithValue("$player", profile.PlayerName);
        command.Parameters.AddWithValue("$key", (object?)profile.PrivateKeyPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$sync", profile.LastSyncUtc?.ToString("O") ?? (object)DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task ReplaceOwnedPalsAsync(long profileId, IReadOnlyList<OwnedPal> pals)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM owned_pals WHERE server_profile_id=$id";
        delete.Parameters.AddWithValue("$id", profileId);
        await delete.ExecuteNonQueryAsync();

        foreach (var pal in pals)
        {
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
            INSERT INTO owned_pals(server_profile_id,instance_id,species_id,display_name,nickname,level,gender,rank,talent_hp,talent_attack,talent_defense,passive_skills_json)
            VALUES($profile,$instance,$species,$display,$nickname,$level,$gender,$rank,$hp,$attack,$defense,$passives)
            """;
            insert.Parameters.AddWithValue("$profile", profileId);
            insert.Parameters.AddWithValue("$instance", pal.InstanceId);
            insert.Parameters.AddWithValue("$species", pal.SpeciesId);
            insert.Parameters.AddWithValue("$display", pal.DisplayName);
            insert.Parameters.AddWithValue("$nickname", pal.Nickname);
            insert.Parameters.AddWithValue("$level", pal.Level ?? (object)DBNull.Value);
            insert.Parameters.AddWithValue("$gender", pal.Gender);
            insert.Parameters.AddWithValue("$rank", pal.Rank);
            insert.Parameters.AddWithValue("$hp", pal.TalentHp ?? (object)DBNull.Value);
            insert.Parameters.AddWithValue("$attack", pal.TalentAttack ?? (object)DBNull.Value);
            insert.Parameters.AddWithValue("$defense", pal.TalentDefense ?? (object)DBNull.Value);
            insert.Parameters.AddWithValue("$passives", JsonSerializer.Serialize(pal.PassiveSkills, JsonOptions));
            await insert.ExecuteNonQueryAsync();
        }

        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE server_profiles SET last_sync_utc=$sync WHERE id=$id";
        update.Parameters.AddWithValue("$sync", DateTimeOffset.UtcNow.ToString("O"));
        update.Parameters.AddWithValue("$id", profileId);
        await update.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<OwnedPal>> GetOwnedPalsAsync(long profileId)
    {
        var results = new List<OwnedPal>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id,server_profile_id,instance_id,species_id,display_name,nickname,level,gender,rank,talent_hp,talent_attack,talent_defense,passive_skills_json FROM owned_pals WHERE server_profile_id=$id ORDER BY display_name,nickname";
        command.Parameters.AddWithValue("$id", profileId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new OwnedPal(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6), reader.GetString(7), reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10), reader.IsDBNull(11) ? null : reader.GetInt32(11),
                JsonSerializer.Deserialize<List<string>>(reader.GetString(12), JsonOptions) ?? []));
        }
        return results;
    }

    private static ServerProfile ReadProfile(SqliteDataReader reader) => new(
        reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)));
}
