using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Controllers;

[ApiController]
[Route("api/v1/admin/network")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class NetworkController : ControllerBase
{
    private readonly AppDbContext _db;

    public NetworkController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the dual-team binary tree rooted at the given memberId, up to the specified depth.
    /// </summary>
    [HttpGet("binary-tree")]
    public async Task<IActionResult> GetBinaryTree(
        [FromQuery] string memberId,
        [FromQuery] int depth = 3,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberId))
            return BadRequest(ApiResponse<object>.Fail("INVALID_REQUEST", "memberId is required."));

        depth = Math.Clamp(depth, 1, 6);

        // Load the root node
        var root = await _db.DualTeamTree.AsNoTracking()
            .FirstOrDefaultAsync(n => n.MemberId == memberId, ct);

        if (root is null)
            return NotFound(ApiResponse<object>.Fail("NODE_NOT_FOUND", $"Member '{memberId}' not found in dual team tree."));

        // Load a subtree using the HierarchyPath prefix — much more efficient than recursive queries
        var pathPrefix = root.HierarchyPath;
        var allNodes = await _db.DualTeamTree.AsNoTracking()
            .Where(n => n.HierarchyPath.StartsWith(pathPrefix))
            .ToListAsync(ct);

        // Load member names in a single query
        var memberIds = allNodes.Select(n => n.MemberId).Distinct().ToList();
        var profiles = await _db.MemberProfiles.AsNoTracking()
            .Where(p => memberIds.Contains(p.MemberId))
            .Select(p => new { p.MemberId, FullName = p.FirstName + " " + p.LastName })
            .ToDictionaryAsync(p => p.MemberId, p => p.FullName, ct);

        // Load latest ranks
        var latestRanks = await _db.MemberRankHistories.AsNoTracking()
            .Where(r => memberIds.Contains(r.MemberId))
            .Include(r => r.RankDefinition)
            .GroupBy(r => r.MemberId)
            .Select(g => g.OrderByDescending(r => r.AchievedAt).First())
            .ToDictionaryAsync(r => r.MemberId, r => r.RankDefinition!.Name, ct);

        // Build tree recursively up to depth
        var nodeMap = allNodes.ToDictionary(n => n.MemberId);

        BinaryNodeDto BuildNode(string mid, int currentDepth)
        {
            var node = nodeMap.GetValueOrDefault(mid);
            var fullName = profiles.GetValueOrDefault(mid, mid);
            var rankName = latestRanks.GetValueOrDefault(mid);

            decimal leftPts = node?.LeftLegPoints ?? 0;
            decimal rightPts = node?.RightLegPoints ?? 0;

            // Count descendants per side
            var leftChildren = allNodes.Where(n => n.ParentMemberId == mid && n.Side == TreeSide.Left).ToList();
            var rightChildren = allNodes.Where(n => n.ParentMemberId == mid && n.Side == TreeSide.Right).ToList();

            // Count total in each leg
            int leftCount = allNodes.Count(n => n.HierarchyPath.StartsWith(pathPrefix) && leftChildren.Any(lc => n.HierarchyPath.StartsWith(lc.HierarchyPath)));
            int rightCount = allNodes.Count(n => n.HierarchyPath.StartsWith(pathPrefix) && rightChildren.Any(rc => n.HierarchyPath.StartsWith(rc.HierarchyPath)));

            BinaryNodeDto? leftNode = null;
            BinaryNodeDto? rightNode = null;

            if (currentDepth < depth)
            {
                var leftChild = leftChildren.FirstOrDefault();
                var rightChild = rightChildren.FirstOrDefault();
                if (leftChild is not null) leftNode = BuildNode(leftChild.MemberId, currentDepth + 1);
                if (rightChild is not null) rightNode = BuildNode(rightChild.MemberId, currentDepth + 1);
            }

            return new BinaryNodeDto(mid, fullName, rankName, leftPts, rightPts, leftCount, rightCount, leftNode, rightNode);
        }

        var tree = BuildNode(memberId, 0);
        return Ok(ApiResponse<BinaryNodeDto>.Ok(tree));
    }

    private record BinaryNodeDto(
        string MemberId,
        string FullName,
        string? RankName,
        decimal LeftPoints,
        decimal RightPoints,
        int LeftCount,
        int RightCount,
        BinaryNodeDto? Left,
        BinaryNodeDto? Right);
}
