namespace Content.IntegrationTests;

[SetUpFixture]
public sealed class PoolManagerTestEventHandler
{
    // This value is completely arbitrary.
    // Grosse: the fork's suite (with the Grosse/_Grosse integration tests) needs ~23 min on slow CI
    // runners; the stock 20 min limit killed the last tests mid-run (PoolManager.Shutdown) and then
    // FailFast-ed the whole host, so the limit is raised well above the observed worst case.
    private static TimeSpan MaximumTotalTestingTimeLimit => TimeSpan.FromMinutes(45);
    private static TimeSpan HardStopTimeLimit => MaximumTotalTestingTimeLimit.Add(TimeSpan.FromMinutes(1));

    [OneTimeSetUp]
    public void Setup()
    {
        PoolManager.Startup();
        // If the tests seem to be stuck, we try to end it semi-nicely
        _ = Task.Delay(MaximumTotalTestingTimeLimit).ContinueWith(_ =>
        {
            // This can and probably will cause server/client pairs to shut down MID test, and will lead to really confusing test failures.
            TestContext.Error.WriteLine($"\n\n{nameof(PoolManagerTestEventHandler)}: ERROR: Tests are taking too long. Shutting down all tests. This may lead to weird failures/exceptions.\n\n");
            PoolManager.Shutdown();
        });

        // If ending it nicely doesn't work within a minute, we do something a bit meaner.
        _ = Task.Delay(HardStopTimeLimit).ContinueWith(_ =>
        {
            var deathReport = PoolManager.DeathReport();
            Environment.FailFast($"Tests took way too ;\n Death Report:\n{deathReport}");
        });
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        PoolManager.Shutdown();
    }
}
