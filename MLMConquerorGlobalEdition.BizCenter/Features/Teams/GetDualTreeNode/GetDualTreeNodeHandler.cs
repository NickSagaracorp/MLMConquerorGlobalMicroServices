using MediatR;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Teams.GetDualTreeNode;

/// <summary>
/// BizCenter dual-tree node endpoint. Delegates ALL tree computation to the
/// shared <see cref="IDualTreeNodeService"/> — do not put query logic here.
/// </summary>
public class GetDualTreeNodeHandler : IRequestHandler<GetDualTreeNodeQuery, Result<DualTreeNodeDto>>
{
    private readonly IDualTreeNodeService _treeService;

    public GetDualTreeNodeHandler(IDualTreeNodeService treeService)
        => _treeService = treeService;

    public async Task<Result<DualTreeNodeDto>> Handle(GetDualTreeNodeQuery request, CancellationToken ct)
    {
        var view = await _treeService.GetNodeAsync(request.NodeMemberId, ct);
        return Result<DualTreeNodeDto>.Success(MapToDto(view));
    }

    private static DualTreeNodeDto MapToDto(DualTreeNodeView v) => new()
    {
        MemberId       = v.MemberId,
        FullName       = v.FullName,
        StatusCode     = v.StatusCode,
        Points         = v.Points,
        PersonalPoints = v.PersonalPoints,
        LeftChild      = MapChild(v.LeftChild),
        RightChild     = MapChild(v.RightChild)
    };

    private static DualTreeChildDto? MapChild(DualTreeChildView? v) => v is null ? null : new()
    {
        MemberId       = v.MemberId,
        FullName       = v.FullName,
        StatusCode     = v.StatusCode,
        Points         = v.Points,
        PersonalPoints = v.PersonalPoints,
        HasLeft        = v.HasLeft,
        HasRight       = v.HasRight,
        LeftChild      = MapGrandchild(v.LeftChild),
        RightChild     = MapGrandchild(v.RightChild)
    };

    private static DualTreeGrandchildDto? MapGrandchild(DualTreeGrandchildView? v) => v is null ? null : new()
    {
        MemberId       = v.MemberId,
        FullName       = v.FullName,
        StatusCode     = v.StatusCode,
        Points         = v.Points,
        PersonalPoints = v.PersonalPoints,
        HasLeft        = v.HasLeft,
        HasRight       = v.HasRight
    };
}
