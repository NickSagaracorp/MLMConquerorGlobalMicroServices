# -*- coding: utf-8 -*-
"""Markdown -> PDF con reportlab, para los documentos de docs/deployment."""
import io, re, sys
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import (BaseDocTemplate, PageTemplate, Frame, Paragraph,
                                Spacer, Table, TableStyle)
from reportlab.lib.enums import TA_LEFT

SRC, OUT, TITLE = sys.argv[1], sys.argv[2], sys.argv[3]
INK   = colors.HexColor("#1b1f23")
MUTED = colors.HexColor("#5b6570")
RULE  = colors.HexColor("#d8dee4")
BAND  = colors.HexColor("#f2f4f7")


def S(n, **k):
    d = dict(name=n, fontName="Helvetica", fontSize=9.6, leading=14.4,
             textColor=INK, alignment=TA_LEFT, spaceAfter=6)
    d.update(k)
    return ParagraphStyle(**d)


st = {
    "h1": S("h1", fontName="Helvetica-Bold", fontSize=17, leading=21, spaceBefore=16, spaceAfter=9),
    "h2": S("h2", fontName="Helvetica-Bold", fontSize=12.5, leading=16, spaceBefore=13, spaceAfter=6),
    "h3": S("h3", fontName="Helvetica-Bold", fontSize=10.4, leading=14, spaceBefore=10, spaceAfter=4),
    "p":  S("p"),
    "li": S("li", leftIndent=12, bulletIndent=3, spaceAfter=3),
    "q":  S("q", leftIndent=10, textColor=MUTED, fontName="Helvetica-Oblique"),
    "code": S("code", fontName="Courier", fontSize=8.3, leading=11.4),
    "th": S("th", fontName="Helvetica-Bold", fontSize=8.8, leading=11.6),
    "td": S("td", fontSize=8.8, leading=11.6),
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
                      '<font face="Courier" size="8.6">%s</font>' % c)
    return t


def header(c, d):
    c.saveState()
    c.setFont("Helvetica", 7.4)
    c.setFillColor(MUTED)
    c.drawString(20 * mm, A4[1] - 13 * mm, TITLE)
    c.drawRightString(A4[0] - 20 * mm, A4[1] - 13 * mm, "MLM Conqueror Global Edition")
    c.setStrokeColor(RULE)
    c.setLineWidth(.4)
    c.line(20 * mm, A4[1] - 15 * mm, A4[0] - 20 * mm, A4[1] - 15 * mm)
    c.drawCentredString(A4[0] / 2, 12 * mm, str(c.getPageNumber()))
    c.restoreState()


doc = BaseDocTemplate(OUT, pagesize=A4, leftMargin=20 * mm, rightMargin=20 * mm,
                      topMargin=21 * mm, bottomMargin=18 * mm, title=TITLE)
doc.addPageTemplates([PageTemplate(
    id="b",
    frames=[Frame(20 * mm, 18 * mm, A4[0] - 40 * mm, A4[1] - 39 * mm, id="n")],
    onPage=header)])

lines = io.open(SRC, encoding="utf-8").read().split("\n")
fl, i = [], 0
while i < len(lines):
    ln = lines[i]

    if ln.startswith("```"):
        buf = []
        i += 1
        while i < len(lines) and not lines[i].startswith("```"):
            buf.append(lines[i])
            i += 1
        i += 1
        body = "<br/>".join(
            (l.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
               .replace(" ", "&nbsp;")) for l in buf) or "&nbsp;"
        t = Table([[Paragraph(body, st["code"])]], colWidths=[doc.width])
        t.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), BAND),
            ("BOX", (0, 0), (-1, -1), .4, RULE),
            ("LEFTPADDING", (0, 0), (-1, -1), 7), ("RIGHTPADDING", (0, 0), (-1, -1), 7),
            ("TOPPADDING", (0, 0), (-1, -1), 6), ("BOTTOMPADDING", (0, 0), (-1, -1), 6)]))
        fl += [Spacer(1, 3), t, Spacer(1, 7)]
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
            ("BACKGROUND", (0, 0), (-1, 0), BAND),
            ("LINEBELOW", (0, 0), (-1, 0), .6, RULE),
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
        fl.append(Paragraph(inline(ln[2:]), st["h1"]))
    elif ln.startswith("> "):
        fl.append(Paragraph(inline(ln[2:]), st["q"]))
    elif re.match(r"^\d+\.\s", ln):
        fl.append(Paragraph(inline(re.sub(r"^\d+\.\s", "", ln)), st["li"],
                            bulletText=ln.split(".")[0] + "."))
    elif ln.startswith("- "):
        fl.append(Paragraph(inline(ln[2:]), st["li"], bulletText="•"))
    elif ln.strip():
        fl.append(Paragraph(inline(ln), st["p"]))
    i += 1

doc.build(fl)
print("  escrito:", OUT)
