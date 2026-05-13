// Build the MLMConqueror Billing Workflow deck.
// Palette: Midnight Executive (navy / ice blue / white).
// Run with: node build-deck.js

const path = require("path");
const pptxgen = require("pptxgenjs");

const NAVY = "1E2761";        // dominant dark
const NAVY_DEEP = "0F1838";   // deeper for accents
const ICE = "CADCFC";         // soft accent
const WHITE = "FFFFFF";
const INK = "1E293B";         // dark slate for body on light
const MUTED = "64748B";       // muted body
const SUCCESS = "10B981";     // green for "ok"
const WARN = "F59E0B";        // amber for "watch"
const DANGER = "DC2626";      // red for "stop"
const SUBTLE_BG = "F8FAFC";   // very light grey for content blocks

const HEADER_FONT = "Georgia";
const BODY_FONT = "Calibri";

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE";   // 13.333" × 7.5"
pres.author = "Sagara Media Group";
pres.title = "MLMConqueror Billing — Rules & Workflow";
pres.subject = "Reference deck for the billing subsystem";
const W = 13.333, H = 7.5;

// ─────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────
function headerBar(slide, title, eyebrow) {
  // Eyebrow + title for content slides
  if (eyebrow) {
    slide.addText(eyebrow.toUpperCase(), {
      x: 0.6, y: 0.35, w: 12, h: 0.32,
      fontFace: BODY_FONT, fontSize: 11, bold: true,
      color: MUTED, charSpacing: 3, margin: 0
    });
  }
  slide.addText(title, {
    x: 0.6, y: eyebrow ? 0.65 : 0.45, w: 12, h: 0.9,
    fontFace: HEADER_FONT, fontSize: 30, bold: true,
    color: NAVY, margin: 0
  });
  // Small navy accent dot to the left of the title
  slide.addShape(pres.shapes.OVAL, {
    x: 0.25, y: eyebrow ? 0.92 : 0.72, w: 0.18, h: 0.18,
    fill: { color: NAVY }, line: { color: NAVY }
  });
}

function footer(slide, slideNum, total) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: H - 0.36, w: W, h: 0.36, fill: { color: NAVY }, line: { color: NAVY }
  });
  slide.addText("MLMConqueror — Billing Reference · v1.0 · 2026-05-13", {
    x: 0.5, y: H - 0.34, w: 9, h: 0.32, fontFace: BODY_FONT, fontSize: 9,
    color: ICE, valign: "middle", margin: 0
  });
  slide.addText(`${slideNum} / ${total}`, {
    x: W - 1.5, y: H - 0.34, w: 1.0, h: 0.32, fontFace: BODY_FONT, fontSize: 9,
    color: ICE, align: "right", valign: "middle", margin: 0
  });
}

function card(slide, x, y, w, h, title, body, accent) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w, h, fill: { color: WHITE },
    line: { color: ICE, width: 1 },
    shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
  });
  // accent bar on the left
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w: 0.08, h, fill: { color: accent || NAVY }, line: { color: accent || NAVY }
  });
  slide.addText(title, {
    x: x + 0.25, y: y + 0.18, w: w - 0.4, h: 0.4,
    fontFace: HEADER_FONT, fontSize: 16, bold: true, color: NAVY, margin: 0
  });
  slide.addText(body, {
    x: x + 0.25, y: y + 0.65, w: w - 0.4, h: h - 0.85,
    fontFace: BODY_FONT, fontSize: 12, color: INK, valign: "top", margin: 0,
    paraSpaceAfter: 4
  });
}

function statCallout(slide, x, y, w, h, big, label, accent) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x, y, w, h, fill: { color: NAVY }, line: { color: NAVY }
  });
  slide.addText(big, {
    x: x + 0.1, y: y + 0.1, w: w - 0.2, h: h * 0.55,
    fontFace: HEADER_FONT, fontSize: 44, bold: true,
    color: accent || ICE, align: "center", valign: "middle", margin: 0
  });
  slide.addText(label, {
    x: x + 0.1, y: y + h * 0.6, w: w - 0.2, h: h * 0.35,
    fontFace: BODY_FONT, fontSize: 11, bold: true,
    color: WHITE, align: "center", valign: "top", margin: 0, charSpacing: 2
  });
}

function pill(slide, x, y, w, h, text, fill, color) {
  slide.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x, y, w, h, fill: { color: fill }, line: { color: fill }, rectRadius: h / 2
  });
  slide.addText(text, {
    x, y, w, h, fontFace: BODY_FONT, fontSize: 10, bold: true,
    color, align: "center", valign: "middle", margin: 0
  });
}

function arrow(slide, x, y, w, color) {
  // simple horizontal arrow indicator (line + tiny triangle made of a small rectangle rotated)
  slide.addShape(pres.shapes.RIGHT_ARROW, {
    x, y, w, h: 0.22,
    fill: { color }, line: { color }
  });
}

// ─────────────────────────────────────────────────────────────────────
// Slide 1 — Title
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  // subtle horizontal accent line
  s.addShape(pres.shapes.RECTANGLE, {
    x: 1, y: 4.2, w: 1.2, h: 0.04, fill: { color: ICE }, line: { color: ICE }
  });
  s.addText("MLMCONQUEROR · BILLING", {
    x: 1, y: 3.5, w: 11, h: 0.45,
    fontFace: BODY_FONT, fontSize: 14, bold: true, color: ICE,
    charSpacing: 6, margin: 0
  });
  s.addText("Rules & Workflow", {
    x: 1, y: 4.4, w: 11, h: 1.1,
    fontFace: HEADER_FONT, fontSize: 56, bold: true, color: WHITE, margin: 0
  });
  s.addText("Reference deck for engineers and AI assistants — the agreed rules of how money moves through the platform, why each rule exists, and where to find it in code.", {
    x: 1, y: 5.5, w: 11, h: 1.0,
    fontFace: BODY_FONT, fontSize: 16, color: ICE, italic: true, margin: 0
  });
  s.addText("v1.0 · 2026-05-13 · Sagara Media Group", {
    x: 1, y: 6.7, w: 11, h: 0.4,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: ICE,
    charSpacing: 3, margin: 0
  });
}

// ─────────────────────────────────────────────────────────────────────
// Slide 2 — Table of contents
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "What's in this deck", "Contents");

  const items = [
    ["01", "Three-layer mental model", "Tokenization · Card-charge · Recurring"],
    ["02", "Spreedly as the universal vault", "Why one token per member is enough"],
    ["03", "Card processor catalog", "Who can charge what"],
    ["04", "Routing matrix", "Country × brand → gateway split"],
    ["05", "Deterministic % counter", "Exact ratios, not statistical drift"],
    ["06", "Currency presentment", "EUR / GBP / CAD / AUD + 2 %"],
    ["07", "Fallback chains", "Signup · Recurring · Authorization"],
    ["08", "Recurring plans", "Travel Advantage vs Lifestyle Ambassador"],
    ["09", "Retry cadence", "Each retry = prev attempt + next offset"],
    ["10", "Commission-balance-first funding", "Pay from earnings before the card"],
    ["11", "Daily Residual ledger", "Its own table + Monday consolidation"],
    ["12", "Membership status semantics", "OnHold ≠ HoldByBilling"],
    ["13", "High-volume architecture", "Four-stage pipeline (planned)"],
    ["14", "Hard rules", "Ten things you must never violate"],
  ];

  // two columns of 7 each
  const colW = 5.7;
  const rowH = 0.55;
  const startY = 1.6;
  items.forEach((it, idx) => {
    const col = idx < 7 ? 0 : 1;
    const row = idx % 7;
    const x = 0.6 + col * (colW + 0.4);
    const y = startY + row * rowH;
    s.addText(it[0], {
      x, y, w: 0.55, h: rowH,
      fontFace: HEADER_FONT, fontSize: 14, bold: true, color: ICE,
      align: "right", valign: "middle", margin: 0
    });
    s.addText(it[1], {
      x: x + 0.65, y, w: colW - 2.7, h: rowH,
      fontFace: BODY_FONT, fontSize: 13, bold: true, color: NAVY,
      valign: "middle", margin: 0
    });
    s.addText(it[2], {
      x: x + colW - 1.95, y, w: 1.95, h: rowH,
      fontFace: BODY_FONT, fontSize: 10, italic: true, color: MUTED,
      valign: "middle", margin: 0
    });
  });
  footer(s, 2, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 3 — Three-layer mental model
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "The three layers of the billing stack", "01 · Mental model");

  s.addText("Most production issues live in the seam between two layers — hold the boundaries clearly.", {
    x: 0.6, y: 1.55, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 13, italic: true, color: MUTED, margin: 0
  });

  const cardW = 4.0, cardH = 4.5, y0 = 2.1, gap = 0.3;
  const layers = [
    {
      title: "1 · Tokenization",
      body: [
        { text: "What it owns", options: { bold: true, color: NAVY, breakLine: true } },
        { text: "The card itself — vaulted once at Spreedly. Returns a single payment_method_token per member.", options: { breakLine: true } },
        { text: " ", options: { breakLine: true } },
        { text: "Where it lives", options: { bold: true, color: NAVY, breakLine: true } },
        { text: "Spreedly + MemberPaymentMethod / MemberCreditCard (Domain).", options: {} }
      ],
      accent: "0EA5E9"
    },
    {
      title: "2 · Card-charge (routing)",
      body: [
        { text: "What it owns", options: { bold: true, color: NAVY, breakLine: true } },
        { text: "Per transaction: which gateway, which currency, what fallback chain to walk on failure. Stateless.", options: { breakLine: true } },
        { text: " ", options: { breakLine: true } },
        { text: "Where it lives", options: { bold: true, color: NAVY, breakLine: true } },
        { text: "Billing/Services/Routing/* + Services/CardGateway/*", options: {} }
      ],
      accent: NAVY
    },
    {
      title: "3 · Recurring / dunning",
      body: [
        { text: "What it owns", options: { bold: true, color: NAVY, breakLine: true } },
        { text: "When to bill, how to bill, what to retry, what to do when retries exhaust. Per-subscription state machine.", options: { breakLine: true } },
        { text: " ", options: { breakLine: true } },
        { text: "Where it lives", options: { bold: true, color: NAVY, breakLine: true } },
        { text: "Billing/Services/Recurring/* + Jobs/RecurringBillingSweepJob.cs", options: {} }
      ],
      accent: "8B5CF6"
    }
  ];
  layers.forEach((l, i) => {
    const x = 0.6 + i * (cardW + gap);
    card(s, x, y0, cardW, cardH, l.title,
      l.body.map((p, idx) => ({ ...p, options: { ...p.options, breakLine: idx < l.body.length - 1 } })),
      l.accent);
  });

  s.addText("Layer 3 never talks to a gateway directly — it always delegates to layer 2.", {
    x: 0.6, y: 6.75, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, italic: true, color: MUTED, margin: 0
  });
  footer(s, 3, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 4 — Spreedly universal vault
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Tokenization: Spreedly as the universal vault", "02 · Tokenization");

  // Left: explanation
  s.addText([
    { text: "One token per member, every gateway in scope.", options: { bold: true, color: NAVY, fontSize: 16, breakLine: true } },
    { text: " ", options: { breakLine: true } },
    { text: "Spreedly is a PCI-compliant vault that proxies to dozens of downstream processors. You vault the card once at signup; at charge time, you tell Spreedly to execute against whichever gateway the router picked.", options: { fontSize: 13, breakLine: true } },
    { text: " ", options: { breakLine: true } },
    { text: "Consequences", options: { bold: true, color: NAVY, fontSize: 14, breakLine: true } },
    { text: "✓  No per-gateway vaulting — admin adding a new processor needs no back-fill", options: { fontSize: 12, breakLine: true } },
    { text: "✓  Router decides; Spreedly never picks the gateway itself", options: { fontSize: 12, breakLine: true } },
    { text: "✓  Constraint: Spreedly rate limits surface as the MaxConcurrencyPerGateway admin parameter", options: { fontSize: 12 } }
  ], { x: 0.6, y: 1.5, w: 6.2, h: 5.5, fontFace: BODY_FONT, color: INK, valign: "top", margin: 0, paraSpaceAfter: 4 });

  // Right: hub-and-spoke diagram
  const cx = 10.0, cy = 4.5;
  // central node
  s.addShape(pres.shapes.OVAL, {
    x: cx - 0.9, y: cy - 0.9, w: 1.8, h: 1.8,
    fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("SPREEDLY\nVAULT", {
    x: cx - 0.9, y: cy - 0.6, w: 1.8, h: 1.2,
    fontFace: HEADER_FONT, fontSize: 13, bold: true, color: WHITE, align: "center", valign: "middle", margin: 0
  });
  // member node
  s.addShape(pres.shapes.OVAL, {
    x: cx - 3.2, y: cy - 0.5, w: 1.6, h: 1.0,
    fill: { color: ICE }, line: { color: NAVY }
  });
  s.addText("MEMBER\n(1 token)", {
    x: cx - 3.2, y: cy - 0.45, w: 1.6, h: 0.9,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: NAVY, align: "center", valign: "middle", margin: 0
  });
  // line from member to spreedly
  s.addShape(pres.shapes.LINE, {
    x: cx - 1.6, y: cy, w: 0.7, h: 0,
    line: { color: NAVY, width: 2 }
  });

  // spokes to gateways
  const gateways = ["NMI Spreedly", "NMI Direct", "Checkout EUR", "Checkout US", "Checkout US LLC", "Shift4", "Stripe EMS"];
  const angleStart = -80, angleEnd = 80;
  const r = 2.4;
  gateways.forEach((g, i) => {
    const t = i / (gateways.length - 1);
    const ang = (angleStart + t * (angleEnd - angleStart)) * Math.PI / 180;
    const tx = cx + Math.cos(ang) * r;
    const ty = cy + Math.sin(ang) * r;
    // small pill
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
      x: tx, y: ty - 0.18, w: 1.4, h: 0.36,
      fill: { color: WHITE }, line: { color: NAVY, width: 1 }, rectRadius: 0.06
    });
    s.addText(g, {
      x: tx, y: ty - 0.18, w: 1.4, h: 0.36,
      fontFace: BODY_FONT, fontSize: 9, bold: true, color: NAVY,
      align: "center", valign: "middle", margin: 0
    });
    // connector from spreedly to pill
    const startX = cx + Math.cos(ang) * 0.9;
    const startY = cy + Math.sin(ang) * 0.9;
    s.addShape(pres.shapes.LINE, {
      x: startX, y: startY, w: tx - startX, h: ty - startY,
      line: { color: ICE, width: 1.5 }
    });
  });
  footer(s, 4, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 5 — Processor catalog
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Card processor catalog", "03 · Catalog");

  const head = ["Processor", "Display name", "Refund", "Recurring", "Notes"];
  const rows = [
    ["NmiSpreedly",   "NMI (Spreedly Vault)", "✓", "✓", "Primary NMI in routing rules"],
    ["NmiDirect",     "NMI Direct",           "✓", "—", "Fallback step in signup chain only"],
    ["CheckoutEUR",   "Checkout.com EUR",     "✓", "✓", "Europe / UK / Russia / Australia / catch-all"],
    ["CheckoutUS",    "Checkout.com US",      "✓", "✓", "USA + Canada"],
    ["CheckoutUsLlc", "Checkout US LLC",      "✓", "—", "Latin America split partner"],
    ["Shift4",        "Shift4",               "✓", "✓", "Europe + Russia split partner"],
    ["StripeEms",     "Stripe EMS",           "✓", "✓", "Maestro / Bancontact / JCB + last-resort fallback"]
  ];

  const data = [
    head.map(c => ({ text: c, options: { fill: { color: NAVY }, color: WHITE, bold: true, fontFace: BODY_FONT, fontSize: 11, valign: "middle" } })),
    ...rows.map(r => r.map((c, idx) => ({
      text: c,
      options: {
        color: idx === 0 ? NAVY : INK,
        bold: idx === 0,
        fontFace: BODY_FONT, fontSize: 11, valign: "middle",
        fill: { color: WHITE }
      }
    })))
  ];

  s.addTable(data, {
    x: 0.6, y: 1.7, w: 12.1,
    colW: [1.8, 2.4, 0.9, 1.1, 5.9],
    border: { type: "solid", pt: 0.5, color: "E2E8F0" },
    rowH: 0.45
  });

  // callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 6.0, w: 12.1, h: 0.75,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText('"NMI" in any routing rule always means NmiSpreedly. NmiDirect is only a step in the signup fallback chain.', {
    x: 0.8, y: 6.0, w: 11.7, h: 0.75,
    fontFace: BODY_FONT, fontSize: 13, italic: true, color: ICE,
    valign: "middle", margin: 0
  });
  footer(s, 5, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 6 — Routing matrix
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Routing matrix — country × brand → split", "04 · Routing");

  const rows = [
    ["Europe (group)",      "Visa / MasterCard", "CheckoutEUR 60 % · Shift4 40 %"],
    ["Russia bloc",         "Visa / MasterCard", "CheckoutEUR 50 % · Shift4 50 %"],
    ["South Korea",         "Visa / MasterCard", "NMI 100 %"],
    ["Japan",               "Visa / MasterCard", "NMI 100 %"],
    ["Canada",              "Visa / MasterCard", "CheckoutUS 100 %  (CAD +2 %)"],
    ["Australia",           "Visa / MasterCard", "CheckoutEUR 100 %  (AUD +2 %)"],
    ["USA",                 "Visa / MasterCard", "CheckoutUS 40 % · NMI 60 %"],
    ["United Kingdom",      "Visa / MasterCard", "CheckoutEUR 100 %  (GBP +2 %)"],
    ["Latin America (grp)", "Visa / MasterCard", "NMI 50 % · Checkout US LLC 50 %"],
    ["Catch-all (other)",   "Visa / MasterCard", "CheckoutEUR 100 %"],
    ["Anywhere",            "Amex",              "CheckoutEUR or CheckoutUS  ·  USD only"],
    ["Anywhere",            "Maestro · Bancontact · JCB", "Stripe EMS 100 %"]
  ];
  const head = ["Country / group", "Card brand", "Split"];

  const data = [
    head.map(c => ({ text: c, options: { fill: { color: NAVY }, color: WHITE, bold: true, fontFace: BODY_FONT, fontSize: 11, valign: "middle" } })),
    ...rows.map(r => r.map((c, idx) => ({
      text: c,
      options: {
        color: idx === 0 ? NAVY : INK,
        bold: idx === 0,
        fontFace: BODY_FONT, fontSize: 11, valign: "middle",
        fill: { color: WHITE }
      }
    })))
  ];

  s.addTable(data, {
    x: 0.6, y: 1.6, w: 9.0,
    colW: [2.8, 2.6, 3.6],
    border: { type: "solid", pt: 0.5, color: "E2E8F0" },
    rowH: 0.34
  });

  // side panel — specificity ladder
  card(s, 9.8, 1.6, 3.0, 5.4, "Most specific wins",
    [
      { text: "1. Exact ISO country", options: { bold: true, breakLine: true, color: NAVY } },
      { text: "↓", options: { color: MUTED, breakLine: true } },
      { text: "2. Country group", options: { bold: true, breakLine: true, color: NAVY } },
      { text: "↓", options: { color: MUTED, breakLine: true } },
      { text: "3. Brand catch-all", options: { bold: true, breakLine: true, color: NAVY } },
      { text: "↓", options: { color: MUTED, breakLine: true } },
      { text: "4. Global catch-all", options: { bold: true, breakLine: true, color: NAVY } },
      { text: " ", options: { breakLine: true } },
      { text: "Country groups (Europe / LatAm / RussiaBloc) are editable from admin — the seeded lists match the rules document exactly.", options: { fontSize: 11, color: MUTED } }
    ], "0EA5E9");
  footer(s, 6, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 7 — Deterministic counter
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "% split — deterministic counter, not random", "05 · The algorithm");

  s.addText([
    { text: "60/40 means exactly 60/40, every day, every bucket.", options: { bold: true, color: NAVY, fontSize: 18 } }
  ], { x: 0.6, y: 1.5, w: 12, h: 0.5, fontFace: BODY_FONT, margin: 0 });

  // Two columns: "How" vs "Why not random"
  card(s, 0.6, 2.1, 6.1, 4.7, "How the counter works",
    [
      { text: "1.", options: { bold: true, color: NAVY, breakLine: false } },
      { text: " One GatewayRoutingCounter row per processor per bucket (OperationType + CardBrand + match).", options: { breakLine: true } },
      { text: "2.", options: { bold: true, color: NAVY, breakLine: false } },
      { text: " On each charge, compute every processor's current share. Pick the one furthest below its target.", options: { breakLine: true } },
      { text: "3.", options: { bold: true, color: NAVY, breakLine: false } },
      { text: " Increment its counter inside the same DB transaction as the charge.", options: { breakLine: true } },
      { text: "4.", options: { bold: true, color: NAVY, breakLine: false } },
      { text: " Charge rolls back → counter rolls back. Always.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Result: long-run ratios are exact, not statistical. Audit-friendly per bucket.", options: { italic: true, color: MUTED } }
    ], NAVY);
  card(s, 7.0, 2.1, 5.7, 4.7, "Why not weighted random",
    [
      { text: "Weighted random drifts with small samples.", options: { bold: true, color: NAVY, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Example: 50 charges in a day with a 60 / 40 split.", options: { breakLine: true } },
      { text: "• Random: anywhere between 24/26 and 36/14 in a single day.", options: { breakLine: true } },
      { text: "• Deterministic: exactly 30 / 20 every time.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "With deterministic counters, support can answer:", options: { color: MUTED, breakLine: true } },
      { text: "\"What % did NMI actually get on Visa-in-USA last week?\"", options: { italic: true, color: NAVY, breakLine: true } },
      { text: "without sampling error to argue about.", options: { color: MUTED } }
    ], "8B5CF6");
  footer(s, 7, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 8 — Currency presentment
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Currency presentment & conversion", "06 · Currency");

  const rows = [
    ["Europe (Visa/MC)",       "EUR", "+2 %"],
    ["United Kingdom",         "GBP", "+2 %"],
    ["Canada",                 "CAD", "+2 %"],
    ["Australia",              "AUD", "+2 %"],
    ["Everywhere else",        "USD", "—"],
    ["Amex (regardless)",      "USD", "—"]
  ];
  const head = ["Region", "Presentment", "Markup"];
  const data = [
    head.map(c => ({ text: c, options: { fill: { color: NAVY }, color: WHITE, bold: true, fontFace: BODY_FONT, fontSize: 12, valign: "middle" } })),
    ...rows.map(r => r.map((c, idx) => ({
      text: c,
      options: {
        color: idx === 0 ? NAVY : INK,
        bold: idx === 0,
        fontFace: BODY_FONT, fontSize: 12, valign: "middle",
        fill: { color: WHITE }
      }
    })))
  ];
  s.addTable(data, {
    x: 0.6, y: 1.6, w: 6.0,
    colW: [3.0, 1.5, 1.5],
    border: { type: "solid", pt: 0.5, color: "E2E8F0" },
    rowH: 0.45
  });

  // Right column — conversion flow
  card(s, 7.0, 1.6, 5.7, 5.4, "Exchange rate flow",
    [
      { text: "Source", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "currencyconverterapi.com  (API key in ApiCredential, admin-editable)", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Cache", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Redis  exchange:USD:{currency}  ·  TTL 1 hour", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Refresh job", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "ExchangeRateRefreshJob  ·  Hangfire queue billing  ·  hourly", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Stale-on-error fallback", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "If Redis misses and the live call fails, read the most recent ExchangeRateSnapshot from SQL. Never a hard failure.", options: {} }
    ], "0EA5E9");
  footer(s, 8, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 9 — Fallback chains
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Fallback chains by operation type", "07 · Fallback");

  function chain(s, x, y, title, steps, color) {
    s.addText(title.toUpperCase(), {
      x, y, w: 4.0, h: 0.32,
      fontFace: BODY_FONT, fontSize: 11, bold: true, color: MUTED, charSpacing: 3, margin: 0
    });
    let cy = y + 0.4;
    steps.forEach((step, idx) => {
      const note = step.note;
      s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
        x, y: cy, w: 4.0, h: 0.5,
        fill: { color: step.primary ? color : WHITE },
        line: { color, width: 1.2 }, rectRadius: 0.08
      });
      s.addText(step.label, {
        x, y: cy, w: 4.0, h: 0.5,
        fontFace: BODY_FONT, fontSize: 12, bold: true,
        color: step.primary ? WHITE : NAVY,
        align: "center", valign: "middle", margin: 0
      });
      if (note) {
        s.addText(note, {
          x: x + 4.05, y: cy, w: 1.5, h: 0.5,
          fontFace: BODY_FONT, fontSize: 9, italic: true, color: MUTED,
          valign: "middle", margin: 0
        });
      }
      cy += 0.5;
      if (idx < steps.length - 1) {
        s.addShape(pres.shapes.RIGHT_ARROW, {
          x: x + 1.85, y: cy, w: 0.3, h: 0.22,
          fill: { color }, line: { color }, rotate: 90
        });
        cy += 0.27;
      }
    });
  }

  chain(s, 0.6, 1.5, "Signup / one-off payment", [
    { label: "NMI Spreedly", primary: true },
    { label: "NMI Direct" },
    { label: "Checkout US" },
    { label: "Stripe EMS" }
  ], NAVY);

  chain(s, 5.0, 1.5, "Recurring (USA / Canada)", [
    { label: "NMI Spreedly", primary: true },
    { label: "Checkout US", note: "+60 min delay" }
  ], "8B5CF6");
  // Second recurring chain below
  chain(s, 5.0, 4.8, "Recurring — alternate primary", [
    { label: "Checkout US", primary: true },
    { label: "NMI Spreedly", note: "+60 min delay" }
  ], "8B5CF6");

  chain(s, 9.3, 1.5, "Card authorization", [
    { label: "NMI Spreedly", primary: true },
    { label: "Checkout US" },
    { label: "Stripe EMS" }
  ], "0EA5E9");

  // Universal exception callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 6.55, w: 12.1, h: 0.65,
    fill: { color: WARN }, line: { color: WARN }, rectRadius: 0.08
  });
  s.addText("Universal exception: any NMI fallback step is forced to USD regardless of the primary's presentment. Stripe fallback keeps the presented currency.", {
    x: 0.8, y: 6.55, w: 11.7, h: 0.65,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: NAVY_DEEP,
    valign: "middle", margin: 0
  });
  footer(s, 9, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 10 — Recurring plans comparison
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Recurring plans — the two seeded today", "08 · Plans");

  function planCard(x, y, w, h, name, products, cycle, cadence, terminal, stop, fee, accent) {
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w, h, fill: { color: WHITE }, line: { color: ICE, width: 1 },
      shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w, h: 0.65, fill: { color: accent }, line: { color: accent }
    });
    s.addText(name, {
      x: x + 0.2, y, w: w - 0.4, h: 0.65,
      fontFace: HEADER_FONT, fontSize: 18, bold: true, color: WHITE,
      valign: "middle", margin: 0
    });
    const rows = [
      ["Products", products],
      ["Cycle", cycle],
      ["Retry cadence (days)", cadence],
      ["On all retries fail", terminal],
      ["Stop after unbilled", stop],
      ["Fee column", fee],
      ["Pay from commission first", "✓  (default — admin-editable)"]
    ];
    let cy = y + 0.85;
    rows.forEach(r => {
      s.addText(r[0], {
        x: x + 0.25, y: cy, w: 1.9, h: 0.45,
        fontFace: BODY_FONT, fontSize: 11, bold: true, color: MUTED,
        valign: "top", margin: 0, charSpacing: 1
      });
      s.addText(r[1], {
        x: x + 2.2, y: cy, w: w - 2.4, h: 0.45,
        fontFace: BODY_FONT, fontSize: 12, color: INK,
        valign: "top", margin: 0
      });
      cy += 0.6;
    });
  }

  planCard(0.6, 1.55, 6.1, 5.4, "Travel Advantage",
    "Elite · VIP · Turbo (three products)",
    "Every 30 days (from last successful billing)",
    "1, 2, 2, 2, 2, 2",
    "Retry on monthly anniversary → repeat next month",
    "90 days  →  membership = HoldByBilling",
    "Product.MonthlyFee",
    NAVY);

  planCard(6.9, 1.55, 5.8, 5.4, "Lifestyle Ambassador",
    "Lifestyle Ambassador (single product)",
    "Annual (from last successful billing)",
    "1, 1, 1, 2, 2, 5, 5",
    "Mark membership Expired (\"Inactive\")",
    "(never auto-stops)",
    "Product.AnnualPrice",
    "8B5CF6");

  s.addText("Admin can add new plans, change cadences, swap products. Subscriptions whose product has no plan continue under the legacy MembershipAutoRenewalJob.", {
    x: 0.6, y: 7.0, w: 12.1, h: 0.35,
    fontFace: BODY_FONT, fontSize: 11, italic: true, color: MUTED, margin: 0
  });
  footer(s, 10, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 11 — Retry cadence timeline
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Retry cadence — offset is from the previous attempt", "09 · Cadence");

  s.addText("Worked example — Travel Advantage cadence (1, 2, 2, 2, 2, 2), bill due May 1.", {
    x: 0.6, y: 1.5, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 13, italic: true, color: MUTED, margin: 0
  });

  // Timeline track
  const tY = 3.0;
  s.addShape(pres.shapes.LINE, {
    x: 0.8, y: tY, w: 11.7, h: 0,
    line: { color: ICE, width: 3 }
  });
  const attempts = [
    { date: "May 1",  label: "1st attempt", offset: "(NextBillingDate)", primary: true },
    { date: "May 2",  label: "Retry 1",     offset: "+1 day" },
    { date: "May 4",  label: "Retry 2",     offset: "+2 days" },
    { date: "May 6",  label: "Retry 3",     offset: "+2 days" },
    { date: "May 8",  label: "Retry 4",     offset: "+2 days" },
    { date: "May 10", label: "Retry 5",     offset: "+2 days" },
    { date: "May 12", label: "Retry 6",     offset: "+2 days" }
  ];
  const step = 11.7 / (attempts.length - 1);
  attempts.forEach((a, i) => {
    const x = 0.8 + i * step;
    s.addShape(pres.shapes.OVAL, {
      x: x - 0.18, y: tY - 0.18, w: 0.36, h: 0.36,
      fill: { color: a.primary ? NAVY : WHITE },
      line: { color: NAVY, width: 2 }
    });
    s.addText(a.date, {
      x: x - 0.6, y: tY + 0.32, w: 1.2, h: 0.3,
      fontFace: BODY_FONT, fontSize: 12, bold: true, color: NAVY,
      align: "center", valign: "top", margin: 0
    });
    s.addText(a.label, {
      x: x - 0.7, y: tY + 0.65, w: 1.4, h: 0.3,
      fontFace: BODY_FONT, fontSize: 10, color: MUTED,
      align: "center", valign: "top", margin: 0
    });
    s.addText(a.offset, {
      x: x - 0.7, y: tY + 0.92, w: 1.4, h: 0.3,
      fontFace: BODY_FONT, fontSize: 9, italic: true, color: a.primary ? NAVY : MUTED,
      align: "center", valign: "top", margin: 0
    });
  });

  // After all retries fail
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 5.4, w: 12.1, h: 1.6,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText("All retries fail?", {
    x: 0.9, y: 5.5, w: 4, h: 0.45,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: ICE, charSpacing: 2, margin: 0
  });
  s.addText([
    { text: "Travel Advantage  ", options: { bold: true, color: WHITE } },
    { text: "→ jump to the monthly anniversary (same day-of-month as enrollment, next month) and run the cadence again. When 90 days elapse since the last successful billing, membership becomes ", options: { color: ICE } },
    { text: "HoldByBilling.", options: { bold: true, color: WARN } }
  ], { x: 0.9, y: 6.0, w: 11.5, h: 0.5, fontFace: BODY_FONT, fontSize: 12, margin: 0 });
  s.addText([
    { text: "Lifestyle Ambassador  ", options: { bold: true, color: WHITE } },
    { text: "→ membership becomes ", options: { color: ICE } },
    { text: "Expired", options: { bold: true, color: WARN } },
    { text: '. Engine stops. Member must contact support or use Biz Center to be billed manually.', options: { color: ICE } }
  ], { x: 0.9, y: 6.5, w: 11.5, h: 0.5, fontFace: BODY_FONT, fontSize: 12, margin: 0 });
  footer(s, 11, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 12 — Commission-balance-first funding
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Commission-balance-first funding", "10 · Funding");

  s.addText("Applied on every recurring bill attempt — for any plan with PayFromCommissionBalanceFirst = true.", {
    x: 0.6, y: 1.5, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 13, italic: true, color: MUTED, margin: 0
  });

  // Available balance breakdown
  card(s, 0.6, 2.05, 5.7, 4.9, "Available balance",
    [
      { text: "generalPending", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "= Σ CommissionEarning where Status = Pending  (includes prior consolidated DR credits AND prior negative token debits)", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "dailyResidualPending", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "= Σ DailyResidualEarning where Status = Pending", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "eligibleDailyResidual", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "= dailyResidualPending ≥ DailyResidualConsolidationMinimum  ?  dailyResidualPending  :  0", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "available = generalPending + eligibleDailyResidual", options: { bold: true, fontSize: 13, color: NAVY } }
    ], NAVY);

  // Decision flow
  card(s, 6.5, 2.05, 6.2, 4.9, "If available ≥ fee  →  pay from commissions",
    [
      { text: "1.  Consolidate eligible Daily Residual rows", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "    Mark them Paid, set PaymentDate / CommentedBy / PaymentComment. Create one CommissionEarning credit row for the total.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "2.  Write the negative debit", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "    New CommissionEarning row, Amount = -fee, Pending.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "3.  Issue product token", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "    TokenTransaction (qty 1) of the plan's TokenType + bump TokenBalance.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "4.  Create the order", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "    Paid Orders + OrderDetail + PaymentHistory (GatewayName = \"CommissionBalance\").", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: " ", options: { breakLine: true } },
      { text: "Else  →  charge the card for the full amount via the routing engine. No split.", options: { bold: true, italic: true, color: DANGER } }
    ], SUCCESS);
  footer(s, 12, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 13 — Daily Residual ledger
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Daily Residual ledger & Monday consolidation", "11 · Daily Residual");

  // Why a separate table
  card(s, 0.6, 1.55, 5.8, 5.4, "Why a separate table",
    [
      { text: "Per-day audit", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Each row carries snapshot fields explaining why it was created:", options: { breakLine: true } },
      { text: "•  CurrentRankId", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "•  EligibleDualTeamPoints / EligibleEnrollmentTeamPoints", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "•  PersonalPoints", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: " ", options: { breakLine: true } },
      { text: "Payment tracking", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Set when the row goes Pending → Paid:", options: { breakLine: true } },
      { text: "•  PaymentDate  (from IDateTimeProvider)", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: '•  CommentedBy ("weekly-consolidation" / "membership-token-purchase" / …)', options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "•  PaymentComment  (descriptive movement note)", options: { fontSize: 11, color: MUTED } }
    ], "0EA5E9");

  // Monday flow
  card(s, 6.6, 1.55, 6.1, 5.4, "Monday consolidation flow",
    [
      { text: "Job:  DailyResidualConsolidationJob", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Hangfire queue commissions  ·  weekly, Mondays", options: { fontSize: 11, color: MUTED, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "For each member:", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Sum pending DailyResidualEarning rows.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "If Σ pending ≥ DailyResidualConsolidationMinimum:", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "→ mark them Paid with tracking fields", options: { fontSize: 12, color: SUCCESS, breakLine: true } },
      { text: "→ set ConsolidatedIntoCommissionEarningId", options: { fontSize: 12, color: SUCCESS, breakLine: true } },
      { text: "→ create one CommissionEarning credit for the total", options: { fontSize: 12, color: SUCCESS, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Below the minimum  →  leave pending, try next Monday.", options: { italic: true, color: MUTED } }
    ], SUCCESS);

  // Threshold callout at bottom
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 7.05, w: 12.1, h: 0.3,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.04
  });
  s.addText("Threshold default $100  ·  configurable in admin (GlobalParameter \"DailyResidualConsolidationMinimum\")  ·  same mechanic also fires ad-hoc when a recurring bill is about to be funded from commissions.", {
    x: 0.8, y: 7.05, w: 11.7, h: 0.3,
    fontFace: BODY_FONT, fontSize: 10, color: ICE, italic: true, valign: "middle", margin: 0
  });
  footer(s, 13, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 14 — Membership status semantics
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Membership status — six values, two are dangerously similar", "12 · Status");

  const states = [
    { code: "Active",        desc: "Current, in good standing.",                                color: SUCCESS, set: "Successful signup / renewal" },
    { code: "Pending",       desc: "Awaiting first activation.",                                color: WARN,    set: "Initial signup pre-payment" },
    { code: "OnHold",        desc: "SUPPORT-INITIATED pause. No charges, no attempts. Resumes normal billing automatically when the support-defined pause ends.", color: "8B5CF6", set: "Support / admin" },
    { code: "Cancelled",     desc: "Member or support cancelled.",                              color: MUTED,   set: "Member action / admin" },
    { code: "Expired",       desc: "Term elapsed without renewal. Lifestyle's MarkExpired lands here.", color: DANGER,  set: "Recurring engine / legacy renewal job" },
    { code: "HoldByBilling", desc: "SYSTEM-INITIATED stop. Travel Advantage hit the 90-day cap. Engine stops. Reactivated only by a successful manual bill.", color: WARN, set: "Recurring engine (Travel terminal)" }
  ];

  // 3 columns × 2 rows
  states.forEach((st, idx) => {
    const col = idx % 3, row = Math.floor(idx / 3);
    const x = 0.6 + col * 4.2;
    const y = 1.55 + row * 2.7;
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w: 4.0, h: 2.5, fill: { color: WHITE }, line: { color: ICE, width: 1 },
      shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w: 4.0, h: 0.5, fill: { color: st.color }, line: { color: st.color }
    });
    s.addText(st.code, {
      x: x + 0.2, y, w: 3.7, h: 0.5,
      fontFace: HEADER_FONT, fontSize: 17, bold: true, color: WHITE, valign: "middle", margin: 0
    });
    s.addText(st.desc, {
      x: x + 0.2, y: y + 0.6, w: 3.7, h: 1.4,
      fontFace: BODY_FONT, fontSize: 11.5, color: INK, valign: "top", margin: 0
    });
    s.addText(st.set, {
      x: x + 0.2, y: y + 2.05, w: 3.7, h: 0.4,
      fontFace: BODY_FONT, fontSize: 10, italic: true, color: MUTED, valign: "top", margin: 0
    });
  });

  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 7.0, w: 12.1, h: 0.4,
    fill: { color: DANGER }, line: { color: DANGER }, rectRadius: 0.05
  });
  s.addText("Critical:  OnHold (support pause, auto-resumes)  ≠  HoldByBilling (engine stop, manual revive only).  Different semantics, different recovery paths.", {
    x: 0.8, y: 7.0, w: 11.7, h: 0.4,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: WHITE, valign: "middle", margin: 0
  });
  footer(s, 14, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 15 — High-volume processing architecture
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "High-volume processing — four-stage pipeline (planned)", "13 · Volume");

  s.addText("Status: designed, not yet implemented.  Goal: complete the day's billing inside a 3-hour window starting 01:00 ET, with worker counts derived dynamically from the day's actual case count.", {
    x: 0.6, y: 1.45, w: 12.1, h: 0.5,
    fontFace: BODY_FONT, fontSize: 12, italic: true, color: MUTED, margin: 0
  });

  // 4 stages as cards in a row with arrows between
  const stages = [
    { num: "1", title: "Planning pass", body: "Count due cases · resolve gateway · read avg latency · compute WorkersNeeded per processor · write batch + shards", time: "~ 1 min, 1 instance", color: NAVY },
    { num: "2", title: "Charge workers", body: "N per processor, queues billing-{processor}. Each owns an id-range shard (zero overlap). Updates the member's own state only · emits PointDeltaEvent rows", time: "01:00 – 04:00 ET", color: "0EA5E9" },
    { num: "3", title: "Upline aggregator", body: "Reduces PointDeltaEvent rows to net delta per upline · one UPDATE per upline · in transaction with events → Applied", time: "~ 04:00 ET, 1 instance", color: "8B5CF6" },
    { num: "4", title: "Downstream triggers", body: "Rank evaluation · FSB / Boost second half · push notifications · scoped only to members touched in this batch", time: "~ 04:15 ET, parallel", color: SUCCESS }
  ];

  const sw = 2.95, sh = 4.5, sy = 2.05, sx0 = 0.5;
  stages.forEach((st, i) => {
    const x = sx0 + i * (sw + 0.15);
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: sw, h: sh, fill: { color: WHITE }, line: { color: ICE, width: 1 },
      shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: sw, h: 0.55, fill: { color: st.color }, line: { color: st.color }
    });
    s.addText(`STAGE ${st.num}`, {
      x: x + 0.18, y: sy, w: sw - 0.36, h: 0.55,
      fontFace: BODY_FONT, fontSize: 11, bold: true, color: WHITE,
      valign: "middle", margin: 0, charSpacing: 3
    });
    s.addText(st.title, {
      x: x + 0.18, y: sy + 0.7, w: sw - 0.36, h: 0.5,
      fontFace: HEADER_FONT, fontSize: 16, bold: true, color: NAVY, margin: 0
    });
    s.addText(st.body, {
      x: x + 0.18, y: sy + 1.3, w: sw - 0.36, h: sh - 1.8,
      fontFace: BODY_FONT, fontSize: 11, color: INK, valign: "top", margin: 0,
      paraSpaceAfter: 3
    });
    s.addText(st.time, {
      x: x + 0.18, y: sy + sh - 0.5, w: sw - 0.36, h: 0.35,
      fontFace: BODY_FONT, fontSize: 10, italic: true, bold: true, color: st.color,
      valign: "bottom", margin: 0
    });
    // arrow between stages
    if (i < stages.length - 1) {
      s.addShape(pres.shapes.RIGHT_ARROW, {
        x: x + sw - 0.04, y: sy + sh / 2 - 0.12, w: 0.3, h: 0.24,
        fill: { color: ICE }, line: { color: ICE }
      });
    }
  });

  // bottom strip — key insight
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.5, y: 6.85, w: 12.3, h: 0.5,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.06
  });
  s.addText("Key insight  ·  the aggregator collapses thousands of point updates per upline into one batched UPDATE — eliminating hot-key contention and state-oscillation writes.", {
    x: 0.7, y: 6.85, w: 11.9, h: 0.5,
    fontFace: BODY_FONT, fontSize: 11, italic: true, color: ICE, valign: "middle", margin: 0
  });
  footer(s, 15, 17);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 16 — Ten hard rules
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addText("TEN HARD RULES", {
    x: 0.6, y: 0.45, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: ICE, charSpacing: 4, margin: 0
  });
  s.addText("Never violate these.", {
    x: 0.6, y: 0.85, w: 12, h: 0.7,
    fontFace: HEADER_FONT, fontSize: 32, bold: true, color: WHITE, margin: 0
  });

  const rules = [
    "Secrets are always encrypted (ENC: prefix). Plain text throws WalletPasswordStorageException.",
    "Time always comes from IDateTimeProvider.  Never DateTime.Now / DateTime.UtcNow directly.",
    "Card data never touches our DB. Only the Spreedly token. No PAN, no CVV, no expiry beyond masked last-4.",
    "Routing decisions are always made on our side. Spreedly is a dumb proxy — adaptive features off.",
    "Counter increments happen inside the charge transaction. Roll back the charge → roll back the counter.",
    "Commission funding is all-or-nothing per source. Either commissions or card pays the full fee, never split.",
    "Daily residuals are consolidated, never deleted. Rows go Paid with PaymentDate / CommentedBy / PaymentComment.",
    "OnHold ≠ HoldByBilling. Code that checks one must never assume the other.",
    "Migrations are generated AND applied. A pending migration is an incomplete change.",
    "No business logic in controllers. Controllers orchestrate; the Billing project owns the logic."
  ];

  const cols = 2, rows = 5;
  const cellW = 6.0, cellH = 0.95;
  const x0 = 0.6, y0 = 1.9;
  rules.forEach((r, i) => {
    const col = i % cols, row = Math.floor(i / cols);
    const x = x0 + col * (cellW + 0.3);
    const y = y0 + row * (cellH + 0.1);
    // number bubble
    s.addShape(pres.shapes.OVAL, {
      x, y: y + 0.1, w: 0.55, h: 0.55,
      fill: { color: ICE }, line: { color: ICE }
    });
    s.addText(`${i + 1}`, {
      x, y: y + 0.1, w: 0.55, h: 0.55,
      fontFace: HEADER_FONT, fontSize: 16, bold: true, color: NAVY,
      align: "center", valign: "middle", margin: 0
    });
    s.addText(r, {
      x: x + 0.7, y, w: cellW - 0.7, h: cellH,
      fontFace: BODY_FONT, fontSize: 11.5, color: WHITE, valign: "middle", margin: 0
    });
  });

  s.addText("MLMConqueror — Billing Reference · v1.0 · 2026-05-13",
    { x: 0.5, y: H - 0.34, w: 12, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, valign: "middle", margin: 0 });
  s.addText("16 / 17",
    { x: W - 1.5, y: H - 0.34, w: 1.0, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, align: "right", valign: "middle", margin: 0 });
}

// ─────────────────────────────────────────────────────────────────────
// Slide 17 — Closing / where things live
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addShape(pres.shapes.RECTANGLE, {
    x: 1, y: 1.2, w: 1.2, h: 0.04, fill: { color: ICE }, line: { color: ICE }
  });
  s.addText("WHERE THINGS LIVE", {
    x: 1, y: 0.6, w: 11, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: ICE, charSpacing: 4, margin: 0
  });
  s.addText("Code locations & references", {
    x: 1, y: 1.4, w: 11, h: 0.8,
    fontFace: HEADER_FONT, fontSize: 32, bold: true, color: WHITE, margin: 0
  });

  const refs = [
    ["Domain entities & enums",   "Domain/Entities/Billing/ · Domain/Entities/Commission/ · Domain/Enums/"],
    ["EF configs & migrations",   "Repository/Configurations/ · Repository/Migrations/"],
    ["Gateway routing services",  "Billing/Services/Routing/"],
    ["Card gateway clients",      "Billing/Services/CardGateway/  (Spreedly proxy)"],
    ["Recurring services",        "Billing/Services/Recurring/"],
    ["Currency conversion",       "Billing/Services/CurrencyConversionService.cs"],
    ["Hangfire jobs — billing",   "Billing/Jobs/"],
    ["Hangfire jobs — commissions","CommissionEngine/Jobs/"],
    ["Seeders",                   "Repository/Seeders/GatewayRoutingSeeder.cs · RecurringBillingSeeder.cs"],
    ["Admin controllers",         "AdminAPI/Controllers/AdminBillingGatewayController.cs · AdminRecurringBillingController.cs"],
    ["Admin UI",                  "AdminWeb/Components/Pages/AdminBilling*.razor · AdminRecurring*.razor"],
    ["Sidebar menu",              "SharedComponents/Components/Layout/AdminSidebarMenu.razor"]
  ];

  let y = 2.4;
  refs.forEach(r => {
    s.addText(r[0], {
      x: 1.0, y, w: 4.2, h: 0.36,
      fontFace: BODY_FONT, fontSize: 12, bold: true, color: ICE, valign: "middle", margin: 0
    });
    s.addText(r[1], {
      x: 5.3, y, w: 7.5, h: 0.36,
      fontFace: "Consolas", fontSize: 11, color: WHITE, valign: "middle", margin: 0
    });
    y += 0.36;
  });

  s.addText("Code wins  ·  this deck second  ·  spec docs in /docs/superpowers/specs/ for history & rationale.", {
    x: 1.0, y: H - 0.85, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, italic: true, color: ICE, margin: 0
  });

  s.addText("MLMConqueror — Billing Reference · v1.0 · 2026-05-13",
    { x: 0.5, y: H - 0.34, w: 12, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, valign: "middle", margin: 0 });
  s.addText("17 / 17",
    { x: W - 1.5, y: H - 0.34, w: 1.0, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, align: "right", valign: "middle", margin: 0 });
}

// Write file
pres.writeFile({ fileName: path.join(__dirname, "Billing-Workflow.pptx") })
    .then(p => console.log("Wrote:", p));
