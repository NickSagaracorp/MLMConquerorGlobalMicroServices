using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.Signups.DTOs;

namespace MLMConquerorGlobalEdition.Signups.Features.Signups.Commands.SelectProducts;

public record SelectProductsCommand(string SignupId, SelectProductsRequest Request) : IRequest<Result<bool>>;
