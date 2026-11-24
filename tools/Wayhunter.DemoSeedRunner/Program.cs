using Wayhunter.DemoSeedRunner;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Explicit Wayhunter demo seed tool. It never creates a database or runs migrations.");
    Console.WriteLine("Set WAYHUNTER_DEMO_SEED_CONNECTION, WAYHUNTER_DEMO_SEED_DATABASE, WAYHUNTER_DEMO_SEED_CONFIRM=SEED_DEMO_DATABASE:<catalog>, WAYHUNTER_DEMO_ACCOUNT_PASSWORD, WAYHUNTER_DEMO_VOUCHER_CODE, WAYHUNTER_DEMO_VOUCHER_IMAGE_REFERENCE and WAYHUNTER_DEMO_VOUCHER_PROTECTION_KEY.");
    Console.WriteLine("Pass --seed --skip-voucher to seed other modules without creating a voucher offer or inventory; only database authorization and the account password are required.");
    return 0;
}

var skipVoucher = args.Length == 2 && args[0] == "--seed" && args[1] == "--skip-voucher";
if (!(args.Length == 1 && args[0] == "--seed") && !skipVoucher)
{
    Console.Error.WriteLine("Pass --seed to write data, optionally followed by --skip-voucher. No database action was taken.");
    return 2;
}

try
{
    var options = DemoSeedOptions.FromEnvironment(
        Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(x => (string)x.Key, x => x.Value?.ToString(), StringComparer.OrdinalIgnoreCase),
        seedVoucher: !skipVoucher);
    await new DemoSeedRunnerService(options).RunAsync();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Demo seed failed closed: {ex.Message}");
    return 1;
}
