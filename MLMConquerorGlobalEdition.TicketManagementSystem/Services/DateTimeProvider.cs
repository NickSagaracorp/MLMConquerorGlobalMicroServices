namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now => DateTime.UtcNow;
}
