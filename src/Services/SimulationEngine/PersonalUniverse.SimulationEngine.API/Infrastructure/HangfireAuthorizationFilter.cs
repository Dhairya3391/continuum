using Hangfire.Dashboard;

namespace PersonalUniverse.SimulationEngine.API;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In production, you should implement proper authorization
        // For now, allow access in development/testing
        return true;
    }
}
