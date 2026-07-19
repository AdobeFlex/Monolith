// (c) Space Exodus Team - EXDS-RL with CLA
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Exodus.Economy;

[AdminCommand(AdminFlags.Admin)]
public sealed class MarketQuoteCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "marketquote";
    public string Description => "Show global dynamic market factor for a key (stack:X / proto:Y / raw id).";
    public string Help => "Usage: marketquote <marketKey>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Usage: marketquote <marketKey>");
            return;
        }

        var market = _entities.System<DynamicMarketSystem>();
        var key = args[0];
        var factor = market.GetFactor(key);
        market.TryGetQuote(key, out var quote);
        shell.WriteLine($"{key}: factor={factor:F4} trend={quote.Trend:F4} change%={quote.ChangePercent:F2}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class MarketSetCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "marketset";
    public string Description => "Set global dynamic market factor for a key.";
    public string Help => "Usage: marketset <marketKey> <factor>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 || !double.TryParse(args[1], out var factor))
        {
            shell.WriteError("Usage: marketset <marketKey> <factor>");
            return;
        }

        var market = _entities.System<DynamicMarketSystem>();
        market.SetFactor(args[0], factor);
        shell.WriteLine($"Set {args[0]} factor → {market.GetFactor(args[0]):F4}");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class MarketResetCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "marketreset";
    public string Description => "Reset global market factors (all keys, or one key).";
    public string Help => "Usage: marketreset [marketKey]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var market = _entities.System<DynamicMarketSystem>();
        if (args.Length == 0)
        {
            market.ResetAll();
            shell.WriteLine("All market factors reset to base.");
            return;
        }

        market.ResetKey(args[0]);
        shell.WriteLine($"Reset {args[0]} to base.");
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class MarketListCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "marketlist";
    public string Description => "List non-base global market factors.";
    public string Help => "Usage: marketlist";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var market = _entities.System<DynamicMarketSystem>();
        var quotes = market.GetAllQuotes();
        if (quotes.Count == 0)
        {
            shell.WriteLine("No active market deviations (all at base 1.0).");
            return;
        }

        foreach (var (key, quote) in quotes)
        {
            shell.WriteLine($"{key}: {quote.Factor:F4} ({quote.ChangePercent:+0.00;-0.00}%) trend={quote.Trend:F4}");
        }
    }
}
