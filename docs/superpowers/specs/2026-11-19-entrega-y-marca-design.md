# Entrega al cliente y PDFs con la marca de Sagara Media Group — Diseño

**Fecha:** 2026-11-19
**Estado:** aprobado por Nick
**Rama:** `secretos-aws` (se trabaja antes que el plan de secretos)

## 1. Objetivo

1. **Entrega:** el desarrollo vive siempre en el GitHub privado de Nick. Al repositorio remoto del cliente solo llegan entregas finales, sin nada que le permita continuar el desarrollo por su cuenta: ni reglas de agentes, skills o plugins, ni planes, especificaciones, pruebas o recetas de despliegue. Si lo intenta, tiene que descubrirlo solo.
2. **Marca:** toda especificación, plan, regla o guía tiene un PDF oficial con la marca de Sagara Media Group. Los PDFs se quedan en el repositorio privado; ninguno viaja al cliente.

## 2. Qué viaja y qué no

| Viaja al cliente | No viaja |
|---|---|
| Los proyectos de la solución, salvo los de pruebas | Todo `*.md`, en cualquier carpeta |
| `docs/billing/`, `docs/commissions/` | Los 10 proyectos `*.Tests` |
| `UI-Templates/` | `docs/superpowers/`, `docs/arquitectura/`, `docs/presentations/`, `docs/deployment/` |
| `global.json`, `Directory.Build.props`, `.gitattributes` | `docs/build_pdf.py`, `docs/generar-pdfs.sh`, `docs/marca/`, `docs/entrega/` |
| `LogoMLMConqueror.png` | `.claude/` y cualquier archivo de reglas de agentes (`CLAUDE.md`, `AGENTS.md`, `.cursor*`, `.github/copilot*`) |
| | `deploy/`, `Scripts/`, `Dockerfile`, `.dockerignore`, `DeploymentScripts/`, `.env.example`, `fix_helpdesk_tables.sql` |
| | Ningún PDF de documentación |

La lista vive en `docs/entrega/no-viaja.txt` (que tampoco viaja).

## 3. La rama de entrega

### 3.0 Remoto y rama por cliente

- Nick desarrolla un SaaS para MLM y lo entrega a varias empresas; cada una recibe solo lo que pagó. En el repositorio privado hay **una rama por cliente**; la de MWRLife es `MWRLife` (creada desde `master` en `c849b9b` el 19-nov-2026). Lo que MWRLife no paga no entra en esa rama.
- El remoto del cliente se llama **`mwrlife`** (`https://github.com/mwrlifeteam/MLMConquerorGlobalMicroServiceMWRLife.git`), no `origin`: `origin` es el GitHub privado de Nick.
- El remoto `mwrlife` tiene la dirección de envío anulada (`remote.mwrlife.pushurl = no-empujar://usa-docs-entrega-entregar.sh`): un `git push mwrlife ...` por error falla. `entregar.sh` lee la dirección real de `remote.mwrlife.url` y empuja desde su propio clon limpio.
- En el remoto del cliente las entregas van a la rama `main`.

- Una rama que **solo contiene entregas** (`entrega` por defecto en el remoto del cliente). Cada entrega es **un commit** cuyo árbol es la foto completa del código entregado, encima de la entrega anterior; nunca lleva el historial del repositorio privado. Mensaje neutro: `Entrega AAAA-MM-DD`.
- **Nunca** se empuja `master` ni ninguna rama del repositorio privado al remoto del cliente.

### 3.1 `docs/entrega/entregar.sh armar <rama-o-commit> <nombre-del-remoto> [rama-remota]` (por ejemplo `armar MWRLife mwrlife`)

1. **Pruebas en el repositorio privado:** crea un *worktree* temporal en `<commit>` y corre las 10 suites. Si alguna falla, se detiene.
2. **Exporta** el árbol de `<commit>` con `git archive` (sin `.git`, sin historial) a una carpeta temporal.
3. **Prepara la copia** (`docs/entrega/preparar.py`):
   - borra todo lo que coincide con `no-viaja.txt`;
   - quita del `.slnx` las entradas `*.Tests`;
   - quita de `.csproj` y `.cs` las declaraciones `InternalsVisibleTo` hacia proyectos `*.Tests`;
   - escribe un `.gitignore` limpio desde `docs/entrega/gitignore-cliente` (solo `bin/`, `obj/`, `.vs/`, `*.user`, `appsettings.*.json` y similares; sin líneas que mencionen `.claude` ni carpetas internas).
4. **Revisa la copia** (también `preparar.py`, modo `--revisar`); falla si encuentra:
   - un archivo cuyo nombre coincida con `no-viaja.txt`;
   - un proyecto o una referencia `*.Tests`;
   - en el **contenido** de cualquier archivo de texto **y del texto de los documentos de Office** (`.pptx`, `.docx`, `.xlsx`, que son zip): las palabras `claude`, `anthropic`, `superpowers`, el nombre de cualquier archivo `.md` y las rutas `docs/superpowers`, `docs/deployment`, `docs/arquitectura` y `docs/presentations` (sin distinguir mayúsculas), salvo en los archivos de terceros de una lista de excepciones (`docs/entrega/excepciones.txt`: las fuentes y el CSS de Remix Icon, que tienen un ícono con ese nombre).
5. **Compila** la copia (`dotnet build` del `.slnx` limpio). Si no compila, se detiene.
6. **Commit de entrega:** clona solo la rama de entrega del remoto (o la crea vacía si es la primera), reemplaza su árbol por la copia y hace el commit `Entrega AAAA-MM-DD`.
7. **Muestra el resumen** (archivos agregados, cambiados y borrados respecto de la entrega anterior) y **pregunta** antes de `git push`. Sin un "sí" explícito no empuja.

### 3.2 Limpieza previa en el repositorio privado

En el código que viaja hay 18 líneas, en 15 archivos, que citan documentos internos (`CLAUDE.md`, `diagnostico-huecos-binario.md`, `placement-rules.md`, guías de `docs/deployment`, un plan de `docs/superpowers`...). Se corrigen en `master` (y llegan a `MWRLife` con el siguiente merge): el comentario conserva la explicación de la regla y pierde la referencia al documento. Las presentaciones de `docs/billing` y `docs/commissions` citan `docs/superpowers/specs/` y `BILLING-RULES.md`: se corrige el texto en su `build-deck.js` y se regeneran los `.pptx`.

## 4. PDFs con la marca

### 4.1 Marca

Tomada de la web de Sagara Media Group (`Downloads/sagaramediagroup-html/sagaramediagroup.com/assets`):

| Elemento | Valor |
|---|---|
| Logo | `logo.svg` (degradado `#3dadaf` → `#264091`) |
| Azul marino (títulos, portada) | `#1c2f66`; oscuro `#152350` |
| Verde azulado (detalles, líneas) | `#3dadaf`; para texto `#1f7f81` |
| Rojo de acento | `#b70900` (solo avisos) |
| Texto | `#12172b`; suave `#4b5265` |
| Fondos y líneas | `#eef1f8`, `#e1e5ee` |
| Tipografía | Space Grotesk (títulos), IBM Plex Sans (texto), IBM Plex Mono (código); todas con licencia SIL Open Font License |

### 4.2 Plantilla

- **Portada:** logo grande, título del documento, subtítulo (proyecto), fecha, "Sagara Media Group" y "Documento confidencial".
- **Páginas interiores:** encabezado con el logo pequeño a la izquierda y el título a la derecha; línea verde azulada; pie con "Confidencial · Sagara Media Group" y número de página.
- Títulos en azul marino con Space Grotesk; texto en IBM Plex Sans; bloques de código con fondo `#eef1f8`, borde izquierdo verde azulado e IBM Plex Mono; tablas con encabezado azul marino y texto blanco.
- Los saltos de página, tamaños de código y división de bloques largos que ya hace `build_pdf.py` se conservan.

### 4.3 Archivos

- `docs/marca/`: `logo.svg`, `logo.png` (convertido con Chrome sin ventana a 2400 px de ancho, porque reportlab no dibuja bien los degradados del SVG), las fuentes `.ttf` y sus licencias `OFL.txt`.
- `docs/build_pdf.py`: la plantilla. Recibe el título y, opcionalmente, el subtítulo y el tamaño de código.
- `docs/generar-pdfs.sh`: genera un PDF junto a cada `.md` de `docs/superpowers`, `docs/deployment` y `docs/arquitectura`. El título sale del primer `# ` del `.md`.
- Los PDFs se suben al repositorio privado con `git add -f` (el `.gitignore` de Nick ignora `docs/deployment/*.pdf` y no se toca).

## 5. Lo que no se hace

- Quitar o reescribir comentarios del código que explican reglas de negocio: son parte del código entregado. Solo se corrigen los que nombran herramientas o rutas internas del método.
- Ofuscar el código.
- Conectar el remoto del cliente: se hace cuando Nick dé su dirección.

## 6. Pruebas

- `preparar.py` tiene pruebas con `unittest` de Python (`docs/entrega/test_preparar.py`) sobre un árbol de ejemplo en una carpeta temporal: borra lo de la lista, limpia el `.slnx` y los `InternalsVisibleTo`, y la revisión falla con un `.md`, con un `*.Tests` o con la palabra `superpowers` en un `.cs`, y no falla con un archivo de la lista de excepciones.
- Una entrega de ensayo a un remoto **local** (`git init --bare` en una carpeta temporal) que demuestra: el árbol entregado compila; `git log` del remoto tiene un solo commit; no hay `.md`, pruebas ni `docs/` interno.
- Los PDFs se revisan a la vista (portada y una página interior de una guía) antes de regenerarlos todos.
