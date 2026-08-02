using Content.Shared._Mono.Company;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem
{
    private int GetCompanyDepositCommission(EntityUid player, int deposit)
    {
        if (deposit <= 0 ||
            !TryComp<CompanyComponent>(player, out var company) ||
            !_prototypeManager.TryIndex<CompanyPrototype>(company.CompanyName, out var companyPrototype))
        {
            return 0;
        }

        var commission = companyPrototype.AtmDepositCommission;
        if (!float.IsFinite(commission) || commission <= 0f)
            return 0;

        return (int) Math.Floor(deposit * Math.Min(commission, 1f));
    }
}
