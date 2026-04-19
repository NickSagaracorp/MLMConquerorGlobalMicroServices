namespace MLMConquerorGlobalEdition.BizCenter.DTOs.Teams;

/// <summary>Top-level result for GET /api/v1/bizcenter/team/enrollment/branches.</summary>
public class EnrollmentBranchesResultDto
{
    /// <summary>Sum of EnrollmentPoints across all direct-sponsored branches.</summary>
    public int TotalPoints { get; set; }

    /// <summary>Sum of EligibleCurrentRank across all direct-sponsored branches.</summary>
    public int TotalEligibleCurrentRank { get; set; }

    /// <summary>Sum of EligibleNextRank across all direct-sponsored branches.</summary>
    public int TotalEligibleNextRank { get; set; }

    /// <summary>Paged branch rows.</summary>
    public BranchPagedData Branches { get; set; } = new();
}

/// <summary>Simple paged wrapper used by the branches endpoint (not the global PagedResult&lt;T&gt;).</summary>
public class BranchPagedData
{
    public List<BranchItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>One row in the branches table — one direct sponsored ambassador.</summary>
public class BranchItemDto
{
    public string MemberId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>Raw enrollment points accumulated by this branch subtree.</summary>
    public int TotalPoints { get; set; }

    /// <summary>Points capped by the current-rank leg limit (MaxEnrollmentTeamPointsPerBranch * required TeamPoints).</summary>
    public int EligibleCurrentRank { get; set; }

    /// <summary>Points capped by the next-rank leg limit.</summary>
    public int EligibleNextRank { get; set; }

    /// <summary>0-100 percentage for the donut circle — EligibleCurrentRank as % of current-rank cap.</summary>
    public int EligibleCurrentPct { get; set; }

    /// <summary>0-100 percentage for the donut circle — EligibleNextRank as % of next-rank cap.</summary>
    public int EligibleNextPct { get; set; }
}
