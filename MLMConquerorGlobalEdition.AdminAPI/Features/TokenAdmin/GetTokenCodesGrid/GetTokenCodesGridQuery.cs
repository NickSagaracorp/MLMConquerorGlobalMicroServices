using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.TokenAdmin.GetTokenCodesGrid;

/// <summary>
/// Lectura del grid de códigos de token de un miembro. El miembro, el tipo de token y "usado / disponible" son los
/// controles de la página y se aplican sobre la tabla antes de proyectar; filtros de columna, orden y página viajan
/// en <see cref="Grid"/>.
/// </summary>
public record GetTokenCodesGridQuery(
    string          MemberId,
    GridDataRequest Grid,
    int?            TokenTypeId = null,
    bool?           IsUsed      = null
) : IRequest<Result<PagedResult<TokenCodeDto>>>;
