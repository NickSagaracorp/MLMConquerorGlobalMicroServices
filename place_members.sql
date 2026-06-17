-- Place all unplaced ambassadors (newly created in this session) into A's R-leg subtree.
-- Strategy: BFS-fill the shallowest open slots in AMB-375401's R-leg subtree to keep paths short
-- (max index entry = 1700 bytes). After placement, recomputes leg points.

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;

-- 1. Collect unplaced new members.
DECLARE @ToPlace TABLE (Seq int IDENTITY(1,1) PRIMARY KEY, MemberId nvarchar(50));

INSERT INTO @ToPlace (MemberId)
SELECT m.MemberId
FROM MemberProfiles m
LEFT JOIN DualTeamTree d ON d.MemberId = m.MemberId
WHERE m.SponsorMemberId IS NOT NULL
  AND m.MemberType = 0
  AND m.CreationDate > DATEADD(MINUTE, -30, GETDATE())
  AND d.MemberId IS NULL
  AND m.IsDeleted = 0
ORDER BY m.CreationDate, m.MemberId;

DECLARE @Total int = (SELECT COUNT(*) FROM @ToPlace);
PRINT CONCAT('Found ', @Total, ' unplaced new members.');

-- 2. Per-row: find the shallowest open slot in R-leg subtree of AMB-375401, place there.
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @i int = 1;
DECLARE @CurId nvarchar(50);
DECLARE @ParentId nvarchar(50);
DECLARE @ParentPath nvarchar(900);
DECLARE @NewPath nvarchar(900);
DECLARE @TargetSide int;
DECLARE @Placed int = 0;
DECLARE @SkippedTooLong int = 0;

WHILE @i <= @Total
BEGIN
    SELECT @CurId = MemberId FROM @ToPlace WHERE Seq = @i;

    -- Find shallowest node in AMB-375401's R-leg subtree that has at least one open child slot.
    -- Prefer LEFT slot first (BFS-style), then RIGHT.
    SET @ParentId = NULL;
    SET @ParentPath = NULL;
    SET @TargetSide = NULL;

    -- Try LEFT slot first
    SELECT TOP 1 @ParentId = d.MemberId, @ParentPath = d.HierarchyPath, @TargetSide = 0
    FROM DualTeamTree d
    WHERE (d.HierarchyPath LIKE '/AMB-700829/AMB-375401%' OR d.MemberId = 'AMB-375401')
      AND d.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM DualTeamTree c WHERE c.ParentMemberId = d.MemberId AND c.Side = 0 AND c.IsDeleted = 0)
    ORDER BY LEN(d.HierarchyPath) ASC, d.MemberId;

    -- If no LEFT slot, try RIGHT slot
    IF @ParentId IS NULL
    BEGIN
        SELECT TOP 1 @ParentId = d.MemberId, @ParentPath = d.HierarchyPath, @TargetSide = 1
        FROM DualTeamTree d
        WHERE (d.HierarchyPath LIKE '/AMB-700829/AMB-375401%' OR d.MemberId = 'AMB-375401')
          AND d.IsDeleted = 0
          AND NOT EXISTS (SELECT 1 FROM DualTeamTree c WHERE c.ParentMemberId = d.MemberId AND c.Side = 1 AND c.IsDeleted = 0)
        ORDER BY LEN(d.HierarchyPath) ASC, d.MemberId;
    END;

    IF @ParentId IS NULL
    BEGIN
        PRINT CONCAT('No open slot in R-leg for member ', @CurId, '. Stopping.');
        BREAK;
    END;

    -- Normalise path
    IF RIGHT(@ParentPath, 1) <> '/'
        SET @ParentPath = @ParentPath + '/';

    SET @NewPath = @ParentPath + @CurId + '/';

    -- Index allows up to 1700 bytes => nvarchar = ~850 chars. Skip if too long.
    IF LEN(@NewPath) * 2 > 1700
    BEGIN
        SET @SkippedTooLong = @SkippedTooLong + 1;
        SET @i = @i + 1;
        CONTINUE;
    END;

    INSERT INTO DualTeamTree (Id, MemberId, ParentMemberId, Side, HierarchyPath,
                              LeftLegPoints, RightLegPoints,
                              CreationDate, CreatedBy, LastUpdateDate, LastUpdateBy, IsDeleted)
    VALUES (LOWER(CONVERT(nvarchar(36), NEWID())), @CurId, @ParentId, @TargetSide, @NewPath,
            0, 0,
            @Now, 'distributed-wave', @Now, 'distributed-wave', 0);

    INSERT INTO PlacementLogs (MemberId, PlacedUnderMemberId, Side, Action, Reason,
                               UnplacementCount, FirstPlacementDate, CreationDate, CreatedBy)
    VALUES (@CurId, @ParentId, @TargetSide, 'AutoPlaced',
            'Distributed-wave bulk placement on R-leg of AMB-700829', 0, @Now, @Now, 'distributed-wave');

    SET @Placed = @Placed + 1;
    SET @i = @i + 1;
END;

PRINT CONCAT('Placed: ', @Placed, '  Skipped (path too long): ', @SkippedTooLong);

-- 4. Recompute all leg points (same SQL the AutoPlacementJob runs).
;WITH leg_sums AS (
    SELECT
        leg_root.ParentMemberId AS NodeMemberId,
        leg_root.Side,
        SUM(s.PersonalPoints) AS LegSum
    FROM DualTeamTree leg_root
    INNER JOIN DualTeamTree subtree
        ON subtree.HierarchyPath LIKE leg_root.HierarchyPath + N'%'
    INNER JOIN MemberStatistics s
        ON s.MemberId = subtree.MemberId
    WHERE leg_root.ParentMemberId IS NOT NULL
    GROUP BY leg_root.ParentMemberId, leg_root.Side
)
UPDATE d
SET LeftLegPoints  = COALESCE((SELECT LegSum FROM leg_sums WHERE NodeMemberId = d.MemberId AND Side = 0), 0),
    RightLegPoints = COALESCE((SELECT LegSum FROM leg_sums WHERE NodeMemberId = d.MemberId AND Side = 1), 0)
FROM DualTeamTree d;

UPDATE s
SET DualTeamPoints = CAST(COALESCE(d.LeftLegPoints, 0) + COALESCE(d.RightLegPoints, 0) AS int)
FROM MemberStatistics s
INNER JOIN DualTeamTree d ON d.MemberId = s.MemberId;

PRINT 'Recomputed leg points.';

COMMIT TRANSACTION;
PRINT 'Done.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT CONCAT('ERROR: ', ERROR_MESSAGE());
    THROW;
END CATCH;

-- 5. Report A's new state.
SELECT 'after_place' AS Phase, m.MemberId, s.EnrollmentPoints, s.EnrollmentTeamSize,
       d.LeftLegPoints AS DT_L_raw, d.RightLegPoints AS DT_R_raw,
       CAST(d.LeftLegPoints + d.RightLegPoints AS int) AS DT_Sum_raw,
       (SELECT TOP 1 r.SortOrder FROM MemberRankHistories h JOIN RankDefinitions r ON r.Id=h.RankDefinitionId WHERE h.MemberId=m.MemberId AND h.IsDeleted=0 ORDER BY r.SortOrder DESC) AS CurrRank
FROM MemberProfiles m
LEFT JOIN MemberStatistics s ON s.MemberId = m.MemberId
LEFT JOIN DualTeamTree d ON d.MemberId = m.MemberId
WHERE m.MemberId = 'AMB-700829';
