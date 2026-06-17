/*
================================================================================
  fix-malformed-dualteam-paths.sql
  Sprint-15 follow-up — Bug 10 data-hygiene script.

  PURPOSE
  -------
  Identify and (optionally) repair rows in DualTeamTree whose HierarchyPath
  has two member IDs concatenated without a separating slash, e.g.:

      /AMB-700829/AMB-375401AMB-610895/      ← Bug 10 (missing internal '/')

  Root cause: three placement code paths historically did
      $"{parentPath}{memberId}/"
  assuming parentPath always ended in '/'. When the parent row's
  HierarchyPath had been written without a trailing slash, the new child
  glued IDs together and every subsequent StartsWith('/AMB-X/') subtree
  query missed those rows.

  The code paths are now fixed (TrimEnd('/') + '/'). This script exists ONLY
  to repair the legacy rows already in the database.

  USAGE — DETECT-ONLY (default; safe; modifies nothing)
  -----------------------------------------------------
  Run this script as-is in SSMS or sqlcmd. It will:
    1. Print the count of suspect rows (LIKE-matched).
    2. Show the top 50 of them so you can sanity-check.
    3. NOT change any data. The UPDATE is intentionally inside a
       BEGIN TRAN ... ROLLBACK block so a casual run cannot mutate
       production accidentally.

  USAGE — APPLY (if you want to actually fix them)
  ------------------------------------------------
  T-SQL is poor at string-rewriting paths with arbitrary-length ID lists.
  For the actual repair use the companion C# tool, which uses a regex to
  rewrite each row deterministically:

      cd scripts/FixDualTeamPaths
      dotnet run                              # detect-only
      COMMIT_FOR_REAL=1 dotnet run            # apply (transactional, asks
                                              # for COMMIT confirmation)

  Either way the repair is opt-in — nothing in this folder auto-applies.
================================================================================
*/

SET NOCOUNT ON;

-- ---------------------------------------------------------------------------
-- 1. DETECT
-- ---------------------------------------------------------------------------
-- LIKE pattern: a digit immediately followed by an ID prefix means we have a
-- concatenation with no slash. Brackets escape the literal dash in T-SQL.
DECLARE @MalformedCount INT;
SELECT  @MalformedCount = COUNT(*)
FROM    DualTeamTree
WHERE   HierarchyPath LIKE '%[0-9]AMB-%'
   OR   HierarchyPath LIKE '%[0-9]MBR-%';

PRINT  '------------------------------------------------------------------';
PRINT  ' DualTeamTree.HierarchyPath malformed-row scan (Sprint-15 Bug 10)';
PRINT  '------------------------------------------------------------------';
PRINT  CONCAT(' Rows matched by LIKE filter : ', @MalformedCount);
PRINT  '';
PRINT  ' This script is DETECT-ONLY. To repair, run the C# companion:';
PRINT  '   scripts/FixDualTeamPaths/Program.cs';
PRINT  '   COMMIT_FOR_REAL=1 dotnet run --project scripts/FixDualTeamPaths';
PRINT  '------------------------------------------------------------------';

-- Show up to 50 examples for human review.
SELECT TOP 50
        Id,
        MemberId,
        ParentMemberId,
        Side,
        HierarchyPath
FROM    DualTeamTree
WHERE   HierarchyPath LIKE '%[0-9]AMB-%'
   OR   HierarchyPath LIKE '%[0-9]MBR-%'
ORDER BY LEN(HierarchyPath) DESC;     -- longest first — most-broken on top

-- ---------------------------------------------------------------------------
-- 2. APPLY (intentionally rolled back — see note above)
-- ---------------------------------------------------------------------------
-- The block below demonstrates the shape of an UPDATE but does NOT save
-- changes. It is wrapped in a transaction and unconditionally rolled back
-- so accidentally pressing F5 cannot mutate the table.
--
-- A robust path rewrite needs a tokenizer (split by '/', then split each
-- segment at every "AMB-" / "MBR-" / "ROOT" boundary, re-emit with '/').
-- T-SQL cannot do that cleanly without a CLR function. Use the C# tool.

IF @MalformedCount > 0
BEGIN
    BEGIN TRAN FixDualTeamPaths_Preview;

    -- The repair belongs in code (see header). Leaving a placeholder UPDATE
    -- here intentionally throws — if someone removes the ROLLBACK and runs
    -- this without the C# tool, they'll see exactly what's missing.
    PRINT ' (skipping in-script UPDATE — use the C# tool to repair)';

    ROLLBACK TRAN FixDualTeamPaths_Preview;
    PRINT  ' Transaction rolled back. No data changed.';
END
ELSE
BEGIN
    PRINT ' Nothing to repair.';
END
