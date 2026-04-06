using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.Services;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.BizCenter.Features.Placement.GetAvailableNodes;

public class GetAvailableNodesHandler
    : IRequestHandler<GetAvailableNodesQuery, Result<AvailableNodesResponse>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;

    public GetAvailableNodesHandler(AppDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<Result<AvailableNodesResponse>> Handle(
        GetAvailableNodesQuery request, CancellationToken ct)
    {
        // Find the sponsor of the member to place
        var memberToPlace = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == request.MemberToPlaceId && !m.IsDeleted, ct);

        if (memberToPlace is null)
            return Result<AvailableNodesResponse>.Failure("MEMBER_NOT_FOUND",
                "El miembro no existe.");

        var sponsorMemberId = memberToPlace.SponsorMemberId;
        if (string.IsNullOrEmpty(sponsorMemberId))
            return Result<AvailableNodesResponse>.Failure("NO_SPONSOR",
                "El miembro no tiene sponsor asignado.");

        // Sponsor's profile
        var sponsor = await _db.MemberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.MemberId == sponsorMemberId, ct);

        if (sponsor is null)
            return Result<AvailableNodesResponse>.Failure("SPONSOR_NOT_FOUND",
                "El sponsor no existe.");

        // Get sponsor's dual-team node
        var sponsorNode = await _db.DualTeamTree
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MemberId == sponsorMemberId, ct);

        // If admin: get full enrollment team subtree of sponsor
        // If ambassador: limited to sponsor's enrollment team
        var enrollmentTeamIds = await GetEnrollmentTeamIdsAsync(sponsorMemberId, ct);

        // Get all dual-team nodes within the enrollment team
        var treeNodes = await _db.DualTeamTree
            .AsNoTracking()
            .Where(d => enrollmentTeamIds.Contains(d.MemberId))
            .ToListAsync(ct);

        var treeNodeIds = treeNodes.Select(n => n.MemberId).ToList();

        // Get profiles for display names
        var profiles = await _db.MemberProfiles
            .AsNoTracking()
            .Where(m => treeNodeIds.Contains(m.MemberId))
            .ToDictionaryAsync(m => m.MemberId, ct);

        // Build occupancy map: parentId → side → bool
        var occupancy = treeNodes.GroupBy(n => n.ParentMemberId ?? "")
            .ToDictionary(
                g => g.Key,
                g => g.Select(n => n.Side).ToHashSet()
            );

        // Build response nodes — include sponsor even if not in dual tree yet
        var allMemberIds = new HashSet<string>(treeNodeIds) { sponsorMemberId };
        var sponsorProfile = profiles.ContainsKey(sponsorMemberId)
            ? profiles[sponsorMemberId]
            : sponsor;

        var result = allMemberIds
            .Where(id => profiles.ContainsKey(id))
            .Select(id =>
            {
                var p = profiles[id];
                occupancy.TryGetValue(id, out var sides);
                var leftOccupied  = sides?.Contains(TreeSide.Left)  ?? false;
                var rightOccupied = sides?.Contains(TreeSide.Right) ?? false;

                var leftChild  = treeNodes.FirstOrDefault(n => n.ParentMemberId == id && n.Side == TreeSide.Left);
                var rightChild = treeNodes.FirstOrDefault(n => n.ParentMemberId == id && n.Side == TreeSide.Right);

                string? leftName  = leftChild  != null && profiles.ContainsKey(leftChild.MemberId)
                    ? $"{profiles[leftChild.MemberId].FirstName} {profiles[leftChild.MemberId].LastName}"
                    : null;
                string? rightName = rightChild != null && profiles.ContainsKey(rightChild.MemberId)
                    ? $"{profiles[rightChild.MemberId].FirstName} {profiles[rightChild.MemberId].LastName}"
                    : null;

                var node = treeNodes.FirstOrDefault(n => n.MemberId == id);

                return new PlacementTreeNodeDto
                {
                    MemberId         = id,
                    FullName         = $"{p.FirstName} {p.LastName}",
                    MemberCode       = p.MemberId,
                    PhotoUrl         = p.ProfilePhotoUrl,
                    LeftChildId      = leftChild?.MemberId,
                    LeftChildName    = leftName,
                    RightChildId     = rightChild?.MemberId,
                    RightChildName   = rightName,
                    IsLeftAvailable  = !leftOccupied,
                    IsRightAvailable = !rightOccupied,
                    ParentMemberId   = node?.ParentMemberId,
                    Depth            = node != null
                        ? node.HierarchyPath.Count(c => c == '/') - 1
                        : 0
                };
            })
            .OrderBy(n => n.Depth)
            .ThenBy(n => n.FullName)
            .ToList();

        return Result<AvailableNodesResponse>.Success(new AvailableNodesResponse
        {
            SponsorMemberId = sponsorMemberId,
            SponsorFullName = $"{sponsorProfile.FirstName} {sponsorProfile.LastName}",
            Nodes           = result
        });
    }

    /// <summary>
    /// Returns all member IDs in the enrollment (genealogy) tree of the given sponsor.
    /// </summary>
    private async Task<List<string>> GetEnrollmentTeamIdsAsync(
        string sponsorMemberId, CancellationToken ct)
    {
        var sponsorGenNode = await _db.GenealogyTree
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.MemberId == sponsorMemberId, ct);

        if (sponsorGenNode is null)
            return new List<string> { sponsorMemberId };

        // All descendants in enrollment tree using HierarchyPath
        var descendants = await _db.GenealogyTree
            .AsNoTracking()
            .Where(g => g.HierarchyPath.StartsWith(sponsorGenNode.HierarchyPath))
            .Select(g => g.MemberId)
            .ToListAsync(ct);

        return descendants;
    }
}
