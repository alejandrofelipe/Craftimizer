using Craftimizer.Plugin;
using Craftimizer.Simulator.Actions;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace Craftimizer.Utils;

/// <summary>
/// Manages persistent storage of macros in a local SQLite database.
/// The database is created automatically at <c>%APPDATA%\XIVLauncher\pluginConfigs\Craftimizer\macros.db</c>
/// if it does not exist. On first run, any macros stored in the legacy JSON config are migrated here.
/// </summary>
public sealed class MacroRepository : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly List<Macro> _macros = [];

    public IReadOnlyList<Macro> Macros => _macros;

    public MacroRepository()
    {
        var dir = Service.PluginInterface.GetPluginConfigDirectory();
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "macros.db");
        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        EnsureSchema();
        _macros.AddRange(LoadAll());
        Macro.OnMacroChanged += OnMacroChanged;
    }

    // ── Schema ────────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        Exec("PRAGMA journal_mode=WAL");
        Exec("PRAGMA foreign_keys=ON");
        Exec("""
            CREATE TABLE IF NOT EXISTS Macros (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Name         TEXT    NOT NULL DEFAULT '',
                RecipeId     INTEGER,
                SavedScore   REAL    NOT NULL DEFAULT 0,
                DisplayOrder INTEGER NOT NULL DEFAULT 0
            )
            """);
        Exec("""
            CREATE TABLE IF NOT EXISTS MacroActions (
                MacroId    INTEGER NOT NULL REFERENCES Macros(Id) ON DELETE CASCADE,
                Position   INTEGER NOT NULL,
                ActionType TEXT    NOT NULL,
                PRIMARY KEY (MacroId, Position)
            )
            """);
        Exec("CREATE INDEX IF NOT EXISTS idx_macros_order  ON Macros(DisplayOrder)");
        Exec("CREATE INDEX IF NOT EXISTS idx_macros_recipe ON Macros(RecipeId)");
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private IEnumerable<Macro> LoadAll()
    {
        // Pre-load all actions grouped by MacroId
        var actionsByMacro = new Dictionary<long, List<ActionType>>();
        using (var cmd = Command("SELECT MacroId, ActionType FROM MacroActions ORDER BY MacroId, Position"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var macroId = r.GetInt64(0);
                if (Enum.TryParse<ActionType>(r.GetString(1), out var action))
                {
                    if (!actionsByMacro.TryGetValue(macroId, out var list))
                        actionsByMacro[macroId] = list = [];
                    list.Add(action);
                }
            }
        }

        using var macroCmd = Command("SELECT Id, Name, RecipeId, SavedScore FROM Macros ORDER BY DisplayOrder, Id");
        using var mr = macroCmd.ExecuteReader();
        while (mr.Read())
        {
            var id = mr.GetInt64(0);
            var macro = new Macro
            {
                Id = (int)id,
                // Set backing fields via internal setter to avoid firing OnMacroChanged
                // while the macro is not yet tracked by this repository.
            };
            // Use direct field assignment to bypass OnMacroChanged during load.
            macro.SetFieldsDirect(mr.GetString(1), mr.IsDBNull(2) ? null : (ushort)mr.GetInt64(2), (float)mr.GetDouble(3));
            if (actionsByMacro.TryGetValue(id, out var actions))
                macro.actions = [.. actions];
            yield return macro;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Adds a new macro and persists it to the database.</summary>
    public void Add(Macro macro)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            var id = InsertMacroRow(macro, _macros.Count, tx);
            macro.Id = (int)id;
            InsertActions(macro, tx);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        _macros.Add(macro);
    }

    /// <summary>Removes a macro from the database.</summary>
    public void Remove(Macro macro)
    {
        using var cmd = Command("DELETE FROM Macros WHERE Id=$id");
        cmd.Parameters.AddWithValue("$id", macro.Id);
        cmd.ExecuteNonQuery();
        _macros.Remove(macro);
        RewriteDisplayOrders();
    }

    /// <summary>Swaps the display order of two macros by index.</summary>
    public void Swap(int i, int j)
    {
        (_macros[i], _macros[j]) = (_macros[j], _macros[i]);
        RewriteDisplayOrders();
    }

    /// <summary>Moves a macro from one index to another.</summary>
    public void Move(int fromIdx, int toIdx)
    {
        var macro = _macros[fromIdx];
        _macros.RemoveAt(fromIdx);
        _macros.Insert(toIdx, macro);
        RewriteDisplayOrders();
    }

    // ── Migration from legacy JSON ─────────────────────────────────────────────

    /// <summary>
    /// Called once during startup to migrate macros from the old JSON config.
    /// Inserts each macro without firing <see cref="Configuration.OnMacroListChanged"/>.
    /// </summary>
    internal void MigrateFromJson(IReadOnlyList<Macro> legacyMacros)
    {
        if (legacyMacros.Count == 0)
            return;

        using var tx = _db.BeginTransaction();
        try
        {
            foreach (var macro in legacyMacros)
            {
                var id = InsertMacroRow(macro, _macros.Count, tx);
                macro.Id = (int)id;
                InsertActions(macro, tx);
                _macros.Add(macro);
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Change tracking via Macro.OnMacroChanged ──────────────────────────────

    private void OnMacroChanged(Macro macro)
    {
        // Ignore macros not managed by this repository (e.g. during object initialization).
        if (!_macros.Contains(macro))
            return;
        UpdateMacro(macro);
    }

    private void UpdateMacro(Macro macro)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            using (var cmd = Command("UPDATE Macros SET Name=$name, RecipeId=$recipeId, SavedScore=$score WHERE Id=$id", tx))
            {
                cmd.Parameters.AddWithValue("$name", macro.Name);
                cmd.Parameters.AddWithValue("$recipeId", macro.RecipeId.HasValue ? (object)macro.RecipeId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("$score", macro.SavedScore);
                cmd.Parameters.AddWithValue("$id", macro.Id);
                cmd.ExecuteNonQuery();
            }
            using (var del = Command("DELETE FROM MacroActions WHERE MacroId=$id", tx))
            {
                del.Parameters.AddWithValue("$id", macro.Id);
                del.ExecuteNonQuery();
            }
            InsertActions(macro, tx);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private long InsertMacroRow(Macro macro, int displayOrder, SqliteTransaction tx)
    {
        using var cmd = Command(
            "INSERT INTO Macros (Name, RecipeId, SavedScore, DisplayOrder) VALUES ($name, $recipeId, $score, $order); SELECT last_insert_rowid()",
            tx);
        cmd.Parameters.AddWithValue("$name", macro.Name);
        cmd.Parameters.AddWithValue("$recipeId", macro.RecipeId.HasValue ? (object)macro.RecipeId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$score", macro.SavedScore);
        cmd.Parameters.AddWithValue("$order", displayOrder);
        return (long)cmd.ExecuteScalar()!;
    }

    private void InsertActions(Macro macro, SqliteTransaction tx)
    {
        for (var i = 0; i < macro.actions.Length; i++)
        {
            using var cmd = Command("INSERT INTO MacroActions (MacroId, Position, ActionType) VALUES ($id, $pos, $action)", tx);
            cmd.Parameters.AddWithValue("$id", macro.Id);
            cmd.Parameters.AddWithValue("$pos", i);
            cmd.Parameters.AddWithValue("$action", Enum.GetName(macro.actions[i]) ?? macro.actions[i].ToString());
            cmd.ExecuteNonQuery();
        }
    }

    private void RewriteDisplayOrders()
    {
        using var tx = _db.BeginTransaction();
        try
        {
            for (var i = 0; i < _macros.Count; i++)
            {
                using var cmd = Command("UPDATE Macros SET DisplayOrder=$order WHERE Id=$id", tx);
                cmd.Parameters.AddWithValue("$order", i);
                cmd.Parameters.AddWithValue("$id", _macros[i].Id);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private SqliteCommand Command(string sql, SqliteTransaction? tx = null)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        return cmd;
    }

    public void Dispose()
    {
        Macro.OnMacroChanged -= OnMacroChanged;
        _db.Close();
        _db.Dispose();
    }
}
