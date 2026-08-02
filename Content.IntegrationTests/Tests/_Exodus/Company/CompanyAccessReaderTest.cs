using Content.Shared._Mono.Company;
using Content.Shared.Access.Components;
using Content.Shared.DoAfter;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Exodus.Company;

[TestFixture]
[TestOf(typeof(CompanyAccessReaderSystem))]
public sealed class CompanyAccessReaderTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ExodusTestCompanyAccessReader
  components:
  - type: CompanyAccessReader
    requireCompanyCard: true
    requiredCompanies:
    - SteelHammerManufacturing
    popupMessage: null
";

    [Test]
    public async Task DumpRequiresMatchingCompanyCard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var readerUid = entMan.Spawn("ExodusTestCompanyAccessReader");
            var user = entMan.Spawn();

            var denied = CreateDumpEvent(entMan, readerUid, user);
            entMan.EventBus.RaiseLocalEvent(readerUid, denied);
            Assert.That(denied.Handled, Is.True);

            var idCard = entMan.AddComponent<IdCardComponent>(user);
            idCard.CompanyName = "SteelHammerManufacturing";

            var allowed = CreateDumpEvent(entMan, readerUid, user);
            entMan.EventBus.RaiseLocalEvent(readerUid, allowed);
            Assert.That(allowed.Handled, Is.False);

            entMan.DeleteEntity(readerUid);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    private static DumpableDoAfterEvent CreateDumpEvent(
        IEntityManager entMan,
        EntityUid reader,
        EntityUid user)
    {
        var dumpEvent = new DumpableDoAfterEvent();
        var args = new DoAfterArgs(entMan,
            user,
            TimeSpan.Zero,
            dumpEvent,
            reader,
            target: reader,
            used: reader);
        dumpEvent.DoAfter = new Content.Shared.DoAfter.DoAfter(0, args, TimeSpan.Zero);
        return dumpEvent;
    }
}
