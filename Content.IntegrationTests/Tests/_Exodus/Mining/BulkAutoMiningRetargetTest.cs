using System.Numerics;
using Content.Server._Exodus.Mining.AutoMining;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Shared._Exodus.Mining.AutoMining;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Exodus.Mining;

[TestFixture]
[TestOf(typeof(BulkAutoMiningSystem))]
public sealed class BulkAutoMiningRetargetTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExodusTestRetargetFastConsole
  parent: BulkAutoMiningConsole
  components:
  - type: BulkAutoMiningConsole
    tilesPerTick: 1
    processInterval: 0.1
";

    [Test]
    public async Task ObstructionRecoveryPreservesExcavationCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var maps = entities.System<SharedMapSystem>();
        var mining = entities.System<BulkAutoMiningSystem>();
        var materials = entities.System<MaterialStorageSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        Entity<BulkAutoMiningConsoleComponent> console = default;
        Entity<BulkAutoMiningEmitterComponent> laser = default;
        Entity<MapGridComponent> asteroid = default;
        EntityUid obstacle = default;
        EntityUid? startup = null;
        Vector2i nextTile = default;
        TimeSpan deadline = default;
        TimeSpan clearedAt = default;

        int Stored() => materials.GetTotalMaterialAmount(laser, localOnly: true);

        await server.WaitAssertion(() =>
        {
            PrepareShip(entities, map.Grid, map.Tile.Tile);
            asteroid = server.ResolveDependency<IMapManager>().CreateGridEntity(map.MapId);
            entities.System<SharedTransformSystem>().SetLocalPosition(asteroid, new Vector2(20, 0));
            for (var x = 0; x < 8; x++)
            {
                maps.SetTile(asteroid, new Vector2i(x, 3), map.Tile.Tile);
                entities.SpawnEntity("AsteroidRock", new EntityCoordinates(asteroid, new Vector2(x + 0.5f, 3.5f)));
            }

            var uid = SpawnPowered(entities, "BulkAutoMiningEmitter", map.Grid, new Vector2(5.5f, 3.5f));
            laser = (uid, entities.GetComponent<BulkAutoMiningEmitterComponent>(uid));
            uid = SpawnPowered(entities, "BulkAutoMiningConsole", map.Grid, new Vector2(0.5f, 0.5f));
            console = (uid, entities.GetComponent<BulkAutoMiningConsoleComponent>(uid));
            Assert.That(mining.TrySelectGrid(console, asteroid), Is.True);
            Assert.That(mining.TryStartMining(console), Is.True);
            Assert.That(Stored(), Is.EqualTo(200));
            AssertIntactTarget();
            nextTile = laser.Comp.BeamTile;
            startup = laser.Comp.StartupStream;
            deadline = laser.Comp.NextMiningTime;
            Assert.That((deadline - timing.CurTime).TotalSeconds, Is.EqualTo(10).Within(0.001));
        });

        await server.WaitRunTicks(18);
        await server.WaitAssertion(() =>
        {
            Assert.That(Stored(), Is.EqualTo(200));
            Assert.That(laser.Comp.BeamTile, Is.EqualTo(nextTile), "Search updates must preserve a valid target.");
            Assert.That(laser.Comp.StartupStream, Is.EqualTo(startup), "Search updates must not restart the audio.");
            obstacle = entities.SpawnEntity("WallShuttle", new EntityCoordinates(map.Grid, new Vector2(7.5f, 3.5f)));
        });
        await PoolManager.WaitUntil(server, () => laser.Comp.BeamGrid == null, maxTicks: 10);
        await server.WaitAssertion(() =>
        {
            Assert.That(Stored(), Is.EqualTo(200));
            entities.DeleteEntity(obstacle);
            clearedAt = timing.CurTime;
        });
        await PoolManager.WaitUntil(server, () => laser.Comp.BeamGrid != null, maxTicks: 18);
        await server.WaitAssertion(() =>
        {
            Assert.That((timing.CurTime - clearedAt).TotalSeconds, Is.LessThanOrEqualTo(0.6));
            AssertIntactTarget();
            Assert.That(laser.Comp.NextMiningTime, Is.EqualTo(deadline));
            Assert.That(Stored(), Is.EqualTo(200), "Recovering the beam must not grant another mining cycle.");
        });

        await PoolManager.WaitUntil(server, () => Stored() > 200, maxTicks: 330);
        await server.WaitAssertion(() =>
        {
            Assert.That(timing.CurTime, Is.GreaterThanOrEqualTo(deadline));
            Assert.That(Stored(), Is.EqualTo(400));
            Assert.That(console.Comp.ProcessedTiles, Is.EqualTo(2));
            AssertIntactTarget();
        });
        await server.WaitRunTicks(18);
        await server.WaitAssertion(() =>
        {
            Assert.That(Stored(), Is.EqualTo(400));
            mining.StopMining(console);
        });
        await pair.CleanReturnAsync();

        void AssertIntactTarget()
        {
            Assert.That(laser.Comp.BeamGrid, Is.EqualTo(asteroid.Owner));
            Assert.That(maps.GetTileRef(asteroid, asteroid.Comp, laser.Comp.BeamTile).Tile.IsEmpty, Is.False);
        }
    }

    [Test]
    public async Task ExhaustedSearchBudgetContinuesAfterHalfSecond()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var maps = entities.System<SharedMapSystem>();
        var mining = entities.System<BulkAutoMiningSystem>();
        var materials = entities.System<MaterialStorageSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        Entity<BulkAutoMiningConsoleComponent> console = default;
        EntityUid laser = default;
        TimeSpan started = default;

        int Stored() => materials.GetTotalMaterialAmount(laser, localOnly: true);

        await server.WaitAssertion(() =>
        {
            PrepareShip(entities, map.Grid, map.Tile.Tile);
            var asteroid = server.ResolveDependency<IMapManager>().CreateGridEntity(map.MapId);
            asteroid.Comp.CanSplit = false;
            entities.System<SharedTransformSystem>().SetLocalPosition(asteroid, new Vector2(20, 0));
            // The first chunks are out of range; the final chunk has reachable tiles.
            for (var x = 600; x < 680; x++)
                maps.SetTile(asteroid, new Vector2i(x, 3), map.Tile.Tile);
            for (var x = 0; x < 8; x++)
                maps.SetTile(asteroid, new Vector2i(x, 3), map.Tile.Tile);

            laser = SpawnPowered(entities, "BulkAutoMiningEmitter", map.Grid, new Vector2(5.5f, 3.5f));
            var uid = SpawnPowered(entities, "BulkAutoMiningConsole", map.Grid, new Vector2(0.5f, 0.5f));
            console = (uid, entities.GetComponent<BulkAutoMiningConsoleComponent>(uid));
            Assert.That(mining.TrySelectGrid(console, asteroid), Is.True);
            Assert.That(mining.TryStartMining(console), Is.True);
            started = timing.CurTime;
            Assert.That(Stored(), Is.Zero);
            Assert.That(entities.GetComponent<BulkAutoMiningJobComponent>(console).Statuses[laser],
                Is.EqualTo(BulkAutoMiningLaserStatus.Searching), "A partial search must not report a confirmed obstruction.");
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() => Assert.That(Stored(), Is.Zero));
        await PoolManager.WaitUntil(server, () => Stored() > 0, maxTicks: 10);
        await server.WaitAssertion(() =>
        {
            Assert.That((timing.CurTime - started).TotalSeconds, Is.InRange(0.5, 0.6));
            Assert.That(Stored(), Is.EqualTo(200));
            Assert.That(console.Comp.ProcessedTiles, Is.EqualTo(1));
        });
        await server.WaitRunTicks(18);
        await server.WaitAssertion(() =>
        {
            Assert.That(Stored(), Is.EqualTo(200));
            mining.StopMining(console);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AnotherLaserCannotAccelerateFailedSearches()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var maps = entities.System<SharedMapSystem>();
        var mining = entities.System<BulkAutoMiningSystem>();
        var materials = entities.System<MaterialStorageSystem>();
        var timing = server.ResolveDependency<IGameTiming>();
        Entity<BulkAutoMiningConsoleComponent> console = default;
        EntityUid blockedLaser = default;
        EntityUid workingLaser = default;
        TimeSpan started = default;

        int Stored(EntityUid uid) => materials.GetTotalMaterialAmount(uid, localOnly: true);

        await server.WaitAssertion(() =>
        {
            PrepareShip(entities, map.Grid, map.Tile.Tile);
            var asteroid = server.ResolveDependency<IMapManager>().CreateGridEntity(map.MapId);
            asteroid.Comp.CanSplit = false;
            entities.System<SharedTransformSystem>().SetLocalPosition(asteroid, new Vector2(20, 0));
            for (var x = 0; x < 64; x++)
                maps.SetTile(asteroid, new Vector2i(x, 3), map.Tile.Tile);

            blockedLaser = SpawnPowered(entities, "BulkAutoMiningEmitter", map.Grid, new Vector2(5.5f, 3.5f));
            workingLaser = SpawnPowered(entities, "BulkAutoMiningEmitter", map.Grid, new Vector2(5.5f, 6.5f));
            var uid = SpawnPowered(entities, "ExodusTestRetargetFastConsole", map.Grid, new Vector2(0.5f, 0.5f));
            console = (uid, entities.GetComponent<BulkAutoMiningConsoleComponent>(uid));
            var obstacle = entities.SpawnEntity("WallShuttle", new EntityCoordinates(map.Grid, new Vector2(7.5f, 3.5f)));
            Assert.That(mining.TrySelectGrid(console, asteroid), Is.True);
            Assert.That(mining.TryStartMining(console), Is.True);
            started = timing.CurTime;
            Assert.That(Stored(blockedLaser), Is.Zero);
            Assert.That(Stored(workingLaser), Is.EqualTo(200));
            entities.DeleteEntity(obstacle);
        });
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(Stored(workingLaser), Is.GreaterThan(200));
            Assert.That(Stored(blockedLaser), Is.Zero,
                "The other laser's 0.1-second work interval must not shorten a failed search's 0.5-second interval.");
        });
        await PoolManager.WaitUntil(server, () => Stored(blockedLaser) > 0, maxTicks: 10);
        await server.WaitAssertion(() =>
        {
            Assert.That((timing.CurTime - started).TotalSeconds, Is.InRange(0.5, 0.6));
            Assert.That(Stored(blockedLaser), Is.EqualTo(200));
            mining.StopMining(console);
        });
        await pair.CleanReturnAsync();
    }

    private static void PrepareShip(IEntityManager entities, Entity<MapGridComponent> grid, Tile tile)
    {
        var maps = entities.System<SharedMapSystem>();
        for (var x = 0; x < 9; x++)
            for (var y = 0; y < 8; y++)
                maps.SetTile(grid, new Vector2i(x, y), tile);
    }

    private static EntityUid SpawnPowered(IEntityManager entities, string prototype, EntityUid grid, Vector2 position)
    {
        var uid = entities.SpawnEntity(prototype, new EntityCoordinates(grid, position));
        var power = entities.GetComponent<ApcPowerReceiverComponent>(uid);
        power.NeedsPower = false;
        power.Powered = true;
        return uid;
    }
}
