using JetBrains.Annotations;

namespace Bombd.Attributes;

[AttributeUsage(AttributeTargets.Method)]
[MeansImplicitUse]
public class TransactionAttribute : Attribute
{
    public TransactionAttribute(string method, string? param = null)
    {
        Method = method;
        ParameterName = param;
    }

    public string Method { get; private set; }
    public string? ParameterName { get; private set; }
}