using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Wallet.GetWallet;

public record GetWalletQuery() : IRequest<Result<WalletDto>>;
