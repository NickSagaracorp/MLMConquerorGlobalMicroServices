-- Revision del bloque 3 del Plan Q: las cotas exclusivas (@lo, @hi) del subarbol de la clave @k, como
-- DualBinaryPath.SubtreeBounds (Plan P, Tarea 9). El equipo de un nodo es BinaryPath > @lo AND BinaryPath < @hi
-- (un Index Seek sobre UX_DualTeamTree_BinaryPath, el nodo incluido), en lugar de expandir los ascendientes de cada
-- fila con dbo.DualAncestorKeys. Entrada: @k varbinary(1700) declarada antes (NULL si el nodo no existe: no sale nada).
-- La usan los scripts de desarrollo (backfill-*.ps1, sql/leg-sponsors.ps1, sql/right-sponsors.ps1, sql/perf-diag.ps1);
-- SqlServerCotasDelSubarbolEnSqlTests exige que coincida con DualBinaryPath.SubtreeBounds.
DECLARE @kLen int = DATALENGTH(@k);
DECLARE @kUlt int = CAST(SUBSTRING(@k, @kLen, 1) AS int);
DECLARE @kTer int = @kUlt & (-@kUlt);                       -- el terminador: el bit 1 mas bajo del ultimo byte
DECLARE @lo varbinary(1700) = SUBSTRING(@k, 1, @kLen - 1) + CAST(CAST(@kUlt - @kTer AS binary(1)) AS varbinary(1));
DECLARE @hi varbinary(1700) = @lo, @kPos int, @kSuma int, @kVal int;
-- @hi = @lo + 1 en el ultimo bit del camino (con profundidad 0, el ultimo bit del ordinal de la raiz), con acarreo.
IF @kTer < 128 SELECT @kPos = @kLen, @kSuma = @kTer * 2 ELSE SELECT @kPos = @kLen - 1, @kSuma = 1;
WHILE @kPos >= 1
BEGIN
    SET @kVal = CAST(SUBSTRING(@hi, @kPos, 1) AS int) + @kSuma;
    SET @hi = SUBSTRING(@hi, 1, @kPos - 1) + CAST(CAST(@kVal % 256 AS binary(1)) AS varbinary(1)) + SUBSTRING(@hi, @kPos + 1, 1700);
    IF @kVal < 256 BREAK;
    SELECT @kSuma = 1, @kPos = @kPos - 1;
END;
