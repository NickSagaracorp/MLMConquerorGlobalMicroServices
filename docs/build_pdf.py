# -*- coding: utf-8 -*-
"""Markdown -> PDF oficial con la marca de Sagara Media Group (reportlab).

Uso: python -I docs/build_pdf.py <md> <pdf> "<titulo>" [tamano-codigo] [subtitulo]
"""
import io, os, re, sys
from datetime import date
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.fonts import addMapping
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, Table,
                                TableStyle, NextPageTemplate, PageBreak)

SRC, OUT, TITLE = sys.argv[1], sys.argv[2], sys.argv[3]
# Cuarto argumento opcional: tamano de letra de los bloques de codigo. Las guias con comandos
# largos lo bajan para que cada linea quepa entera y se pueda copiar sin cortes.
CODE_PT = float(sys.argv[4]) if len(sys.argv) > 4 else 8.3
SUBTITLE = sys.argv[5] if len(sys.argv) > 5 else "MLM Conqueror Global Edition"

# --- Marca: sagaramediagroup.com (assets/css/site.css y assets/img/logo.svg) ----------------------
MARCA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "marca")
F = os.path.join(MARCA, "fuentes")
for nombre, archivo in [("Plex", "IBMPlexSans-Regular.ttf"), ("Plex-Bold", "IBMPlexSans-Bold.ttf"),
                        ("Plex-Italic", "IBMPlexSans-Italic.ttf"), ("Plex-Semi", "IBMPlexSans-SemiBold.ttf"),
                        ("PlexMono", "IBMPlexMono-Regular.ttf"), ("Grotesk", "SpaceGrotesk-Bold.ttf"),
                        ("Grotesk-Medium", "SpaceGrotesk-Medium.ttf")]:
    pdfmetrics.registerFont(TTFont(nombre, os.path.join(F, archivo)))
addMapping("Plex", 0, 0, "Plex")
addMapping("Plex", 1, 0, "Plex-Bold")
addMapping("Plex", 0, 1, "Plex-Italic")
addMapping("Plex", 1, 1, "Plex-Bold")
LOGO = os.path.join(MARCA, "logo.png")
LOGO_PROPORCION = 631 / 2400

NAVY      = colors.HexColor("#1c2f66")
TEAL      = colors.HexColor("#3dadaf")
TEAL_INK  = colors.HexColor("#1f7f81")
INK       = colors.HexColor("#12172b")
MUTED     = colors.HexColor("#4b5265")
RULE      = colors.HexColor("#e1e5ee")
BAND      = colors.HexColor("#eef1f8")


def S(n, **k):
    d = dict(name=n, fontName="Plex", fontSize=9.6, leading=14.4,
             textColor=INK, alignment=TA_LEFT, spaceAfter=6)
    d.update(k)
    return ParagraphStyle(**d)


st = {
    "h1": S("h1", fontName="Grotesk", fontSize=18, leading=22, spaceBefore=16, spaceAfter=9, textColor=NAVY),
    "h2": S("h2", fontName="Grotesk", fontSize=13, leading=17, spaceBefore=13, spaceAfter=6, textColor=NAVY),
    "h3": S("h3", fontName="Grotesk-Medium", fontSize=10.8, leading=14, spaceBefore=10, spaceAfter=4,
            textColor=TEAL_INK),
    "p":  S("p"),
    "li": S("li", leftIndent=12, bulletIndent=3, spaceAfter=3),
    "q":  S("q", leftIndent=10, textColor=MUTED, fontName="Plex-Italic"),
    "li2": S("li2", leftIndent=26, bulletIndent=16, spaceAfter=3),
    "code": S("code", fontName="PlexMono", fontSize=CODE_PT, leading=CODE_PT * 1.37),
    "th": S("th", fontName="Plex-Semi", fontSize=8.8, leading=11.6, textColor=colors.white),
    "td": S("td", fontSize=8.8, leading=11.6),
    "portada_t": S("portada_t", fontName="Grotesk", fontSize=28, leading=33, textColor=NAVY, spaceAfter=10),
    "portada_s": S("portada_s", fontName="Grotesk-Medium", fontSize=13, leading=17, textColor=TEAL_INK),
    "portada_f": S("portada_f", textColor=MUTED),
}


def inline(t):
    """Escapa, aplica negrita/cursiva y codigo en linea.

    El codigo se aparta ANTES de tocar las negritas: un `p***@dominio.com` lleva
    asteriscos que la regla de negrita partiria por la mitad, dejando etiquetas
    cruzadas que reportlab rechaza.
    """
    t = t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    spans = []

    def stash(m):
        spans.append(m.group(1))
        return "\x00%d\x00" % (len(spans) - 1)

    t = re.sub(r"`([^`]+)`", stash, t)
    t = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", t)
    t = re.sub(r"(?<![\w*])\*([^*\n]+)\*(?![\w*])", r"<i>\1</i>", t)
    for i, c in enumerate(spans):
        t = t.replace("\x00%d\x00" % i,
                      '<font face="PlexMono" size="8.4">%s</font>' % c)
    return t


def portada(c, d):
    c.saveState()
    c.setFillColor(NAVY)
    c.rect(0, 0, A4[0], 28 * mm, stroke=0, fill=1)
    c.setFillColor(TEAL)
    c.rect(0, 28 * mm, A4[0], 1.6 * mm, stroke=0, fill=1)
    ancho = 95 * mm
    c.drawImage(LOGO, 20 * mm, A4[1] - 55 * mm, width=ancho, height=ancho * LOGO_PROPORCION, mask="auto")
    c.setFont("Plex", 8.5)
    c.setFillColor(colors.white)
    c.drawString(20 * mm, 13 * mm, "Sagara Media Group")
    c.drawRightString(A4[0] - 20 * mm, 13 * mm, "Documento confidencial")
    c.restoreState()


def interior(c, d):
    c.saveState()
    alto = 7.5 * mm
    c.drawImage(LOGO, 20 * mm, A4[1] - 15.5 * mm, width=alto / LOGO_PROPORCION, height=alto, mask="auto")
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
CUERPO_ALTO = A4[1] - 43 * mm
doc.addPageTemplates([
    PageTemplate(id="portada", frames=[Frame(20 * mm, 45 * mm, A4[0] - 40 * mm, A4[1] - 120 * mm, id="p")],
                 onPage=portada),
    PageTemplate(id="b", frames=[Frame(20 * mm, 20 * mm, A4[0] - 40 * mm, CUERPO_ALTO, id="n")],
                 onPage=interior),
])

fl = [Spacer(1, 40 * mm), Paragraph(inline(TITLE), st["portada_t"]),
      Paragraph(inline(SUBTITLE), st["portada_s"]), Spacer(1, 8 * mm),
      Paragraph(date.today().strftime("%d-%m-%Y"), st["portada_f"]),
      NextPageTemplate("b"), PageBreak()]

lines = io.open(SRC, encoding="utf-8").read().split("\n")
i = 0
primer_h1 = True
while i < len(lines):
    ln = lines[i]

    if ln.startswith("```"):
        buf = []
        i += 1
        while i < len(lines) and not lines[i].startswith("```"):
            buf.append(lines[i])
            i += 1
        i += 1
        # Un bloque mas alto que la pagina no cabe en ningun marco: se parte en trozos que si.
        per_page = min(40, max(10, int((CUERPO_ALTO - 24) // (CODE_PT * 1.37)) - 2))
        chunks = [buf[k:k + per_page] for k in range(0, len(buf), per_page)] or [[]]
        fl.append(Spacer(1, 3))
        for chunk in chunks:
            body = "<br/>".join(
                (l.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                   .replace(" ", "&nbsp;")) for l in chunk) or "&nbsp;"
            t = Table([[Paragraph(body, st["code"])]], colWidths=[doc.width])
            t.setStyle(TableStyle([
                ("BACKGROUND", (0, 0), (-1, -1), BAND),
                ("BOX", (0, 0), (-1, -1), .4, RULE),
                ("LINEBEFORE", (0, 0), (0, -1), 2.2, TEAL),
                ("LEFTPADDING", (0, 0), (-1, -1), 8), ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 6), ("BOTTOMPADDING", (0, 0), (-1, -1), 6)]))
            fl.append(t)
        fl.append(Spacer(1, 7))
        continue

    if (ln.startswith("|") and i + 1 < len(lines)
            and set(lines[i + 1].replace("|", "").strip()) <= set("-: ")):
        hdr = [c.strip() for c in ln.strip().strip("|").split("|")]
        i += 2
        rows = []
        while i < len(lines) and lines[i].startswith("|"):
            rows.append([c.strip() for c in lines[i].strip().strip("|").split("|")])
            i += 1
        n = len(hdr)
        w = doc.width / n
        data = ([[Paragraph(inline(c), st["th"]) for c in hdr]] +
                [[Paragraph(inline(c), st["td"]) for c in (r + [""] * n)[:n]] for r in rows])
        t = Table(data, colWidths=[w] * n, repeatRows=1)
        t.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, 0), NAVY),
            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, BAND]),
            ("GRID", (0, 0), (-1, -1), .3, RULE),
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("LEFTPADDING", (0, 0), (-1, -1), 5), ("RIGHTPADDING", (0, 0), (-1, -1), 5),
            ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 4)]))
        fl += [Spacer(1, 3), t, Spacer(1, 8)]
        continue

    if ln.startswith("---") and ln.strip("- ") == "":
        fl.append(Spacer(1, 9))
    elif ln.startswith("### "):
        fl.append(Paragraph(inline(ln[4:]), st["h3"]))
    elif ln.startswith("## "):
        fl.append(Paragraph(inline(ln[3:]), st["h2"]))
    elif ln.startswith("# "):
        # El primer titulo del documento ya esta en la portada.
        if not (primer_h1 and ln[2:].strip() == TITLE.strip()):
            fl.append(Paragraph(inline(ln[2:]), st["h1"]))
        primer_h1 = False
    elif ln.startswith("> "):
        fl.append(Paragraph(inline(ln[2:]), st["q"]))
    elif re.match(r"^\d+\.\s", ln):
        fl.append(Paragraph(inline(re.sub(r"^\d+\.\s", "", ln)), st["li"],
                            bulletText=ln.split(".")[0] + "."))
    elif ln.startswith("  - "):
        fl.append(Paragraph(inline(ln[4:]), st["li2"], bulletText="–"))
    elif ln.startswith("- "):
        fl.append(Paragraph(inline(ln[2:]), st["li"], bulletText="•"))
    elif ln.strip():
        fl.append(Paragraph(inline(ln), st["p"]))
    i += 1

doc.build(fl)
print("  escrito:", OUT)
