<#
Plan Q, Bloque 7 (desviacion pedida por Nick: la base de escala crece en el sitio). Anade altas completas a una base de
escala YA GENERADA con 01-generar-altas.ps1, sin borrarla, recrearla ni pasarla a un solo usuario: los servicios pueden
estar trabajando contra ella. Repite altas\anadir-lote.sql (lotes de -Lote altas, cada uno calculado primero y escrito
despues por fases cortas; coherente al terminar su ultima fase) hasta que el equipo binario de ESC-M llegue a -Hasta. Un
lote que falla a medias se reanuda (el script continua las fases que faltan del mismo lote) hasta 3 veces. Se puede
cortar y volver a lanzar: sigue desde lo que haya, el lote a medias incluido.

Despues: altas\h-rangos.sql (los rangos que la evaluacion anadiria con los equipos nuevos, solo por encima del rango de
cada miembro), estadisticas de los indices y 02-verificar-escala.sql.

  powershell -File Scripts/sql/plan-p/escala/03-anadir-altas.ps1 -Hasta 1000000 -Salida C:\temp\escala-anadir-1m.txt [-Lote 50000]

#>
param([Parameter(Mandatory)][int]$Hasta, [Parameter(Mandatory)][string]$Salida, [int]$Lote = 50000)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')
Set-Location $raiz
$base = 'MLMConquerorGlobalEditionDb_Escala'
$fases = Join-Path $PSScriptRoot 'altas'
$t0 = Get-Date
function Log($m) { $l = "[{0:HH:mm:ss} +{1,5:0} s] {2}" -f (Get-Date), ((Get-Date) - $t0).TotalSeconds, $m; $l | Out-File $Salida -Append -Encoding utf8; Write-Host $l }
function Sql($fichero, $extra = @()) {
    $sa = @('-S', '.', '-d', $base, '-E', '-C', '-b', '-I', '-W', '-v', 'Confirmo=ESCALA') + $extra + @('-i', $fichero)
    $out = & sqlcmd @sa 2>&1 | Out-String
    $out.Trim() -split "`r?`n" | ? { $_ } | % { "    $_" } | Out-File $Salida -Append -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Fallo $fichero (sqlcmd $LASTEXITCODE)" }
}
function EquipoEscM {
    $v = & sqlcmd -S . -d $base -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT LeftLegCount + RightLegCount FROM DualTeamTree WHERE MemberId = N'ESC-M' AND IsDeleted = 0"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer el equipo de ESC-M' }
    [int](($v | ? { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1).Trim())
}
"" | Out-File $Salida -Append -Encoding utf8
Log ("Libre en C: {0:N0} GB" -f ((Get-PSDrive C).Free / 1GB))
$equipo = EquipoEscM
Log "Equipo binario de ESC-M: $equipo; objetivo $Hasta"
while ($equipo -lt $Hasta) {
    $n = [Math]::Min($Lote, $Hasta - $equipo)
    $intento = 1
    while ($true) {
        try { Sql (Join-Path $fases 'anadir-lote.sql') @('-v', "Altas=$n"); break }
        catch {
            Log "Lote fallido (intento $intento): $_"
            if ($intento -ge 3) { throw }
            $intento++
        }
    }
    $equipo = EquipoEscM
    Log "Lote de $n hecho: equipo binario de ESC-M $equipo"
}
Sql (Join-Path $fases 'h-rangos.sql');                                     Log 'h-rangos hecho (rangos nuevos)'
& sqlcmd -S . -d $base -E -C -b -Q "EXEC sp_updatestats" | Out-Null;     Log 'estadisticas actualizadas'
Log "ANADIDO en $([int]((Get-Date) - $t0).TotalSeconds) s"
try { Sql (Join-Path $PSScriptRoot '02-verificar-escala.sql'); Log 'VERIFICACION OK' } catch { Log "VERIFICACION FALLA: $_" }
& sqlcmd -S . -d $base -E -C -W -Q "SET NOCOUNT ON; SELECT name, size/128 AS Mb FROM sys.database_files" | Out-File $Salida -Append -Encoding utf8
Log 'FIN'
