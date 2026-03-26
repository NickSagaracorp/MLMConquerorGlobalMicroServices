using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Wallet.UpdateWallet;

public record UpdateWalletCommand(UpdateWalletRequest Request) : IRequest<Result<WalletDto>>;
