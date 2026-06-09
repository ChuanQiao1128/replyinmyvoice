using Xunit;

// Tests run serially (cross-collection parallelization disabled). Several suites spin up internal
// concurrent tasks and wait on bounded timeouts — e.g. the Stripe webhook checkout-grant race
// (StripeEventServiceTests) and the V1 per-key rate-limit race (V1RewriteRateLimitTests). Under
// xUnit's default parallelism on CI, CPU oversubscription makes those internal tasks lag past
// their timeouts, producing flaky failures. Serial execution gives each test its full CPU and
// keeps CI deterministic (the trade-off is a slower test run, which is acceptable in CI).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
