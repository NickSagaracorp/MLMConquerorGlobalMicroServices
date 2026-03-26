using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Domain.Exceptions;

namespace MLMConquerorGlobalEdition.Domain.Entities.Tokens;

public class TokenBalance : AuditChangesStringKey
{
    public string MemberId { get; set; } = string.Empty;
    public int TokenTypeId { get; set; }
    public int Balance { get; set; }

    public void Deduct(int amount)
    {
        if (amount > Balance)
            throw new InsufficientTokenBalanceException();
        Balance -= amount;
    }

    public void Add(int amount) => Balance += amount;
}
