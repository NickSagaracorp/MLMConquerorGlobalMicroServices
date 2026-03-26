using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SelectProducts;

public record SelectProductsCommand(string SignupId, SelectProductsRequest Request) : IRequest<Result<bool>>;
