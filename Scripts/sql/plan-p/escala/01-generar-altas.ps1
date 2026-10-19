<#
Plan P, Tarea 18 (Bloque D). Orquestador del generador de altas COMPLETAS en MLMConquerorGlobalEditionDb_Escala.

Genera N altas de embajador bajo ESC-M, mas la raiz provisional ESC-P con 50.000 en su equipo, por conjuntos en SQL
(1.000.000 de altas por la API serian mas de 11 dias). Cada alta deja exactamente las filas de un alta real pagada por
Entrax y colocada despues por el motor (lista y contraste con la base local en el Plan P, Bloque D, «Que deja un alta
real»). Forma de los datos:
  - Binario: raiz ESC-0; ESC-M a su izquierda; bajo ESC-M una columna de 8.500 niveles por la izquierda (la cadena de
    derrames) y, de cada nodo de la columna, por la derecha, un subarbol completo por niveles hasta sumar N. ESC-P es una
    raiz provisional (sin padre) con un arbol completo de 50.000 nodos.
  - Inscripcion: independiente del binario. Cada patrocinador inscribe de 0 a 10 (45 % ninguno, media 1,47, pocos muy
    productivos; hash MD5 determinista), en anchura, hasta sumar N bajo ESC-M, mas una rama larga de 400 niveles colgada
    del nodo mas hondo (unos 440 niveles en la rama mas larga); mas ESC-P, inscrito por ESC-2 (cabeza de la columna binaria), con 49.999 debajo.
  - n = orden de alta (fechas crecientes en los ultimos 24 meses, hora del servidor): todo patrocinador y todo padre
    binario es anterior a sus hijos. Producto por n (VIP, Elite, Turbo del catalogo real) mas Lifestyle Ambassador.
  - Contrasena de todos los usuarios sinteticos: Escala2026! (un hash V3 generado para esta base, no copiado de nadie).

Fases (Scripts/sql/plan-p/escala/altas), con tablas de trabajo en el esquema escala que z-cerrar.sql borra:
  a-preparar (serie)  ->  en paralelo:  [b-identidad -> c-pedidos -> d-cobros]
                                        [e-inscripcion -> f-ascendientes Cola | Deltas | Avisos (a la vez)]
                                        [g-binario]
                      ->  z-cerrar -> ..\03-relleno-clave-binario.sql -> ..\04-contadores-y-colas.sql
                      ->  sp_updatestats -> 02-verificar-escala.sql
Solo toca MLMConquerorGlobalEditionDb_Escala: cada fichero SQL lo comprueba (DB_NAME) y exige -v Confirmo=ESCALA.

  powershell -File Scripts/sql/plan-p/escala/01-generar-altas.ps1 -Nodos 400000 -Salida C:\temp\escala-400k.txt [-Recrear]

-Recrear borra la base de escala (SOLO esa) y la vuelve a crear con 00-crear-bd-escala.sql antes de generar.
#>
param([Parameter(Mandatory)][int]$Nodos, [Parameter(Mandatory)][string]$Salida, [switch]$Recrear)
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
    $out.Trim() -split "`r?`n" | ? { $_ -and $_ -notmatch 'join order has been enforced' } | % { "    $_" } | Out-File $Salida -Append -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Fallo $fichero (sqlcmd $LASTEXITCODE)" }
}
"" | Out-File $Salida -Encoding utf8
if ($Recrear) {
    Log "Borrar $base (solo esa) y crearla con 00"
    & sqlcmd -S . -d master -E -C -b -Q "IF DB_ID(N'$base') IS NOT NULL BEGIN ALTER DATABASE [$base] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$base]; END" | Out-Null
    $o = & sqlcmd -S . -d master -E -C -b -I -W -v Confirmo=ESCALA -i (Join-Path $PSScriptRoot '00-crear-bd-escala.sql') 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Log $o; throw 'Fallo 00' }
}
Log "a-preparar (N = $Nodos)"
Sql (Join-Path $fases 'a-preparar.sql') @('-v', "Nodos=$Nodos")
Log 'a-preparar hecho; lanzo en paralelo b-c-d, e-f y g'

$job = {
    param($raiz, $base, $fases, $salida, $lista)
    Set-Location $raiz
    foreach ($f in $lista) {
        $t = Get-Date
        $extra = @(); $nombre = $f
        if ($f -like 'f-ascendientes:*') { $extra = @('-v', "Tabla=$($f.Split(':')[1])"); $nombre = 'f-ascendientes.sql' }
        $sa = @('-S', '.', '-d', $base, '-E', '-C', '-b', '-I', '-W', '-v', 'Confirmo=ESCALA') + $extra + @('-i', (Join-Path $fases $nombre))
        $out = & sqlcmd @sa 2>&1 | Out-String
        $ok = $LASTEXITCODE -eq 0
        "{0} [{1:0} s] {2}`n{3}" -f $f, ((Get-Date) - $t).TotalSeconds, ($(if ($ok) { 'ok' } else { 'FALLO' })), (($out -split "`r?`n" | ? { $_ -and $_ -notmatch 'join order' }) -join "`n")
        if (-not $ok) { throw "Fallo $f" }
    }
}
$j1 = Start-Job $job -ArgumentList $raiz, $base, $fases, $Salida, @('b-identidad.sql', 'c-pedidos.sql', 'd-cobros.sql')
$j2 = Start-Job $job -ArgumentList $raiz, $base, $fases, $Salida, @('e-inscripcion.sql')
$j3 = Start-Job $job -ArgumentList $raiz, $base, $fases, $Salida, @('g-binario.sql')
Wait-Job $j2 | Out-Null
Receive-Job $j2 -ErrorAction Continue | Out-File $Salida -Append -Encoding utf8
if ($j2.State -ne 'Completed') { Log 'FALLO e-inscripcion'; throw 'Fallo e' }
Log 'e-inscripcion hecho; lanzo f-ascendientes (Cola, Deltas, Avisos) a la vez'
$jf = 'Cola', 'Deltas', 'Avisos' | % { Start-Job $job -ArgumentList $raiz, $base, $fases, $Salida, @("f-ascendientes:$_") }
$todos = @($j1, $j3) + $jf
Wait-Job $todos | Out-Null
foreach ($j in $todos) { Receive-Job $j -ErrorAction Continue | Out-File $Salida -Append -Encoding utf8 }
if ($todos | ? { $_.State -ne 'Completed' }) { Log 'FALLO en una fase paralela'; throw 'Fallo paralelo' }
Log 'fases paralelas hechas'
Sql (Join-Path $fases 'z-cerrar.sql');                                   Log 'z-cerrar hecho'
Sql (Join-Path $PSScriptRoot '..\03-relleno-clave-binario.sql');         Log '03 hecho'
Sql (Join-Path $PSScriptRoot '..\04-contadores-y-colas.sql');            Log '04 hecho'
& sqlcmd -S . -d $base -E -C -b -Q "EXEC sp_updatestats" | Out-Null;     Log 'estadisticas actualizadas'
Log "GENERACION TERMINADA en $([int]((Get-Date) - $t0).TotalSeconds) s"
try { Sql (Join-Path $PSScriptRoot '02-verificar-escala.sql'); Log 'VERIFICACION OK' } catch { Log "VERIFICACION FALLA: $_" }
& sqlcmd -S . -d $base -E -C -W -Q "SET NOCOUNT ON; SELECT name, size/128 AS Mb FROM sys.database_files" | Out-File $Salida -Append -Encoding utf8
Log 'FIN'
