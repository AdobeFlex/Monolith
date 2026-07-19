// (c) Space Exodus Team - EXDS-RL with CLA
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Exodus.CCVar;
using Content.Shared._Exodus.Economy;
using Content.Shared.GameTicking;

namespace Content.Server._Exodus.Economy;

/// <summary>
/// Cross-round DB persistence for the global dynamic market quote store.
/// </summary>
public sealed partial class DynamicMarketSystem
{
    [Dependency] private readonly IServerDbManager _db = default!;

    private bool _persist = true;
    private float _persistInterval = 60f;
    private TimeSpan _nextPersist;
    private readonly HashSet<string> _dirtyKeys = new();
    private readonly HashSet<string> _deletedKeys = new();
    private bool _loadStarted;
    private bool _loadCompleted;

    /// <summary>
    /// Set by <see cref="ClearAllPersisted"/> so an in-flight DB load cannot repopulate
    /// after an admin/market reset.
    /// </summary>
    private bool _blockLoadApply;

    /// <summary>
    /// Next flush should wipe the entire quotes table (then re-upsert current memory).
    /// Required so ResetAll clears keys that never finished loading into memory.
    /// </summary>
    private bool _pendingFullClear;

    private bool _flushInProgress;
    private bool _forceFlushQueued;
    private Task? _pendingFlush;

    private void InitializePersistence()
    {
        Subs.CVar(_cfg, EXCVars.DynamicMarketPersist, OnPersistCVar, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketPersistIntervalSeconds, v =>
        {
            _persistInterval = Math.Max(5f, v);
            _nextPersist = _timing.CurTime + TimeSpan.FromSeconds(_persistInterval);
        }, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartFlush);
        _nextPersist = _timing.CurTime + TimeSpan.FromSeconds(_persistInterval);

        if (_persist)
            _ = LoadFromDatabaseAsync();
    }

    private void OnPersistCVar(bool value)
    {
        _persist = value;
        if (_persist && !_loadStarted)
            _ = LoadFromDatabaseAsync();
    }

    private void UpdatePersistence()
    {
        if (!_persist)
            return;

        if ((_forceFlushQueued || _pendingFullClear) && !_flushInProgress)
        {
            _forceFlushQueued = false;
            _pendingFlush = FlushDirtyAsync(forceAll: true);
            return;
        }

        if (_timing.CurTime < _nextPersist)
            return;

        _nextPersist += TimeSpan.FromSeconds(_persistInterval);
        _pendingFlush = FlushDirtyAsync();
    }

    private void OnRoundRestartFlush(RoundRestartCleanupEvent ev)
    {
        QueueForceFlush();
    }

    /// <summary>
    /// Called from <see cref="DynamicMarketSystem.Shutdown"/>.
    /// </summary>
    private void ShutdownPersistence()
    {
        if (!_persist)
            return;

        // Kick a force flush; cannot reliably block the entity-system shutdown path.
        QueueForceFlush();
        if (!_flushInProgress)
            _pendingFlush = FlushDirtyAsync(forceAll: true);
    }

    private void QueueForceFlush()
    {
        if (!_persist)
            return;

        if (_flushInProgress)
        {
            _forceFlushQueued = true;
            return;
        }

        _pendingFlush = FlushDirtyAsync(forceAll: true);
    }

    private void MarkDirty(string marketKey)
    {
        if (!_persist)
            return;

        _dirtyKeys.Add(marketKey);
        _deletedKeys.Remove(marketKey);
    }

    private void MarkDeleted(string marketKey)
    {
        if (!_persist)
            return;

        _deletedKeys.Add(marketKey);
        _dirtyKeys.Remove(marketKey);
    }

    /// <summary>
    /// Wipe persisted market state: block any in-flight load apply, clear dirty tracking,
    /// and queue a full table clear + re-upsert of current in-memory quotes.
    /// Caller must already have cleared <see cref="_quotes"/>.
    /// </summary>
    private void ClearAllPersisted()
    {
        if (!_persist)
            return;

        _blockLoadApply = true;
        _pendingFullClear = true;
        _dirtyKeys.Clear();
        _deletedKeys.Clear();
        QueueForceFlush();
    }

    private async Task LoadFromDatabaseAsync()
    {
        if (_loadStarted || !_persist)
            return;

        _loadStarted = true;

        try
        {
            var rows = await _db.GetAllEconomyMarketQuotes();

            // Admin reset while awaiting DB — do not repopulate from stale rows.
            if (_blockLoadApply)
            {
                Log.Info("Skipped applying economy market quotes load; market was reset during load.");
                return;
            }

            // Per-key merge only. Never discard the whole load because one key traded during
            // the await — that previously wiped every other persisted factor for the session.
            var loaded = 0;
            foreach (var (key, factor, trend, _) in rows)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (_dirtyKeys.Contains(key) || _deletedKeys.Contains(key) || _quotes.ContainsKey(key))
                    continue;

                _quotes[key] = new MarketQuote(factor)
                {
                    Trend = trend,
                    PreviousFactor = factor,
                };
                loaded++;
            }

            Log.Info($"Loaded {loaded} economy market quotes from database ({rows.Count} rows).");
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load economy market quotes; continuing with empty store. {e}");
        }
        finally
        {
            _loadCompleted = true;
        }
    }

    private async Task FlushDirtyAsync(bool forceAll = false)
    {
        if (!_persist)
            return;

        if (_flushInProgress)
        {
            if (forceAll || _pendingFullClear)
                _forceFlushQueued = true;
            return;
        }

        var doFullClear = _pendingFullClear;
        if (!forceAll && !doFullClear && _dirtyKeys.Count == 0 && _deletedKeys.Count == 0)
            return;

        _flushInProgress = true;
        _pendingFullClear = false;

        List<(string MarketKey, double Factor, float Trend)> upserts;
        List<string> deletes;

        if (doFullClear)
        {
            // Full table wipe then write whatever is currently in memory (usually empty after ResetAll).
            upserts = new List<(string, double, float)>(_quotes.Count);
            foreach (var (key, quote) in _quotes)
            {
                upserts.Add((key, quote.Factor, quote.Trend));
            }

            deletes = new List<string>();
            _dirtyKeys.Clear();
            _deletedKeys.Clear();
        }
        else if (forceAll)
        {
            upserts = new List<(string, double, float)>(_quotes.Count);
            foreach (var (key, quote) in _quotes)
            {
                upserts.Add((key, quote.Factor, quote.Trend));
            }

            deletes = new List<string>(_deletedKeys);
            _dirtyKeys.Clear();
            _deletedKeys.Clear();
        }
        else
        {
            upserts = new List<(string, double, float)>(_dirtyKeys.Count);
            foreach (var key in _dirtyKeys)
            {
                if (_quotes.TryGetValue(key, out var quote))
                    upserts.Add((key, quote.Factor, quote.Trend));
            }

            deletes = new List<string>(_deletedKeys);
            _dirtyKeys.Clear();
            _deletedKeys.Clear();
        }

        try
        {
            if (doFullClear)
                await _db.ClearEconomyMarketQuotes();

            if (upserts.Count > 0)
                await _db.UpsertEconomyMarketQuotes(upserts);

            if (deletes.Count > 0)
                await _db.DeleteEconomyMarketQuotes(deletes);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to flush economy market quotes: {e}");

            if (doFullClear)
                _pendingFullClear = true;

            foreach (var (key, _, _) in upserts)
            {
                if (_quotes.ContainsKey(key))
                    _dirtyKeys.Add(key);
            }

            foreach (var key in deletes)
            {
                if (!_quotes.ContainsKey(key))
                    _deletedKeys.Add(key);
            }
        }
        finally
        {
            _flushInProgress = false;

            if (_forceFlushQueued || _pendingFullClear)
            {
                _forceFlushQueued = false;
                _pendingFlush = FlushDirtyAsync(forceAll: true);
            }
        }
    }
}
