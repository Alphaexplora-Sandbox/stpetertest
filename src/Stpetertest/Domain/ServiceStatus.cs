namespace Stpetertest.Domain;

// Whether the service considers itself healthy is a decision, not a
// transport concern, so it lives here and the endpoint asks for it.
// This is also the seam a unit test substitutes.
public interface IServiceStatus
{
    string CurrentStatus();
}

public sealed class ServiceStatus : IServiceStatus
{
    public string CurrentStatus() => "ok";
}
