using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.GetS3Config;

public record GetS3ConfigQuery : IRequest<Result<S3StorageConfigDto>>;
