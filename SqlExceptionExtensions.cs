using Microsoft.Data.SqlClient;
// Extension method to identify transient SQL errors (common in distributed systems)
public static class SqlExceptionExtensions
{
    public static bool IsTransient(this SqlException ex)
    {
        // Common transient errors: Timeout, client/server network error, resource busy
        return ex.Number == 1205 // Deadlock
            || ex.Number == 109 // Transport-level error
            || ex.Number == 4060 // Database unavailable
            || ex.Number == 40143 // Server less available
            || ex.Number == 40197 // Service encountering issues
            || ex.Number == 40501; // Service busy
    }
}
