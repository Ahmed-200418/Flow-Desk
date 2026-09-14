using FlowDesk.Application.Common.Interfaces;

namespace FlowDesk.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
