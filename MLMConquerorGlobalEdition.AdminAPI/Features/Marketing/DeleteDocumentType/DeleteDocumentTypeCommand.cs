using MediatR;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.DeleteDocumentType;

public record DeleteDocumentTypeCommand(int Id) : IRequest<Result<bool>>;
