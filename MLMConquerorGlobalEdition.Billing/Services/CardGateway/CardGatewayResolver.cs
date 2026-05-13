using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Billing.Services.CardGateway;

/// <summary>
/// Maps CardProcessor → ICardGatewayService.
/// </summary>
public class CardGatewayResolver : ICardGatewayResolver
{
    private readonly Dictionary<CardProcessor, ICardGatewayService> _map;

    public CardGatewayResolver(IEnumerable<ICardGatewayService> services)
    {
        _map = services.ToDictionary(s => s.Processor);
    }

    public ICardGatewayService Resolve(CardProcessor processor)
    {
        if (_map.TryGetValue(processor, out var svc)) return svc;
        throw new InvalidOperationException($"No ICardGatewayService registered for processor '{processor}'.");
    }
}

public interface ICardGatewayResolver
{
    ICardGatewayService Resolve(CardProcessor processor);
}
