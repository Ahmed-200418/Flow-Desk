using System.Collections.Concurrent;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace FlowDesk.Infrastructure.Logging;

public class SensitiveDataRedactionDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "ConfirmPassword",
        "CurrentPassword",
        "NewPassword",
        "Token",
        "AccessToken",
        "RefreshToken",
        "Jwt",
        "Authorization",
        "ApiKey",
        "Secret",
        "ClientSecret",
        "ConnectionString",
        "SecurityStamp",
        "ResetToken",
        "EmailConfirmationToken",
        "TwoFactorCode",
        "Otp",
        "Cookie"
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> TypePropertyCache = new();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        result = null;

        if (value == null) return false;

        var type = value.GetType();

        // Do not destructure primitive types, strings, standard framework types
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(decimal))
        {
            return false;
        }

        // Only destructure non-system objects (e.g. DTOs, Commands, Queries, Entities)
        if (type.Namespace != null && (type.Namespace.StartsWith("System") || type.Namespace.StartsWith("Microsoft") || type.Namespace.StartsWith("Serilog")))
        {
            return false;
        }

        var properties = TypePropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        if (properties.Length == 0) return false;

        var logProperties = new List<LogEventProperty>();

        foreach (var prop in properties)
        {
            if (!prop.CanRead) continue;

            object? propVal;
            try
            {
                propVal = prop.GetValue(value);
            }
            catch
            {
                continue;
            }

            if (SensitiveKeys.Contains(prop.Name))
            {
                logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue("[REDACTED]")));
            }
            else
            {
                var destructuredVal = propertyValueFactory.CreatePropertyValue(propVal, destructureObjects: true);
                logProperties.Add(new LogEventProperty(prop.Name, destructuredVal));
            }
        }

        result = new StructureValue(logProperties, type.Name);
        return true;
    }

    public static bool IsSensitiveKey(string key)
    {
        return SensitiveKeys.Contains(key);
    }
}
