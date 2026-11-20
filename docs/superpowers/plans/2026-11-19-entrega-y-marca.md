# Entrega al cliente y PDFs con la marca de Sagara — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** (1) Un script que arma entregas para el remoto del cliente como fotos sin historial, sin `.md`, pruebas, documentación interna, recetas de despliegue ni rastro de agentes, y que no empuja sin el OK de Nick. (2) Todos los PDFs de especificaciones, planes, reglas y guías con la marca de Sagara Media Group.

**Architecture:** `docs/entrega/` contiene la lista de exclusiones, un `preparar.py` (prepara y revisa la copia exportada, con pruebas `unittest`) y `entregar.sh` (pruebas en un worktree del commit, `git archive`, preparar, revisar, compilar, commit en la rama de entrega, y empujar solo en una segunda llamada explícita). `docs/build_pdf.py` pasa a una plantilla con portada, encabezado con logo y las tipografías y colores de la marca guardados en `docs/marca/`; `docs/generar-pdfs.sh` regenera un PDF por cada `.md`.

**Tech Stack:** bash (Git Bash), Python 3.13 con reportlab (ya instalado) y `unittest`, Chrome sin ventana para convertir el logo, git, .NET 10 SDK.

**Diseño:** `docs/superpowers/specs/2026-11-19-entrega-y-marca-design.md`.

**Reglas:** commits en español sin acentos y sin firma; no se agrega el `.gitignore` de Nick ni `Proceso ImportarData OldSystem/`; nada se empuja a ningún remoto sin OK explícito de Nick; los PDFs se agregan con `git add -f`.

---

## Mapa de archivos

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `MLMConquerorGlobalEdition.AdminAPI/Features/Members/GetMemberStats/GetMemberStatsHandler.cs` | Modificar | Quitar "per CLAUDE.md" del comentario |
| `MLMConquerorGlobalEdition.Repository/Migrations/20260405130705_HelpdeskModule.cs` | Modificar | Quitar la ruta del plan del comentario |
| `docs/marca/logo.svg`, `logo.png`, `fuentes/*.ttf`, `fuentes/OFL-*.txt` | Crear | Recursos de marca |
| `docs/marca/logo-a-png.html` | Crear | Página mínima para convertir el SVG con Chrome |
| `docs/build_pdf.py` | Modificar | Plantilla de marca |
| `docs/generar-pdfs.sh` | Crear | Un PDF por cada `.md` interno |
| `docs/entrega/no-viaja.txt` | Crear | Lo que nunca viaja |
| `docs/entrega/excepciones.txt` | Crear | Archivos de terceros que pueden contener las palabras vigiladas |
| `docs/entrega/gitignore-cliente` | Crear | `.gitignore` que recibe el cliente |
| `docs/entrega/preparar.py` | Crear | Preparar y revisar la copia exportada |
| `docs/entrega/test_preparar.py` | Crear | Pruebas de `preparar.py` |
| `docs/entrega/entregar.sh` | Crear | El proceso completo de entrega |

---

### Task 1: Limpiar los dos comentarios que delatan el método

**Files:**
- Modify: `MLMConquerorGlobalEdition.AdminAPI/Features/Members/GetMemberStats/GetMemberStatsHandler.cs:40`
- Modify: `MLMConquerorGlobalEdition.Repository/Migrations/20260405130705_HelpdeskModule.cs:14`

- [ ] **Step 1: GetMemberStatsHandler**

La línea 40 pasa de

```csharp
        // IDateTimeProvider per CLAUDE.md — never DateTime.UtcNow directly.
```

a

```csharp
        // IDateTimeProvider: la hora sale siempre del proveedor, nunca de DateTime.UtcNow directo.
```

- [ ] **Step 2: Migración HelpdeskModule**

Leer el comentario completo (líneas 10 a 25). En la línea 14 se quita el paréntesis con la ruta del plan, dejando el resto del texto igual. Por ejemplo `Plan Q, Bloque 6 (DQ11, docs/superpowers/plans/2026-10-19-plan-q-escala-binario-y-reportes.md): en una base` pasa a `Bloque 6 (DQ11): en una base`. Es solo un comentario: no cambia la migración.

- [ ] **Step 3: Comprobar que no queda nada**

Run: `git grep -in "superpowers\|claude\|anthropic\|AGENTS\.md" -- . ':!docs' ':!*.md' ':!Scripts' ':!*.Tests' ':!*remixicon*' ':!.gitignore' ':!.dockerignore'`
Expected: ninguna línea.

Run: `dotnet build MLMConquerorGlobalEdition.AdminAPI -v q` y `dotnet build MLMConquerorGlobalEdition.Repository -v q`
Expected: `0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add MLMConquerorGlobalEdition.AdminAPI/Features/Members/GetMemberStats/GetMemberStatsHandler.cs MLMConquerorGlobalEdition.Repository/Migrations/20260405130705_HelpdeskModule.cs
git commit -m "Comentarios: la regla de la hora del proveedor se explica en el propio comentario y la migracion de Helpdesk deja de citar la ruta de un documento interno"
```

---

### Task 2: Recursos de marca (`docs/marca/`)

**Files:**
- Create: `docs/marca/logo.svg`, `docs/marca/logo.png`, `docs/marca/logo-a-png.html`
- Create: `docs/marca/fuentes/` (7 `.ttf` y 3 licencias)

- [ ] **Step 1: Copiar el logo**

```bash
mkdir -p docs/marca/fuentes
cp "/c/Users/sagar/Downloads/sagaramediagroup-html/sagaramediagroup.com/assets/img/logo.svg" docs/marca/logo.svg
```

- [ ] **Step 2: Convertir el logo a PNG**

`docs/marca/logo-a-png.html`:

```html
<!doctype html>
<meta charset="utf-8">
<style>html,body{margin:0;background:transparent}img{display:block;width:2400px;height:auto}</style>
<img src="logo.svg" alt="">
```

El SVG mide 750.39 × 197.341, así que a 2400 px de ancho la altura es 631 px:

```bash
cd docs/marca
"/c/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --disable-gpu \
  --hide-scrollbars --default-background-color=00000000 --window-size=2400,631 \
  --screenshot="$(cygpath -w "$PWD/logo.png")" "file:///$(cygpath -m "$PWD/logo-a-png.html")"
cd ../..
python -I -c "from PIL import Image; im=Image.open('docs/marca/logo.png'); print(im.size, im.mode)" 2>/dev/null \
  || python -I -c "import struct;d=open('docs/marca/logo.png','rb').read(24);print(struct.unpack('>II',d[16:24]))"
```

Expected: `(2400, 631)`. Abrir `docs/marca/logo.png` con la herramienta de lectura de imágenes y comprobar que se ve el logo con el degradado, sin bordes recortados y con fondo transparente. Si el fondo sale blanco, se acepta: la portada y el encabezado son blancos.

- [ ] **Step 3: Descargar las tipografías (licencia SIL Open Font License)**

```bash
t=$(mktemp -d)
curl -sL -o "$t/plex-sans.zip" "https://github.com/IBM/plex/releases/download/%40ibm/plex-sans%401.3.6/ibm-plex-sans.zip"
curl -sL -o "$t/space.zip" "https://github.com/floriankarsten/space-grotesk/releases/download/2.0.0/SpaceGrotesk-2.0.0.zip"
curl -sL -o docs/marca/fuentes/IBMPlexMono-Regular.ttf "https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/IBMPlexMono-Regular.ttf"
curl -sL -o docs/marca/fuentes/OFL-IBMPlexMono.txt "https://raw.githubusercontent.com/google/fonts/main/ofl/ibmplexmono/OFL.txt"
unzip -l "$t/plex-sans.zip" | grep -E "ttf/IBMPlexSans-(Regular|Bold|Italic|SemiBold)\.ttf|LICENSE"
unzip -l "$t/space.zip" | grep -E "static/SpaceGrotesk-(Bold|Medium)\.ttf|OFL"
```

Con las rutas que muestren los dos `unzip -l`, extraer a `docs/marca/fuentes/` (sin carpetas, `unzip -j`):
- `IBMPlexSans-Regular.ttf`, `IBMPlexSans-Bold.ttf`, `IBMPlexSans-Italic.ttf`, `IBMPlexSans-SemiBold.ttf` y la licencia como `OFL-IBMPlexSans.txt`;
- `SpaceGrotesk-Bold.ttf`, `SpaceGrotesk-Medium.ttf` y la licencia como `OFL-SpaceGrotesk.txt`.

Si alguno de los dos zips no trae TTF estáticos con esos nombres, avisar a Nick antes de usar fuentes variables (reportlab no maneja bien los ejes de una fuente variable).

Run: `ls docs/marca/fuentes`
Expected: 7 `.ttf` y 3 `OFL-*.txt`.

- [ ] **Step 4: Commit**

```bash
git add docs/marca
git commit -m "Marca de Sagara Media Group para los PDFs: logo (svg y png de 2400 px), IBM Plex Sans y Mono y Space Grotesk con sus licencias OFL"
```

---

### Task 3: Plantilla de marca en `docs/build_pdf.py`

**Files:**
- Modify: `docs/build_pdf.py`

Uso nuevo: `python -I docs/build_pdf.py <md> <pdf> "<título>" [tamaño-código] [subtítulo]`. El subtítulo por defecto es `MLM Conqueror Global Edition`.

- [ ] **Step 1: Tipografías y colores**

Reemplazar el bloque de colores (`INK`, `MUTED`, `RULE`, `BAND`) por:

```python
import os
from datetime import date
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.fonts import addMapping
from reportlab.platypus import NextPageTemplate, PageBreak

SUBTITLE = sys.argv[5] if len(sys.argv) > 5 else "MLM Conqueror Global Edition"
MARCA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "marca")
F = os.path.join(MARCA, "fuentes")
for nombre, archivo in [("Plex", "IBMPlexSans-Regular.ttf"), ("Plex-Bold", "IBMPlexSans-Bold.ttf"),
                        ("Plex-Italic", "IBMPlexSans-Italic.ttf"), ("Plex-Semi", "IBMPlexSans-SemiBold.ttf"),
                        ("PlexMono", "IBMPlexMono-Regular.ttf"), ("Grotesk", "SpaceGrotesk-Bold.ttf"),
                        ("Grotesk-Medium", "SpaceGrotesk-Medium.ttf")]:
    pdfmetrics.registerFont(TTFont(nombre, os.path.join(F, archivo)))
addMapping("Plex", 0, 0, "Plex"); addMapping("Plex", 1, 0, "Plex-Bold")
addMapping("Plex", 0, 1, "Plex-Italic"); addMapping("Plex", 1, 1, "Plex-Bold")
LOGO = os.path.join(MARCA, "logo.png")

# Colores de sagaramediagroup.com (assets/css/site.css)
NAVY      = colors.HexColor("#1c2f66")
NAVY_DEEP = colors.HexColor("#152350")
TEAL      = colors.HexColor("#3dadaf")
TEAL_INK  = colors.HexColor("#1f7f81")
INK       = colors.HexColor("#12172b")
MUTED     = colors.HexColor("#4b5265")
RULE      = colors.HexColor("#e1e5ee")
BAND      = colors.HexColor("#eef1f8")
```

y quitar el `import` duplicado de `os` si lo hubiera.

- [ ] **Step 2: Estilos**

En `S()` el `fontName` por defecto pasa a `"Plex"`. Los estilos pasan a:

```python
st = {
    "h1": S("h1", fontName="Grotesk", fontSize=18, leading=22, spaceBefore=16, spaceAfter=9, textColor=NAVY),
    "h2": S("h2", fontName="Grotesk", fontSize=13, leading=17, spaceBefore=13, spaceAfter=6, textColor=NAVY),
    "h3": S("h3", fontName="Grotesk-Medium", fontSize=10.8, leading=14, spaceBefore=10, spaceAfter=4, textColor=TEAL_INK),
    "p":  S("p"),
    "li": S("li", leftIndent=12, bulletIndent=3, spaceAfter=3),
    "q":  S("q", leftIndent=10, textColor=MUTED, fontName="Plex-Italic"),
    "li2": S("li2", leftIndent=26, bulletIndent=16, spaceAfter=3),
    "code": S("code", fontName="PlexMono", fontSize=CODE_PT, leading=CODE_PT * 1.37),
    "th": S("th", fontName="Plex-Semi", fontSize=8.8, leading=11.6, textColor=colors.white),
    "td": S("td", fontSize=8.8, leading=11.6),
    "portada_t": S("portada_t", fontName="Grotesk", fontSize=28, leading=33, textColor=NAVY, spaceAfter=10),
    "portada_s": S("portada_s", fontName="Grotesk-Medium", fontSize=13, leading=17, textColor=TEAL_INK),
}
```

En `inline()`, el código en línea pasa a `'<font face="PlexMono" size="8.4">%s</font>'`.

- [ ] **Step 3: Portada, encabezado y pie**

Reemplazar `header()` y la creación de `doc` por:

```python
def portada(c, d):
    c.saveState()
    c.setFillColor(NAVY)
    c.rect(0, 0, A4[0], 28 * mm, stroke=0, fill=1)
    c.setFillColor(TEAL)
    c.rect(0, 28 * mm, A4[0], 1.6 * mm, stroke=0, fill=1)
    ancho = 95 * mm
    c.drawImage(LOGO, 20 * mm, A4[1] - 55 * mm, width=ancho, height=ancho * 631 / 2400, mask="auto")
    c.setFont("Plex", 8.5)
    c.setFillColor(colors.white)
    c.drawString(20 * mm, 15 * mm, "Sagara Media Group")
    c.drawRightString(A4[0] - 20 * mm, 15 * mm, "Documento confidencial")
    c.restoreState()


def interior(c, d):
    c.saveState()
    alto = 7.5 * mm
    c.drawImage(LOGO, 20 * mm, A4[1] - 15.5 * mm, width=alto * 2400 / 631, height=alto, mask="auto")
    c.setFont("Plex", 7.4)
    c.setFillColor(MUTED)
    c.drawRightString(A4[0] - 20 * mm, A4[1] - 12.5 * mm, TITLE)
    c.setStrokeColor(TEAL)
    c.setLineWidth(.8)
    c.line(20 * mm, A4[1] - 17.5 * mm, A4[0] - 20 * mm, A4[1] - 17.5 * mm)
    c.setStrokeColor(RULE)
    c.setLineWidth(.4)
    c.line(20 * mm, 15 * mm, A4[0] - 20 * mm, 15 * mm)
    c.drawString(20 * mm, 10.5 * mm, "Confidencial · Sagara Media Group")
    c.drawRightString(A4[0] - 20 * mm, 10.5 * mm, str(c.getPageNumber()))
    c.restoreState()


doc = BaseDocTemplate(OUT, pagesize=A4, leftMargin=20 * mm, rightMargin=20 * mm,
                      topMargin=23 * mm, bottomMargin=20 * mm, title=TITLE,
                      author="Sagara Media Group", subject=SUBTITLE)
cuerpo = Frame(20 * mm, 20 * mm, A4[0] - 40 * mm, A4[1] - 43 * mm, id="n")
doc.addPageTemplates([
    PageTemplate(id="portada", frames=[Frame(20 * mm, 45 * mm, A4[0] - 40 * mm, A4[1] - 120 * mm, id="p")],
                 onPage=portada),
    PageTemplate(id="b", frames=[cuerpo], onPage=interior),
])

fl = [Spacer(1, 40 * mm), Paragraph(inline(TITLE), st["portada_t"]),
      Paragraph(inline(SUBTITLE), st["portada_s"]), Spacer(1, 8 * mm),
      Paragraph(date.today().strftime("%d-%m-%Y"), st["p"]),
      NextPageTemplate("b"), PageBreak()]
```

y donde antes empezaba `fl, i = [], 0`, queda `i = 0` (la lista `fl` ya tiene la portada). El cálculo de `per_page` de los bloques de código usa ahora `A4[1] - 43 * mm` en lugar de `A4[1] - 39 * mm`.

- [ ] **Step 4: Bloques de código y tablas con la marca**

En el `TableStyle` de los bloques de código, `("BOX", ...)` pasa a dos líneas: `("BOX", (0, 0), (-1, -1), .4, RULE)` y `("LINEBEFORE", (0, 0), (0, -1), 2.2, TEAL)`.

En el de las tablas, la fila de encabezado pasa a fondo `NAVY` (`("BACKGROUND", (0, 0), (-1, 0), NAVY)`), y se agrega `("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, BAND])`.

- [ ] **Step 5: Probar con una guía y revisarla a la vista**

```bash
python -I docs/build_pdf.py docs/deployment/despliegue-aws-eks.md "$TEMP/prueba-marca.pdf" "Despliegue en AWS EKS" 7.4
```

Expected: `escrito: ...prueba-marca.pdf`. Abrir con la herramienta de lectura de PDF las páginas 1 (portada) y 4 (interior con código y tabla) y comprobar: logo nítido con su degradado; títulos en azul marino; encabezado con logo y título; pie con "Confidencial · Sagara Media Group" y número; bloques de código con borde verde azulado; tablas con encabezado azul y texto blanco; acentos y "ñ" correctos.

- [ ] **Step 6: Commit**

```bash
git add docs/build_pdf.py
git commit -m "PDFs con la marca de Sagara Media Group: portada con logo, encabezado con logo y titulo, pie confidencial con numero de pagina, Space Grotesk en titulos, IBM Plex Sans en el texto e IBM Plex Mono en el codigo, colores de la web"
```

---

### Task 4: Un PDF por cada `.md` interno (`docs/generar-pdfs.sh`)

**Files:**
- Create: `docs/generar-pdfs.sh`

- [ ] **Step 1: El script**

```bash
#!/usr/bin/env bash
# Genera, junto a cada .md de docs/superpowers, docs/deployment y docs/arquitectura, su PDF
# oficial con la marca de Sagara Media Group. El titulo es la primera linea "# " del .md.
# Las guias de docs/deployment usan codigo mas pequeno (7.4) para que los comandos quepan.
# Uso (desde la raiz):  bash docs/generar-pdfs.sh [archivo.md ...]
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
archivos=("$@")
[ ${#archivos[@]} -gt 0 ] || mapfile -t archivos < <(git ls-files 'docs/superpowers/*.md' 'docs/deployment/*.md' 'docs/arquitectura/*.md')
for md in "${archivos[@]}"; do
  titulo=$(grep -m1 '^# ' "$md" | sed 's/^# //')
  [ -n "$titulo" ] || titulo=$(basename "$md" .md)
  case "$md" in docs/deployment/*) codigo=7.4 ;; *) codigo=8.3 ;; esac
  python -I docs/build_pdf.py "$md" "${md%.md}.pdf" "$titulo" "$codigo" >/dev/null
  echo "ok  ${md%.md}.pdf"
done
```

- [ ] **Step 2: Generar todos**

Run: `bash docs/generar-pdfs.sh`
Expected: una línea `ok` por cada `.md` (29 o más, contando los dos documentos nuevos de hoy). Si alguno falla con un error de reportlab (por ejemplo, una etiqueta mal cerrada por un asterisco), se corrige el `.md` o la función `inline()` y se vuelve a correr solo ese archivo.

- [ ] **Step 3: Revisar dos a la vista**

Abrir la portada y una página interior de `docs/superpowers/plans/2026-11-19-secretos-aws.pdf` y de `docs/arquitectura/<el primero que haya>.pdf`.

- [ ] **Step 4: Commit**

```bash
git add docs/generar-pdfs.sh
git add -f $(git ls-files 'docs/superpowers/*.md' 'docs/deployment/*.md' 'docs/arquitectura/*.md' | sed 's/\.md$/.pdf/')
git status --short | grep -v "^??" | grep -v "\.gitignore"
git commit -m "Un PDF oficial con la marca de Sagara por cada especificacion, plan, regla y guia interna (docs/generar-pdfs.sh); se quedan en el repositorio privado"
```

---

### Task 5: `preparar.py` y sus pruebas

**Files:**
- Create: `docs/entrega/no-viaja.txt`, `docs/entrega/excepciones.txt`, `docs/entrega/gitignore-cliente`
- Create: `docs/entrega/preparar.py`
- Test: `docs/entrega/test_preparar.py`

- [ ] **Step 1: Las tres listas**

`docs/entrega/no-viaja.txt`:

```
# Lo que NUNCA viaja al remoto del cliente. Un patron por linea, rutas relativas a la raiz:
#   termina en /   -> carpeta (en la raiz, o en cualquier nivel si empieza con **/)
#   sin /          -> nombre de archivo o carpeta en cualquier nivel (admite * y ?)
#   con /          -> ruta exacta desde la raiz (admite * y ?)
*.md
*.Tests/
docs/superpowers/
docs/arquitectura/
docs/presentations/
docs/deployment/
docs/marca/
docs/entrega/
docs/build_pdf.py
docs/generar-pdfs.sh
.claude/
CLAUDE.md
AGENTS.md
.cursor/
.cursorrules
.github/copilot-instructions.md
deploy/
Scripts/
DeploymentScripts/
Dockerfile
.dockerignore
.env.example
fix_helpdesk_tables.sql
Proceso ImportarData OldSystem/
```

`docs/entrega/excepciones.txt`:

```
# Archivos de terceros que pueden contener las palabras vigiladas sin delatar nada
# (Remix Icon trae iconos con nombres de productos). Mismos patrones que no-viaja.txt.
remixicon.css
remixicon.eot
remixicon.svg
remixicon.ttf
remixicon.woff
remixicon.woff2
remixicon.symbol.svg
```

`docs/entrega/gitignore-cliente`:

```
bin/
obj/
.vs/
*.user
*.suo
appsettings.*.json
!appsettings.json
**/wwwroot/uploads/
**/wwwroot/certificates/
.env
```

- [ ] **Step 2: Escribir las pruebas**

`docs/entrega/test_preparar.py`:

```python
import os, pathlib, shutil, tempfile, unittest
import preparar

AQUI = pathlib.Path(__file__).parent

SLNX = """<Solution>
  <Project Path="App.Api/App.Api.csproj" />
  <Project Path="App.Api.Tests/App.Api.Tests.csproj" />
  <Project Path="App.Web/App.Web.csproj" />
</Solution>
"""


class Base(unittest.TestCase):
    def setUp(self):
        self.raiz = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.raiz)
        self.no_viaja = preparar.leer_patrones(AQUI / "no-viaja.txt")
        self.excepciones = preparar.leer_patrones(AQUI / "excepciones.txt")

    def escribir(self, ruta, texto="x"):
        p = self.raiz / ruta
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(texto, encoding="utf-8")
        return p

    def existe(self, ruta):
        return (self.raiz / ruta).exists()


class Preparar(Base):
    def test_borra_lo_que_no_viaja_y_deja_lo_demas(self):
        for r in ["README.md", "App.Api/Notas.md", "App.Api.Tests/Prueba.cs", "docs/superpowers/plans/p.md",
                  "docs/arquitectura/a.pdf", "docs/build_pdf.py", "deploy/aws/x.sh", "Scripts/s.ps1",
                  "Dockerfile", ".claude/settings.json", "App.Api/Program.cs", "docs/billing/deck.pptx",
                  "docs/commissions/build-deck.js", "UI-Templates/index.html", "global.json"]:
            self.escribir(r)
        self.escribir("App.slnx", SLNX)

        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")

        for r in ["README.md", "App.Api/Notas.md", "App.Api.Tests", "docs/superpowers", "docs/arquitectura",
                  "docs/build_pdf.py", "deploy", "Scripts", "Dockerfile", ".claude"]:
            self.assertFalse(self.existe(r), r)
        for r in ["App.Api/Program.cs", "docs/billing/deck.pptx", "docs/commissions/build-deck.js",
                  "UI-Templates/index.html", "global.json"]:
            self.assertTrue(self.existe(r), r)

    def test_quita_las_pruebas_del_slnx(self):
        self.escribir("App.slnx", SLNX)
        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")
        texto = (self.raiz / "App.slnx").read_text(encoding="utf-8")
        self.assertNotIn("Tests", texto)
        self.assertIn("App.Api/App.Api.csproj", texto)
        self.assertIn("App.Web/App.Web.csproj", texto)

    def test_quita_internals_visible_to_hacia_pruebas(self):
        self.escribir("App.Api/App.Api.csproj",
                      '<Project>\n  <ItemGroup>\n    <InternalsVisibleTo Include="App.Api.Tests" />\n'
                      '    <InternalsVisibleTo Include="DynamicProxyGenAssembly2" />\n  </ItemGroup>\n</Project>\n')
        self.escribir("App.Api/Properties/AssemblyInfo.cs",
                      'using System.Runtime.CompilerServices;\n[assembly: InternalsVisibleTo("App.Api.Tests")]\n')
        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")
        csproj = (self.raiz / "App.Api/App.Api.csproj").read_text(encoding="utf-8")
        info = (self.raiz / "App.Api/Properties/AssemblyInfo.cs").read_text(encoding="utf-8")
        self.assertNotIn("App.Api.Tests", csproj)
        self.assertIn("DynamicProxyGenAssembly2", csproj)
        self.assertNotIn("Tests", info)

    def test_escribe_el_gitignore_del_cliente(self):
        self.escribir(".gitignore", ".claude/\nProceso ImportarData OldSystem/\n")
        preparar.preparar(self.raiz, self.no_viaja, AQUI / "gitignore-cliente")
        texto = (self.raiz / ".gitignore").read_text(encoding="utf-8")
        self.assertNotIn(".claude", texto)
        self.assertIn("bin/", texto)


class Revisar(Base):
    def problemas(self):
        return preparar.revisar(self.raiz, self.no_viaja, self.excepciones)

    def test_copia_limpia_no_tiene_problemas(self):
        self.escribir("App.Api/Program.cs", "var x = 1; // la hora sale del proveedor\n")
        self.escribir("App.slnx", '<Solution><Project Path="App.Api/App.Api.csproj" /></Solution>')
        self.assertEqual(self.problemas(), [])

    def test_un_md_es_un_problema(self):
        self.escribir("App.Api/LEEME.md")
        self.assertTrue(any("LEEME.md" in p for p in self.problemas()))

    def test_un_proyecto_de_pruebas_es_un_problema(self):
        self.escribir("App.slnx", '<Solution><Project Path="App.Api.Tests/App.Api.Tests.csproj" /></Solution>')
        self.assertTrue(any("Tests" in p for p in self.problemas()))

    def test_una_palabra_vigilada_en_el_contenido_es_un_problema(self):
        self.escribir("App.Api/Handler.cs", "// ver docs/superpowers/plans/x\n")
        self.escribir("App.Api/Otro.cs", "// per CLAUDE.md\n")
        p = self.problemas()
        self.assertTrue(any("Handler.cs" in x for x in p))
        self.assertTrue(any("Otro.cs" in x for x in p))

    def test_las_excepciones_no_son_problema(self):
        self.escribir("App.Web/wwwroot/assets/css/remixicon.css", ".ri-claude-fill:before{}")
        self.assertEqual(self.problemas(), [])

    def test_los_binarios_no_se_leen(self):
        p = self.raiz / "App.Web/wwwroot/img/logo.png"
        p.parent.mkdir(parents=True)
        p.write_bytes(b"\x89PNG\x00\x00claude")
        self.assertEqual(self.problemas(), [])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 3: Correr y ver que falla**

Run: `cd docs/entrega && python -I -m unittest test_preparar -v; cd ../..`
Expected: `ModuleNotFoundError: No module named 'preparar'` (con `-I` el directorio actual no está en la ruta; si pasa eso, correr como `python -m unittest` sin `-I` dentro de `docs/entrega`, que es código propio del repositorio).

- [ ] **Step 4: Implementar `preparar.py`**

```python
# -*- coding: utf-8 -*-
"""Prepara y revisa la copia exportada que se entrega al cliente.

  python preparar.py preparar <copia> <no-viaja.txt> <gitignore-cliente>
  python preparar.py revisar  <copia> <no-viaja.txt> <excepciones.txt>

revisar termina con codigo 1 y lista los problemas si encuentra algo que no debe viajar.
"""
import fnmatch, pathlib, re, shutil, sys

VIGILADAS = re.compile(r"claude|anthropic|superpowers|agents\.md", re.IGNORECASE)
INTERNALS_CSPROJ = re.compile(r'^\s*<InternalsVisibleTo\s+Include="[^"]*\.Tests"\s*/>\s*\r?\n', re.MULTILINE)
INTERNALS_CS = re.compile(r'^\s*\[assembly:\s*InternalsVisibleTo\("[^"]*\.Tests"\)\]\s*\r?\n', re.MULTILINE)
PROYECTO_PRUEBAS = re.compile(r'^\s*<Project\s+Path="[^"]*\.Tests/[^"]*"\s*/>\s*\r?\n', re.MULTILINE)


def leer_patrones(archivo):
    return [l.strip() for l in pathlib.Path(archivo).read_text(encoding="utf-8").splitlines()
            if l.strip() and not l.strip().startswith("#")]


def coincide(rel, patrones):
    """rel: ruta relativa con '/'. Ver el encabezado de no-viaja.txt."""
    partes = rel.split("/")
    for p in patrones:
        if p.endswith("/"):
            carpeta = p[:-1]
            if "/" in carpeta:
                if any(fnmatch.fnmatch("/".join(partes[:i]), carpeta) for i in range(1, len(partes) + 1)):
                    return True
            elif any(fnmatch.fnmatch(x, carpeta) for x in partes[:-1]) or fnmatch.fnmatch(partes[0], carpeta):
                return True
        elif "/" in p:
            if fnmatch.fnmatch(rel, p):
                return True
        elif any(fnmatch.fnmatch(x, p) for x in partes):
            return True
    return False


def archivos(raiz):
    for p in sorted(pathlib.Path(raiz).rglob("*")):
        if p.is_file():
            yield p, p.relative_to(raiz).as_posix()


def reescribir(p, patron):
    texto = p.read_text(encoding="utf-8-sig")
    nuevo = patron.sub("", texto)
    if nuevo != texto:
        p.write_text(nuevo, encoding="utf-8")


def preparar(raiz, no_viaja, gitignore_cliente):
    raiz = pathlib.Path(raiz)
    for p, rel in list(archivos(raiz)):
        if coincide(rel, no_viaja):
            p.unlink()
    for d in sorted((d for d in raiz.rglob("*") if d.is_dir()), key=lambda d: len(d.parts), reverse=True):
        if not any(d.iterdir()):
            d.rmdir()
    for slnx in raiz.glob("*.slnx"):
        reescribir(slnx, PROYECTO_PRUEBAS)
    for csproj in raiz.rglob("*.csproj"):
        reescribir(csproj, INTERNALS_CSPROJ)
    for cs in raiz.rglob("*.cs"):
        if "InternalsVisibleTo" in cs.read_text(encoding="utf-8-sig", errors="ignore"):
            reescribir(cs, INTERNALS_CS)
    shutil.copyfile(gitignore_cliente, raiz / ".gitignore")


def revisar(raiz, no_viaja, excepciones):
    problemas = []
    for p, rel in archivos(raiz):
        if coincide(rel, no_viaja):
            problemas.append(f"no debe viajar: {rel}")
            continue
        if coincide(rel, excepciones):
            continue
        datos = p.read_bytes()
        if b"\x00" in datos[:8000]:
            continue  # binario
        texto = datos.decode("utf-8", errors="ignore")
        if rel.endswith((".slnx", ".sln", ".csproj")) and ".Tests" in texto:
            problemas.append(f"referencia a pruebas: {rel}")
        m = VIGILADAS.search(texto)
        if m:
            linea = texto.count("\n", 0, m.start()) + 1
            problemas.append(f"palabra vigilada '{m.group(0)}': {rel}:{linea}")
    return problemas


def main(argv):
    modo, copia, lista, extra = argv[1:5]
    if modo == "preparar":
        preparar(copia, leer_patrones(lista), extra)
        return 0
    if modo == "revisar":
        problemas = revisar(copia, leer_patrones(lista), leer_patrones(extra))
        for p in problemas:
            print("  " + p)
        return 1 if problemas else 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
```

- [ ] **Step 5: Correr y ver que pasa**

Run: `cd docs/entrega && python -m unittest test_preparar -v; cd ../..`
Expected: `OK` con 10 pruebas.

- [ ] **Step 6: Commit**

```bash
git add docs/entrega/no-viaja.txt docs/entrega/excepciones.txt docs/entrega/gitignore-cliente docs/entrega/preparar.py docs/entrega/test_preparar.py
git commit -m "Entrega al cliente: lista de lo que no viaja, preparacion de la copia exportada (borra lo excluido, quita las pruebas del slnx y los InternalsVisibleTo hacia pruebas, gitignore limpio) y revision que falla con un md, una referencia a pruebas o una palabra vigilada en el contenido; con pruebas"
```

---

### Task 6: `entregar.sh`

**Files:**
- Create: `docs/entrega/entregar.sh`

- [ ] **Step 1: El script**

```bash
#!/usr/bin/env bash
# Arma una entrega para el remoto del cliente: una foto sin historial del commit indicado, sin
# nada de docs/entrega/no-viaja.txt. NO empuja: deja la entrega lista en una carpeta y dice
# como empujarla. Empujar es una segunda llamada explicita, solo con el OK de Nick.
#
#   bash docs/entrega/entregar.sh armar   <commit> <url-del-remoto> [rama]
#   bash docs/entrega/entregar.sh empujar <carpeta-que-dio-armar>
set -euo pipefail
export MSYS_NO_PATHCONV=1
RAIZ="$(git rev-parse --show-toplevel)"; DIR="$RAIZ/docs/entrega"
SLN="MLMConquerorGlobalEdition.slnx"

armar() {
  local commit="${1:?falta el commit}" remoto="${2:?falta la url del remoto}" rama="${3:-entrega}"
  local sha; sha=$(git -C "$RAIZ" rev-parse --verify "$commit^{commit}")
  local fecha; fecha=$(date +%F)
  local base; base="$HOME/mlm-entregas/$(date +%Y%m%d-%H%M%S)"
  mkdir -p "$base"
  echo "Entrega de ${sha:0:7} hacia $remoto ($rama) en $base"

  echo "1/6 Pruebas en el repositorio privado, sobre ese commit"
  git -C "$RAIZ" worktree add --detach "$base/fuente" "$sha" >/dev/null
  trap 'git -C "$RAIZ" worktree remove --force "$base/fuente" 2>/dev/null || true' RETURN
  for t in "$base"/fuente/*.Tests; do
    r=$(dotnet test "$t" -nologo -v q 2>&1 | grep -E "Passed!|Failed!" || true)
    echo "    $(basename "$t"): ${r:-sin resultado}"
    case "$r" in Passed!*) ;; *) echo "Una suite no paso: no se entrega." >&2; return 1 ;; esac
  done

  echo "2/6 Exportar el arbol sin historial"
  mkdir -p "$base/copia"
  git -C "$RAIZ" archive "$sha" | tar -x -C "$base/copia"

  echo "3/6 Preparar"
  python -I "$DIR/preparar.py" preparar "$base/copia" "$DIR/no-viaja.txt" "$DIR/gitignore-cliente"

  echo "4/6 Revisar"
  if ! python -I "$DIR/preparar.py" revisar "$base/copia" "$DIR/no-viaja.txt" "$DIR/excepciones.txt"; then
    echo "La copia tiene cosas que no deben viajar (arriba). No se entrega." >&2; return 1
  fi

  echo "5/6 Compilar la copia"
  cp -r "$base/copia" "$base/compilar"
  dotnet build "$base/compilar/$SLN" -nologo -v q 2>&1 | grep -E "error|Error\(s\)" | tail -3
  dotnet build "$base/compilar/$SLN" -nologo -v q >/dev/null 2>&1 || { echo "La copia no compila. No se entrega." >&2; return 1; }
  rm -rf "$base/compilar"

  echo "6/6 Commit de entrega"
  if git ls-remote --exit-code --heads "$remoto" "$rama" >/dev/null 2>&1; then
    git clone -q --branch "$rama" --single-branch "$remoto" "$base/entrega"
  else
    git init -q "$base/entrega"; git -C "$base/entrega" checkout -q --orphan "$rama"
    git -C "$base/entrega" remote add origin "$remoto"
  fi
  find "$base/entrega" -mindepth 1 -maxdepth 1 ! -name .git -exec rm -rf {} +
  cp -a "$base/copia/." "$base/entrega/"
  git -C "$base/entrega" add -A
  if git -C "$base/entrega" diff --cached --quiet; then
    echo "No hay cambios respecto de la entrega anterior."; return 0
  fi
  git -C "$base/entrega" commit -q -m "Entrega $fecha"
  echo
  git -C "$base/entrega" show --stat --oneline HEAD | tail -15
  echo
  echo "Lista para empujar. Revisa y, con el OK, corre:"
  echo "  bash docs/entrega/entregar.sh empujar \"$base\""
}

empujar() {
  local base="${1:?falta la carpeta}"
  local rama; rama=$(git -C "$base/entrega" rev-parse --abbrev-ref HEAD)
  git -C "$base/entrega" push origin "$rama"
}

case "${1:-}" in
  armar) shift; armar "$@" ;;
  empujar) shift; empujar "$@" ;;
  *) sed -n '2,9p' "$0"; exit 2 ;;
esac
```

- [ ] **Step 2: Revisión de sintaxis**

Run: `bash -n docs/entrega/entregar.sh && echo ok`
Expected: `ok`.

- [ ] **Step 3: Commit**

```bash
git add docs/entrega/entregar.sh
git commit -m "Entrega al cliente: entregar.sh arma la foto sin historial de un commit (pruebas en un worktree del repositorio privado, git archive, preparar, revisar, compilar, commit Entrega AAAA-MM-DD sobre la rama de entregas) y no empuja; empujar es una segunda llamada explicita"
```

---

### Task 7: Ensayo con un remoto local

No toca ningún remoto real.

- [ ] **Step 1: Remoto de ensayo**

```bash
ensayo="$HOME/mlm-entregas/ensayo-remoto.git"
rm -rf "$ensayo" && git init -q --bare "$ensayo"
bash docs/entrega/entregar.sh armar HEAD "$ensayo"
```

Expected: las 10 suites `Passed!`, revisión sin problemas, compila, y al final "Lista para empujar".

- [ ] **Step 2: Empujar al remoto de ensayo y comprobar**

```bash
bash docs/entrega/entregar.sh empujar "<carpeta que imprimio armar>"
git clone -q "$ensayo" "$HOME/mlm-entregas/ensayo-clon"
cd "$HOME/mlm-entregas/ensayo-clon"
git log --oneline | wc -l                                   # 1
git ls-files | grep -ciE "\.md$|\.Tests/|^docs/(superpowers|arquitectura|presentations|deployment)|^deploy/|^Scripts/|^\.claude" # 0
git ls-files docs | sed 's#/.*##' | sort -u                 # docs
git ls-files docs | cut -d/ -f2 | sort -u                   # billing, commissions
grep -c "Tests" MLMConquerorGlobalEdition.slnx              # 0
dotnet build MLMConquerorGlobalEdition.slnx -nologo -v q 2>&1 | grep "Error(s)"   # 0 Error(s)
cd - >/dev/null
```

- [ ] **Step 3: Segunda entrega encima de la primera**

Hacer un commit trivial en una rama de ensayo (por ejemplo, un comentario en `global.json` no: es JSON; usar un espacio en `Directory.Build.props`), `armar` contra el mismo remoto de ensayo y `empujar`. En el clon, `git pull` y `git log --oneline | wc -l` da 2, y `git show --stat HEAD` muestra solo `Directory.Build.props`. Después se deshace el commit de ensayo (`git reset --hard HEAD~1` en esa rama de ensayo, nunca en `secretos-aws` ni en `master`) y se borran `ensayo-remoto.git` y `ensayo-clon`.

- [ ] **Step 4: Informe a Nick**

Contarle el resultado y que la entrega real espera la dirección del remoto del cliente.

---

### Task 8: Memoria

- [ ] **Step 1:** Actualizar `entrega-al-cliente.md` en la memoria con la ubicación de las herramientas (`docs/entrega/`, `docs/generar-pdfs.sh`) y que el ensayo pasó.
