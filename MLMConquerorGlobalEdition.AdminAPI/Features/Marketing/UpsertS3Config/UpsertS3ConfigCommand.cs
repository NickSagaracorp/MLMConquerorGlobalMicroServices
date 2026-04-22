using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Marketing;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Marketing.UpsertS3Config;

public record UpsertS3ConfigCommand(S3StorageConfigDto Request) : IRequest<Result<S3StorageConfigDto>>;
