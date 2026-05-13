// Build the MLMConqueror Commissions Workflow deck.
// Palette: Midnight Executive base + violet accent (to distinguish from the
// billing deck while keeping the same visual family).
// Run with: node build-deck.js

const path = require("path");
const pptxgen = require("pptxgenjs");

const NAVY = "1E2761";
const NAVY_DEEP = "0F1838";
const ICE = "CADCFC";
const WHITE = "FFFFFF";
const INK = "1E293B";
const MUTED = "64748B";
const SUBTLE_BG = "F8FAFC";

// Differentiating accents — every "commission stream" gets its own color
const VIOLET = "8B5CF6";
const TEAL = "0EA5E9";
const EMERALD = "10B981";
const AMBER = "F59E0B";
const ROSE = "EC4899";
const INDIGO = "6366F1";
const DANGER = "DC2626";

const HEADER_FONT = "Georgia";
const BODY_FONT = "Calibri";

const pres = new pptxgen();
pres.layout = "LAYOUT_WIDE";   // 13.333" × 7.5"
pres.author = "Sagara Media Group";
pres.title = "MLMConqueror Commissions — Rules & Workflow";
pres.subject = "Reference deck for the commissions subsystem";
const W = 13.333, H = 7.5;

// ─────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────
function headerBar(slide, title, eyebrow) {
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
  slide.addShape(pres.shapes.OVAL, {
    x: 0.25, y: eyebrow ? 0.92 : 0.72, w: 0.18, h: 0.18,
    fill: { color: VIOLET }, line: { color: VIOLET }
  });
}

function footer(slide, slideNum, total) {
  slide.addShape(pres.shapes.RECTANGLE, {
    x: 0, y: H - 0.36, w: W, h: 0.36, fill: { color: NAVY }, line: { color: NAVY }
  });
  slide.addText("MLMConqueror — Commissions Reference · v1.0 · 2026-05-13", {
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

const TOTAL = 16;

// ─────────────────────────────────────────────────────────────────────
// Slide 1 — Title
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addShape(pres.shapes.RECTANGLE, {
    x: 1, y: 4.2, w: 1.2, h: 0.04, fill: { color: VIOLET }, line: { color: VIOLET }
  });
  s.addText("MLMCONQUEROR · COMMISSIONS", {
    x: 1, y: 3.5, w: 11, h: 0.45,
    fontFace: BODY_FONT, fontSize: 14, bold: true, color: ICE,
    charSpacing: 6, margin: 0
  });
  s.addText("Rules & Workflow", {
    x: 1, y: 4.4, w: 11, h: 1.1,
    fontFace: HEADER_FONT, fontSize: 56, bold: true, color: WHITE, margin: 0
  });
  s.addText("Reference deck for engineers and AI assistants — the agreed rules of how every member gets paid, why each rule exists, and where to find it in code.", {
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
    ["01", "Six commission streams",         "What pays, when, and why"],
    ["02", "The two trees",                  "Dual Team (binary) · Enrollment (unilevel)"],
    ["03", "Points axes",                    "PP · DTP · EP · eligible vs raw"],
    ["04", "CommissionType taxonomy",        "One catalog row drives every payout"],
    ["05", "Sponsor Bonus & Fast Start",     "Real-time · 3 time-boxed windows"],
    ["06", "Daily Residual ledger",          "Own table · snapshot · Monday consolidation"],
    ["07", "Boost · Car · Presidential",     "Weekly, monthly, monthly pool"],
    ["08", "Matching Bonus",                 "Match your downline's earnings"],
    ["09", "Earnings lifecycle",             "Pending → Paid is terminal · reversals are new rows"],
    ["10", "Ranks & qualification",          "Per-leg caps · lifetime rank · evaluation cadence"],
    ["11", "Loyalty points",                 "Locked / Unlocked · MissedPayment flag"],
    ["12", "Job catalog",                    "Daily · weekly · monthly · sweeps"],
    ["13", "Commissions ↔ Billing",          "Integration seams"],
    ["14", "Ten hard rules",                 "Never violate these"],
  ];

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
      fontFace: HEADER_FONT, fontSize: 14, bold: true, color: VIOLET,
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
  footer(s, 2, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 3 — The six commission streams
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Six commission streams — six different questions answered", "01 · Streams");

  const streams = [
    { name: "Sponsor Bonus",      ask: "You sponsored someone.",                                                          when: "Real-time · on signup",                color: VIOLET },
    { name: "Fast Start Bonus",   ask: "You sold during one of your three launch windows.",                               when: "Real-time · on order",                  color: ROSE },
    { name: "Daily Residual",     ask: "Your binary legs accumulated points yesterday.",                                  when: "Nightly batch · 02:00 UTC",             color: TEAL },
    { name: "Boost Bonus",        ask: "You enrolled enough new Elite/Turbo members this week.",                          when: "Weekly · Sun 03:00 UTC + sweep",        color: AMBER },
    { name: "Car Bonus",          ask: "You hit the car-qualification threshold this month.",                             when: "Monthly · 1st 05:00 UTC + sweep",       color: EMERALD },
    { name: "Presidential Bonus", ask: "You're at the top — here's a share of the company pool.",                         when: "Monthly · 1st 04:00 UTC",               color: INDIGO },
  ];

  const cw = 4.0, ch = 2.5, gap = 0.25;
  streams.forEach((st, i) => {
    const col = i % 3, row = Math.floor(i / 3);
    const x = 0.6 + col * (cw + gap);
    const y = 1.55 + row * (ch + 0.25);
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w: cw, h: ch, fill: { color: WHITE }, line: { color: ICE, width: 1 },
      shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w: cw, h: 0.5, fill: { color: st.color }, line: { color: st.color }
    });
    s.addText(st.name, {
      x: x + 0.2, y, w: cw - 0.4, h: 0.5,
      fontFace: HEADER_FONT, fontSize: 16, bold: true, color: WHITE,
      valign: "middle", margin: 0
    });
    s.addText(st.ask, {
      x: x + 0.2, y: y + 0.6, w: cw - 0.4, h: 0.9,
      fontFace: BODY_FONT, fontSize: 13, italic: true, color: INK, valign: "top", margin: 0
    });
    s.addText(st.when, {
      x: x + 0.2, y: y + ch - 0.55, w: cw - 0.4, h: 0.45,
      fontFace: BODY_FONT, fontSize: 11, bold: true, color: st.color,
      valign: "bottom", margin: 0, charSpacing: 1
    });
  });

  // Plus matching bonus callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 6.9, w: 12.1, h: 0.42,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.06
  });
  s.addText("Plus  Matching Bonus  —  event-triggered when downline earnings land. Pays a % match on a parent earning row, configured via CommissionType.ResidualOverCommissionType.", {
    x: 0.8, y: 6.9, w: 11.7, h: 0.42,
    fontFace: BODY_FONT, fontSize: 11, color: ICE, italic: true, valign: "middle", margin: 0
  });
  footer(s, 3, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 4 — The two trees
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "The two trees — every member sits in both", "02 · Trees");

  // Dual Team card
  card(s, 0.6, 1.55, 6.0, 4.6, "Dual Team (binary)",
    [
      { text: "Every ambassador has a LEFT and RIGHT leg.", options: { breakLine: true } },
      { text: "Points accumulate per leg (uncapped storage).", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "At rank-evaluation time:", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "each leg's contribution is capped at MaxTeamPointsPerBranch × rank.TeamPoints (default 50 %).", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Source: DualTeamEntity  ·  HierarchyPath enables O(1) ancestor lookups (no recursion).", options: { fontSize: 11, color: MUTED } }
    ], TEAL);

  // Enrollment card
  card(s, 6.7, 1.55, 6.0, 4.6, "Enrollment (unilevel / genealogy)",
    [
      { text: 'The "who sponsored whom" tree.', options: { breakLine: true } },
      { text: "How deep the tree pays is per-commission-type — driven by CommissionType.LevelNo rows. There is no global depth cap.", options: { breakLine: true } },
      { text: "Tree depth and rank are orthogonal: a Black Royal can sit 60+ generations above another ambassador.", options: { italic: true, fontSize: 11, color: MUTED, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Per-branch ET cap:", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "each direct-sponsorship branch is capped at MaxEnrollmentTeamPointsPerBranch × rank.EnrollmentTeam.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Source: GenealogyEntity  ·  HierarchyPath  ·  feeds Sponsor + FSB + Matching.", options: { fontSize: 11, color: MUTED } }
    ], VIOLET);

  // Placement rules strip at bottom
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 6.3, w: 12.1, h: 0.9,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText("Placement window: 30 days from enrollment.", {
    x: 0.9, y: 6.3, w: 4, h: 0.9,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: WHITE, valign: "middle", margin: 0
  });
  s.addText("Unplacement: max 2 per member · only within 72 h of first placement.", {
    x: 4.9, y: 6.3, w: 5.5, h: 0.9,
    fontFace: BODY_FONT, fontSize: 12, color: ICE, valign: "middle", margin: 0
  });
  s.addText("Ghost Points: admin-only · leg-scoped · visible only to the upline.", {
    x: 0.9, y: 6.65, w: 11.5, h: 0.5,
    fontFace: BODY_FONT, fontSize: 11, italic: true, color: ICE, valign: "top", margin: 0
  });
  footer(s, 4, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 5 — Points axes
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Points axes — raw vs eligible, and where each lives", "03 · Points");

  const head = ["Axis", "Field", "Used by", "Cap?"];
  const rows = [
    ["Personal Points (PP)",   "MemberStatistic.PersonalPoints", "Rank thresholds, FSB",                       "—"],
    ["External Customer Pts",  "MemberStatistic.ExternalCustomerPoints", "Some rank/FSB qualifications",        "—"],
    ["Dual-Team Points (DTP)", "DualTeamEntity.{Left,Right}LegPoints", "Daily Residual, rank DT thresholds",   "Per-leg: MaxTeamPointsPerBranch"],
    ["Enrollment Points (EP)", "MemberStatistic.EnrollmentPoints + per-branch sums", "Rank ET thresholds",       "Per-branch: MaxEnrollmentTeamPointsPerBranch"],
    ["DualTeamSize / EnrollmentTeamSize", "MemberStatistic.*",          "Headcount thresholds",                 "—"],
    ["QualifiedSponsoredMembers",         "MemberStatistic.*",          "Sponsor counts / FSB unlocks",          "—"]
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
    x: 0.6, y: 1.6, w: 12.1,
    colW: [3.0, 3.2, 3.4, 2.5],
    border: { type: "solid", pt: 0.5, color: "E2E8F0" },
    rowH: 0.5
  });

  // Cap formula callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 5.9, w: 12.1, h: 1.3,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText("Capping math (used everywhere rank is evaluated)", {
    x: 0.9, y: 5.95, w: 11.7, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: ICE, charSpacing: 2, margin: 0
  });
  s.addText("CappedDualTeamTotal = min( min(left, perLegCap) + min(right, perLegCap),  threshold )", {
    x: 0.9, y: 6.4, w: 11.7, h: 0.4,
    fontFace: "Consolas", fontSize: 12, color: WHITE, margin: 0
  });
  s.addText("CappedEnrollmentTotal = min( Σ min(branch, perBranchCap),  threshold )       ·  threshold = 0  ⇒  axis opts OUT for this rank", {
    x: 0.9, y: 6.78, w: 11.7, h: 0.4,
    fontFace: "Consolas", fontSize: 12, color: WHITE, margin: 0
  });
  footer(s, 5, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 6 — CommissionType taxonomy
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "CommissionType — one catalog row drives every payout", "04 · Taxonomy");

  s.addText("Most types are not hardcoded. Adding a new commission type is mostly a catalog row + a CommissionCategory + (optionally) a new handler if the formula is novel.", {
    x: 0.6, y: 1.5, w: 12.1, h: 0.5,
    fontFace: BODY_FONT, fontSize: 12, italic: true, color: MUTED, margin: 0
  });

  card(s, 0.6, 2.05, 6.1, 5.0, "Payout shape",
    [
      { text: "Percentage", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "% of the source order (or matched earning).", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "Amount  ·  AmountPromo", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Flat USD; AmountPromo overrides during a promotion window.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "PaymentDelayDays", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "PaymentDate = EarnedDate + N days. Drives the payout sweep.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "IsRealTime  ·  IsPaidOnSignup  ·  IsPaidOnRenewal", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Which order types and which moments trigger this type.", options: { fontSize: 11, color: MUTED } }
    ], TEAL);

  card(s, 6.85, 2.05, 5.85, 5.0, "Eligibility levers",
    [
      { text: "LifeTimeRank  ·  CurrentRank", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Min lifetime / min current rank to qualify.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "PersonalPoints  ·  TeamPoints  ·  EnrollmentTeam", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Per-axis thresholds. 0 = opt-out for that axis.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "NewMembers  ·  DaysAfterJoining  ·  MembersRebill", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Counts within a sliding window (FSB-style).", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "ResidualBased  ·  ResidualOverCommissionType  ·  ResidualPercentage", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Mark a type as a residual / matching layer on another type.", options: { fontSize: 11, breakLine: true, color: MUTED } },
      { text: "ReverseId", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Points at the CommissionType used to write a cancelling row.", options: { fontSize: 11, color: MUTED } }
    ], VIOLET);
  footer(s, 6, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 7 — Sponsor Bonus + Fast Start Bonus (FSB)
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Sponsor Bonus & Fast Start Bonus — real-time payouts", "05 · Real-time");

  // Left: Sponsor Bonus card
  card(s, 0.6, 1.55, 5.0, 5.4, "Sponsor Bonus",
    [
      { text: "Trigger", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Real-time, on signup completion.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Beneficiary", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Direct sponsor in the enrollment tree.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Reversal", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "If the sponsoree cancels within the window → ReverseSponsorBonusHandler writes a Cancelled negative row. Original is not deleted.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Handler", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Features/CalculateSponsorBonus/CalculateSponsorBonusHandler.cs", options: { fontFace: "Consolas", fontSize: 10, color: MUTED } }
    ], VIOLET);

  // Right: FSB windows visual
  const fx = 5.8, fy = 1.55, fw = 6.95, fh = 5.4;
  s.addShape(pres.shapes.RECTANGLE, {
    x: fx, y: fy, w: fw, h: fh, fill: { color: WHITE }, line: { color: ICE, width: 1 },
    shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: fx, y: fy, w: 0.08, h: fh, fill: { color: ROSE }, line: { color: ROSE }
  });
  s.addText("Fast Start Bonus", {
    x: fx + 0.25, y: fy + 0.15, w: fw - 0.4, h: 0.4,
    fontFace: HEADER_FONT, fontSize: 16, bold: true, color: NAVY, margin: 0
  });
  s.addText("Three time-boxed windows over the member's tenure. Outside any window → no earning, no error.", {
    x: fx + 0.25, y: fy + 0.6, w: fw - 0.4, h: 0.5,
    fontFace: BODY_FONT, fontSize: 12, color: INK, valign: "top", margin: 0
  });

  // Timeline visualization
  const tlY = fy + 1.6;
  const tlX = fx + 0.4;
  const tlW = fw - 0.8;
  s.addShape(pres.shapes.LINE, {
    x: tlX, y: tlY + 0.3, w: tlW, h: 0,
    line: { color: ICE, width: 3 }
  });
  s.addText("Member enrollment", {
    x: tlX - 0.15, y: tlY - 0.05, w: 1.5, h: 0.3,
    fontFace: BODY_FONT, fontSize: 9, italic: true, color: MUTED, margin: 0
  });
  s.addText("Lifetime", {
    x: tlX + tlW - 0.6, y: tlY - 0.05, w: 0.7, h: 0.3,
    fontFace: BODY_FONT, fontSize: 9, italic: true, color: MUTED, align: "right", margin: 0
  });

  // 3 windows + extended
  const windows = [
    { name: "Window 1",          startPct: 0.05, widthPct: 0.18, color: ROSE,     y: 0.0 },
    { name: "W1 extended",       startPct: 0.18, widthPct: 0.08, color: AMBER,    y: 0.42 },
    { name: "Window 2",          startPct: 0.42, widthPct: 0.18, color: ROSE,     y: 0.0 },
    { name: "Window 3",          startPct: 0.72, widthPct: 0.18, color: ROSE,     y: 0.0 },
  ];
  windows.forEach(w => {
    const wx = tlX + tlW * w.startPct;
    const ww = tlW * w.widthPct;
    s.addShape(pres.shapes.RECTANGLE, {
      x: wx, y: tlY + 0.15 + w.y, w: ww, h: 0.3,
      fill: { color: w.color }, line: { color: w.color }
    });
    s.addText(w.name, {
      x: wx, y: tlY + 0.15 + w.y, w: ww, h: 0.3,
      fontFace: BODY_FONT, fontSize: 9, bold: true, color: WHITE,
      align: "center", valign: "middle", margin: 0
    });
  });

  // Logic block
  const lY = fy + 3.2;
  s.addText("Stored in MemberCommissionCountDown:", {
    x: fx + 0.25, y: lY, w: fw - 0.4, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: NAVY, margin: 0
  });
  s.addText([
    { text: "•  FastStartBonus1Start / 1End", options: { breakLine: true } },
    { text: "•  FastStartBonus1ExtendedStart / 1ExtendedEnd", options: { breakLine: true } },
    { text: "•  FastStartBonus2Start / 2End", options: { breakLine: true } },
    { text: "•  FastStartBonus3Start / 3End", options: { breakLine: true } },
    { text: " ", options: { breakLine: true } },
    { text: "History of changes in MemberCommissionCountDownHistory.", options: { italic: true, color: MUTED } }
  ], { x: fx + 0.25, y: lY + 0.4, w: fw - 0.4, h: 2.0, fontFace: BODY_FONT, fontSize: 11, color: INK, margin: 0 });

  footer(s, 7, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 8 — Daily Residual ledger
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Daily Residual — its own ledger, with snapshots", "06 · Daily Residual");

  s.addText("Daily Residual gets its own table for per-day audit + snapshot fields. Consolidated weekly (or ad-hoc when a recurring bill funds from commissions) into one CommissionEarning credit row.", {
    x: 0.6, y: 1.5, w: 12.1, h: 0.55,
    fontFace: BODY_FONT, fontSize: 12, italic: true, color: MUTED, margin: 0
  });

  // Left card — schema
  card(s, 0.6, 2.15, 6.0, 4.9, "DailyResidualEarning",
    [
      { text: "BeneficiaryMemberId  ·  Amount  ·  EarnedDate  ·  Status", options: { fontFace: "Consolas", fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Snapshot fields (set at accrual)", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "•  CurrentRankId", options: { fontSize: 11, breakLine: true } },
      { text: "•  EligibleDualTeamPoints", options: { fontSize: 11, breakLine: true } },
      { text: "•  EligibleEnrollmentTeamPoints", options: { fontSize: 11, breakLine: true } },
      { text: "•  PersonalPoints", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Payment-tracking fields (set on Pending → Paid)", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "•  PaymentDate  (from IDateTimeProvider)", options: { fontSize: 11, breakLine: true } },
      { text: '•  CommentedBy  ("weekly-consolidation" / "membership-token-purchase" / …)', options: { fontSize: 11, breakLine: true } },
      { text: "•  PaymentComment  (descriptive movement note)", options: { fontSize: 11, breakLine: true } },
      { text: "•  ConsolidatedIntoCommissionEarningId", options: { fontSize: 11 } }
    ], TEAL);

  // Right card — Monday job
  card(s, 6.75, 2.15, 6.0, 4.9, "Monday consolidation",
    [
      { text: "Job:  DailyResidualConsolidationJob", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Queue  commissions  ·  cron 0 4 * * 1  (Mondays 04:00 UTC)", options: { fontFace: "Consolas", fontSize: 10, color: MUTED, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "For each member:", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Sum pending DailyResidualEarning rows.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "If Σ ≥ DailyResidualConsolidationMinimum:", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "→ mark them Paid + tracking fields", options: { color: EMERALD, breakLine: true } },
      { text: "→ set ConsolidatedIntoCommissionEarningId", options: { color: EMERALD, breakLine: true } },
      { text: "→ create one CommissionEarning credit for the total", options: { color: EMERALD, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Below the minimum → leave pending, try next Monday.", options: { italic: true, color: MUTED, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Threshold default $100  ·  configurable in admin (GlobalParameter).", options: { fontSize: 11, color: MUTED } }
    ], EMERALD);

  footer(s, 8, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 9 — Boost · Car · Presidential
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Periodic bonuses — Boost · Car · Presidential", "07 · Periodic");

  function periodCard(x, y, w, h, name, cadence, eligibility, jobs, color) {
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w, h, fill: { color: WHITE }, line: { color: ICE, width: 1 },
      shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y, w, h: 0.55, fill: { color }, line: { color }
    });
    s.addText(name, {
      x: x + 0.2, y, w: w - 0.4, h: 0.55,
      fontFace: HEADER_FONT, fontSize: 17, bold: true, color: WHITE,
      valign: "middle", margin: 0
    });
    const rows = [
      ["Cadence", cadence],
      ["Eligibility", eligibility],
      ["Jobs", jobs]
    ];
    let cy = y + 0.75;
    rows.forEach(r => {
      s.addText(r[0].toUpperCase(), {
        x: x + 0.2, y: cy, w: w - 0.4, h: 0.32,
        fontFace: BODY_FONT, fontSize: 10, bold: true, color: MUTED,
        charSpacing: 2, margin: 0
      });
      s.addText(r[1], {
        x: x + 0.2, y: cy + 0.32, w: w - 0.4, h: 0.95,
        fontFace: BODY_FONT, fontSize: 11.5, color: INK, valign: "top", margin: 0
      });
      cy += 1.35;
    });
  }

  periodCard(0.6,  1.55, 4.0, 5.4, "Boost Bonus", "Weekly · Sundays 03:00 UTC + a 5-min sweep that backfills any missed past weeks",
    "Only NEW Elite (MembershipLevelId 3) and Turbo (4) signups in the upline's tree count toward thresholds — week runs Mon → Sun.",
    "BoostBonusJob · BoostBonusSweepJob", AMBER);

  periodCard(4.75, 1.55, 4.0, 5.4, "Car Bonus", "Monthly · 1st 05:00 UTC + daily 06:00 UTC reconciliation sweep",
    "Members meeting a rank + volume threshold receive a monthly stipend.",
    "CarBonusJob · CarBonusSweepJob", EMERALD);

  periodCard(8.9, 1.55, 3.9, 5.4, "Presidential Bonus", "Monthly · 1st 04:00 UTC",
    "Share of a company-defined pool, divided among members at or above the qualifying rank.",
    "PresidentialBonusJob", INDIGO);

  footer(s, 9, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 10 — Matching Bonus
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Matching Bonus — match your downline's earnings", "08 · Matching");

  s.addText("Event-triggered: fires when a downline earning row lands (FSB, Daily Residual consolidation, etc.). Pays a percentage match to the upline.", {
    x: 0.6, y: 1.55, w: 12.1, h: 0.5,
    fontFace: BODY_FONT, fontSize: 13, italic: true, color: MUTED, margin: 0
  });

  // Diagram: downline → upline match
  // Member A earns → A's sponsor B gets level-1 match → B's sponsor C gets level-2 match
  const dx = 1.5, dy = 2.6, nodeW = 2.3, nodeH = 1.1, gap = 1.2;

  const nodes = [
    { name: "Downline\nmember A", sub: "Earns FSB $100", color: VIOLET },
    { name: "Sponsor B", sub: "Level-1 match", color: ROSE },
    { name: "Sponsor C", sub: "Level-2 match", color: AMBER },
    { name: "Sponsor D", sub: "Level-3 match", color: EMERALD }
  ];

  nodes.forEach((n, i) => {
    const x = dx + i * (nodeW + gap);
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
      x, y: dy, w: nodeW, h: nodeH,
      fill: { color: n.color }, line: { color: n.color }, rectRadius: 0.08
    });
    s.addText(n.name, {
      x, y: dy + 0.1, w: nodeW, h: 0.5,
      fontFace: HEADER_FONT, fontSize: 13, bold: true, color: WHITE,
      align: "center", valign: "middle", margin: 0
    });
    s.addText(n.sub, {
      x, y: dy + 0.6, w: nodeW, h: 0.4,
      fontFace: BODY_FONT, fontSize: 10, color: WHITE,
      align: "center", valign: "middle", margin: 0
    });
    if (i < nodes.length - 1) {
      s.addShape(pres.shapes.RIGHT_ARROW, {
        x: x + nodeW + 0.1, y: dy + nodeH / 2 - 0.13, w: 0.9, h: 0.26,
        fill: { color: NAVY }, line: { color: NAVY }
      });
    }
  });

  // Configuration callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 4.4, w: 12.1, h: 1.85,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText("HOW IT'S CONFIGURED", {
    x: 0.9, y: 4.5, w: 11.7, h: 0.35,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: ICE, charSpacing: 3, margin: 0
  });
  s.addText([
    { text: "•  CommissionType.ResidualBased = true", options: { fontFace: "Consolas", color: WHITE, breakLine: true } },
    { text: "•  CommissionType.ResidualOverCommissionType = <id of the source type to match>", options: { fontFace: "Consolas", color: WHITE, breakLine: true } },
    { text: "•  CommissionType.ResidualPercentage = the match %", options: { fontFace: "Consolas", color: WHITE, breakLine: true } },
    { text: "•  CommissionType.LevelNo = which generation (1 = direct sponsor, 2 = grandparent, …)", options: { fontFace: "Consolas", color: WHITE } }
  ], { x: 0.9, y: 4.9, w: 11.7, h: 1.4, fontSize: 11, margin: 0 });

  // Eligibility note
  s.addText("Eligibility uses the same levers as any other commission type — LifeTimeRank, CurrentRank, PersonalPoints, SponsoredMembers thresholds — so an upline that hasn't earned the right to match a given level simply doesn't receive a row.", {
    x: 0.6, y: 6.45, w: 12.1, h: 0.8,
    fontFace: BODY_FONT, fontSize: 12, italic: true, color: MUTED, margin: 0
  });

  footer(s, 10, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 11 — CommissionEarning lifecycle
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Earnings lifecycle — Paid is terminal", "09 · Lifecycle");

  // Flow diagram across the slide
  const baseY = 2.2;
  const boxH = 1.0;
  const boxes = [
    { x: 0.6, w: 3.0, color: AMBER,   label: "Created · Pending",  sub: "EarnedDate = now\nPaymentDate = now + PaymentDelayDays" },
    { x: 4.2, w: 3.0, color: EMERALD, label: "Payout sweep → Paid", sub: "When PaymentDate ≤ now\nNo other transition allowed" },
    { x: 7.8, w: 5.0, color: DANGER,  label: "Reversal: NEW negative row", sub: "Status Cancelled · linked via SourceOrderId / SourceMemberId. Original never mutated." }
  ];
  boxes.forEach((b, i) => {
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
      x: b.x, y: baseY, w: b.w, h: boxH * 2,
      fill: { color: b.color }, line: { color: b.color }, rectRadius: 0.08
    });
    s.addText(b.label, {
      x: b.x + 0.15, y: baseY + 0.15, w: b.w - 0.3, h: 0.6,
      fontFace: HEADER_FONT, fontSize: 16, bold: true, color: WHITE,
      align: "center", valign: "middle", margin: 0
    });
    s.addText(b.sub, {
      x: b.x + 0.15, y: baseY + 0.85, w: b.w - 0.3, h: 1.0,
      fontFace: BODY_FONT, fontSize: 12, color: WHITE,
      align: "center", valign: "middle", margin: 0
    });
    if (i < 2) {
      s.addShape(pres.shapes.RIGHT_ARROW, {
        x: b.x + b.w + 0.05, y: baseY + boxH - 0.15, w: 0.5, h: 0.3,
        fill: { color: NAVY }, line: { color: NAVY }
      });
    }
  });

  // Token-funded membership row
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 5.0, w: 12.1, h: 1.0,
    fill: { color: WHITE }, line: { color: VIOLET, width: 2 }, rectRadius: 0.08
  });
  s.addText("Token purchase from commission balance", {
    x: 0.8, y: 5.05, w: 11.7, h: 0.35,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: VIOLET, charSpacing: 2, margin: 0
  });
  s.addText("A new CommissionEarning row with Amount = -fee, Status = Pending. Notes / SourceOrderId point at the renewal order. This is the debit ledger entry (see BILLING-RULES §7).", {
    x: 0.8, y: 5.45, w: 11.7, h: 0.55,
    fontFace: BODY_FONT, fontSize: 12, color: INK, valign: "middle", margin: 0
  });

  // Dedup callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.6, y: 6.2, w: 12.1, h: 1.0,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText("Dedup contract", {
    x: 0.8, y: 6.25, w: 11.7, h: 0.35,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: ICE, charSpacing: 2, margin: 0
  });
  s.addText("Unique constraint on (SourceOrderId, CommissionTypeId) — code RELIES on this instead of \"remembering\" it processed this order. CSV-imported rows carry CsvImportBatchId for bulk reversal.", {
    x: 0.8, y: 6.6, w: 11.7, h: 0.55,
    fontFace: BODY_FONT, fontSize: 12, color: WHITE, valign: "middle", margin: 0
  });

  footer(s, 11, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 12 — Ranks & qualification
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Ranks — qualification, lifetime, evaluation", "10 · Ranks");

  // 3 columns: definitions, requirements, evaluation
  card(s, 0.6, 1.55, 4.0, 5.4, "Definitions",
    [
      { text: "RankDefinition", options: { fontFace: "Consolas", bold: true, color: NAVY, breakLine: true } },
      { text: "•  Name", options: { fontSize: 11, breakLine: true } },
      { text: "•  SortOrder  (higher = senior)", options: { fontSize: 11, breakLine: true } },
      { text: "•  Status  (Active / Retired)", options: { fontSize: 11, breakLine: true } },
      { text: "•  CertificateTemplateUrl", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "19 progression rungs", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Silver → Black Royal  (Id 1–19, SortOrder 1–19). Id 20 = Lifestyle Consultant, the default starting rank at SortOrder 0.", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "RankRequirement", options: { fontFace: "Consolas", bold: true, color: NAVY, breakLine: true } },
      { text: "Multiple per rank (LevelNo 0, 1, 2 …) — for multi-tier ranks with stepped requirements.", options: { fontSize: 11 } }
    ], TEAL);

  card(s, 4.75, 1.55, 4.0, 5.4, "Capping math",
    [
      { text: "Per-leg cap (DT)", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "perLegCap = MaxTeamPointsPerBranch × TeamPoints", options: { fontFace: "Consolas", fontSize: 10, breakLine: true } },
      { text: "(default 0.5 — each leg ≤ 50 %)", options: { fontSize: 11, italic: true, color: MUTED, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Per-branch cap (ET)", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "perBranchCap = MaxEnrollmentTeamPointsPerBranch × EnrollmentTeam", options: { fontFace: "Consolas", fontSize: 10, breakLine: true } },
      { text: "(default 0.5)", options: { fontSize: 11, italic: true, color: MUTED, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Threshold = 0", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "means that axis opts OUT for this rank.", options: { fontSize: 11, italic: true, color: MUTED } }
    ], VIOLET);

  card(s, 8.9, 1.55, 3.9, 5.4, "Evaluation",
    [
      { text: "Nightly sweep", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "RankEngine · 03:30 UTC. Evaluates members whose stats changed.", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "On-demand", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "POST /api/v1/ranks/evaluate/{memberId}", options: { fontFace: "Consolas", fontSize: 10, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Lifetime rank", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Highest SortOrder ever achieved. Stays at lifetime even if current rank drops.", options: { fontSize: 11, breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Queue", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "RankEvaluationQueue lets us avoid full-table scans every night.", options: { fontSize: 11 } }
    ], EMERALD);

  footer(s, 12, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 13 — Loyalty + job catalog (combined)
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Loyalty points & job catalog", "11 · Loyalty + 12 · Jobs");

  // Loyalty card on the left
  card(s, 0.6, 1.55, 5.5, 5.5, "Loyalty Points",
    [
      { text: "Locked / Unlocked model", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Locked points cannot be spent. Unlocks after N consecutive successful payments (per-product setting in ProductLoyaltyPointsSetting).", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "MissedPayment flag", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Recurring billing failures flip this true. Once true, future unlocks on this row are blocked.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Monthly bucketing", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "MonthNo + YearNo for reports.", options: { breakLine: true } },
      { text: " ", options: { breakLine: true } },
      { text: "Why this design?", options: { bold: true, color: NAVY, breakLine: true } },
      { text: "Loyalty pays for stable subscribers. The billing engine signals success/failure into this table — Stopped / HoldByBilling / Expired transitions are what flip MissedPayment.", options: { italic: true, color: MUTED } }
    ], ROSE);

  // Job catalog on the right
  const jx = 6.3, jy = 1.55, jw = 6.45, jh = 5.5;
  s.addShape(pres.shapes.RECTANGLE, {
    x: jx, y: jy, w: jw, h: jh, fill: { color: WHITE }, line: { color: ICE, width: 1 },
    shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
  });
  s.addShape(pres.shapes.RECTANGLE, {
    x: jx, y: jy, w: 0.08, h: jh, fill: { color: NAVY }, line: { color: NAVY }
  });
  s.addText("Hangfire jobs · queue commissions", {
    x: jx + 0.25, y: jy + 0.15, w: jw - 0.4, h: 0.4,
    fontFace: HEADER_FONT, fontSize: 16, bold: true, color: NAVY, margin: 0
  });

  const jobs = [
    ["DailyResidualJob",                "0 2 * * *",     "Daily 02:00 UTC"],
    ["BoostBonusJob",                   "0 3 * * 0",     "Sundays 03:00 UTC"],
    ["BoostBonusSweepJob",              "*/5 * * * *",   "Every 5 min · backfill"],
    ["CarBonusJob",                     "0 5 1 * *",     "1st of month 05:00 UTC"],
    ["CarBonusSweepJob",                "0 6 * * *",     "Daily 06:00 UTC · reconciliation"],
    ["PresidentialBonusJob",            "0 4 1 * *",     "1st of month 04:00 UTC"],
    ["DailyResidualConsolidationJob",   "0 4 * * 1",     "Mondays 04:00 UTC"]
  ];

  const jhead = ["Job", "Cron", "When"];
  const jdata = [
    jhead.map(c => ({ text: c, options: { fill: { color: NAVY }, color: WHITE, bold: true, fontFace: BODY_FONT, fontSize: 10, valign: "middle" } })),
    ...jobs.map(r => r.map((c, idx) => ({
      text: c,
      options: {
        color: idx === 0 ? NAVY : INK,
        bold: idx === 0,
        fontFace: idx === 1 ? "Consolas" : BODY_FONT,
        fontSize: 10, valign: "middle",
        fill: { color: WHITE }
      }
    })))
  ];
  s.addTable(jdata, {
    x: jx + 0.25, y: jy + 0.7, w: jw - 0.5,
    colW: [2.6, 1.5, 2.05],
    border: { type: "solid", pt: 0.5, color: "E2E8F0" },
    rowH: 0.34
  });

  s.addText("Real-time (event-driven): Sponsor Bonus · Fast Start Bonus · Matching Bonus. These fire from the order/signup handler via MediatR — never scheduled.", {
    x: jx + 0.25, y: jy + jh - 0.8, w: jw - 0.5, h: 0.7,
    fontFace: BODY_FONT, fontSize: 11, italic: true, color: MUTED, valign: "top", margin: 0
  });

  footer(s, 13, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 14 — Commissions ↔ Billing integration
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: SUBTLE_BG };
  headerBar(s, "Commissions ↔ Billing — four integration seams", "13 · Integration");

  const seams = [
    { num: "1", title: "Billing triggers commissions",  body: "A successful recurring charge cascades into: FSB on the order (real-time), Boost Bonus eligibility (weekly), Matching Bonus (event), upline DT/ET contribution → rank evaluation.", color: TEAL },
    { num: "2", title: "Commissions fund bills",        body: "CommissionBalanceService reads pending CommissionEarning + pending DailyResidualEarning (with consolidation threshold) → consolidates → writes negative debit → issues product token.", color: VIOLET },
    { num: "3", title: "State changes reverse points",  body: "A subscription going Active → Expired / HoldByBilling retracts the contribution to upline. In the planned high-volume pipeline, this becomes a negative PointDeltaEvent.", color: AMBER },
    { num: "4", title: "Loyalty sees billing health",   body: "MissedPayment flips true when the recurring engine fails to bill. Future unlocks on the affected loyalty-points rows are blocked.", color: ROSE }
  ];

  const cw = 2.95, ch = 4.5, sy = 1.6, sx0 = 0.5;
  seams.forEach((s2, i) => {
    const x = sx0 + i * (cw + 0.15);
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: cw, h: ch, fill: { color: WHITE }, line: { color: ICE, width: 1 },
      shadow: { type: "outer", blur: 8, offset: 2, angle: 90, color: "000000", opacity: 0.06 }
    });
    s.addShape(pres.shapes.RECTANGLE, {
      x, y: sy, w: cw, h: 0.55, fill: { color: s2.color }, line: { color: s2.color }
    });
    s.addText(`SEAM ${s2.num}`, {
      x: x + 0.18, y: sy, w: cw - 0.36, h: 0.55,
      fontFace: BODY_FONT, fontSize: 11, bold: true, color: WHITE,
      valign: "middle", margin: 0, charSpacing: 3
    });
    s.addText(s2.title, {
      x: x + 0.18, y: sy + 0.65, w: cw - 0.36, h: 0.6,
      fontFace: HEADER_FONT, fontSize: 15, bold: true, color: NAVY, margin: 0
    });
    s.addText(s2.body, {
      x: x + 0.18, y: sy + 1.35, w: cw - 0.36, h: ch - 1.5,
      fontFace: BODY_FONT, fontSize: 11.5, color: INK, valign: "top", margin: 0,
      paraSpaceAfter: 3
    });
  });

  // bottom callout
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, {
    x: 0.5, y: 6.4, w: 12.3, h: 0.95,
    fill: { color: NAVY }, line: { color: NAVY }, rectRadius: 0.08
  });
  s.addText("THE PLANNED SEAM (BILLING-RULES §10)", {
    x: 0.7, y: 6.45, w: 11.9, h: 0.35,
    fontFace: BODY_FONT, fontSize: 11, bold: true, color: ICE, charSpacing: 3, margin: 0
  });
  s.addText("The upline aggregator collapses thousands of per-charge events into one batched UPDATE per upline — eliminating hot-key contention on top-of-tree members and ending state-oscillation writes. Rank evaluation queue gets enqueued only for members whose stats actually moved.", {
    x: 0.7, y: 6.78, w: 11.9, h: 0.55,
    fontFace: BODY_FONT, fontSize: 11, italic: true, color: WHITE, valign: "top", margin: 0
  });

  footer(s, 14, TOTAL);
}

// ─────────────────────────────────────────────────────────────────────
// Slide 15 — Ten hard rules
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addText("TEN HARD RULES", {
    x: 0.6, y: 0.45, w: 12, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: VIOLET, charSpacing: 4, margin: 0
  });
  s.addText("Never violate these.", {
    x: 0.6, y: 0.85, w: 12, h: 0.7,
    fontFace: HEADER_FONT, fontSize: 32, bold: true, color: WHITE, margin: 0
  });

  const rules = [
    "CommissionEarning.Status = Paid is terminal. Reversals are NEW negative rows; never mutate Paid.",
    "Daily Residuals live in their own table. New accruals go to DailyResidualEarning, never directly to CommissionEarning.",
    "Consolidation threshold is configurable (GlobalParameter \"DailyResidualConsolidationMinimum\", default $100). Never hardcode.",
    "Per-leg / per-branch caps live on CommissionType and RankRequirement. Bypassing them produces unqualified ranks.",
    "No commission calc from controllers. Real-time types via MediatR from order/signup handlers; batch types from Hangfire jobs.",
    "Ghost Points visibility is scoped. Reports outside the affected member's upline must never surface them.",
    "Dedup by (SourceOrderId, CommissionTypeId). The unique constraint exists — rely on it, don't \"remember\".",
    "Consolidated daily-residual rows are never deleted. Going Paid means tracking fields + ConsolidatedIntoCommissionEarningId set.",
    "Time always comes from IDateTimeProvider. Never DateTime.Now / DateTime.UtcNow in handlers or jobs.",
    "Snapshot, don't recompute. New accrual tables capture the values that explain the row (rank, eligible points, PP)."
  ];

  const cols = 2, rows = 5;
  const cellW = 6.0, cellH = 0.95;
  const x0 = 0.6, y0 = 1.9;
  rules.forEach((r, i) => {
    const col = i % cols, row = Math.floor(i / cols);
    const x = x0 + col * (cellW + 0.3);
    const y = y0 + row * (cellH + 0.1);
    s.addShape(pres.shapes.OVAL, {
      x, y: y + 0.1, w: 0.55, h: 0.55,
      fill: { color: VIOLET }, line: { color: VIOLET }
    });
    s.addText(`${i + 1}`, {
      x, y: y + 0.1, w: 0.55, h: 0.55,
      fontFace: HEADER_FONT, fontSize: 16, bold: true, color: WHITE,
      align: "center", valign: "middle", margin: 0
    });
    s.addText(r, {
      x: x + 0.7, y, w: cellW - 0.7, h: cellH,
      fontFace: BODY_FONT, fontSize: 11.5, color: WHITE, valign: "middle", margin: 0
    });
  });

  s.addText("MLMConqueror — Commissions Reference · v1.0 · 2026-05-13",
    { x: 0.5, y: H - 0.34, w: 12, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, valign: "middle", margin: 0 });
  s.addText(`15 / ${TOTAL}`,
    { x: W - 1.5, y: H - 0.34, w: 1.0, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, align: "right", valign: "middle", margin: 0 });
}

// ─────────────────────────────────────────────────────────────────────
// Slide 16 — Closing / code locations
// ─────────────────────────────────────────────────────────────────────
{
  const s = pres.addSlide();
  s.background = { color: NAVY };
  s.addShape(pres.shapes.RECTANGLE, {
    x: 1, y: 1.2, w: 1.2, h: 0.04, fill: { color: VIOLET }, line: { color: VIOLET }
  });
  s.addText("WHERE THINGS LIVE", {
    x: 1, y: 0.6, w: 11, h: 0.4,
    fontFace: BODY_FONT, fontSize: 12, bold: true, color: VIOLET, charSpacing: 4, margin: 0
  });
  s.addText("Code locations & references", {
    x: 1, y: 1.4, w: 11, h: 0.8,
    fontFace: HEADER_FONT, fontSize: 32, bold: true, color: WHITE, margin: 0
  });

  const refs = [
    ["Commission entities",          "Domain/Entities/Commission/"],
    ["Rank entities",                "Domain/Entities/Rank/"],
    ["Loyalty entities",             "Domain/Entities/Loyalty/"],
    ["Calculation handlers",         "CommissionEngine/Features/Calculate*/"],
    ["Reversal handlers",            "CommissionEngine/Features/Reverse*/"],
    ["Hangfire jobs",                "CommissionEngine/Jobs/"],
    ["Rank computation",             "Repository/Services/Ranks/RankComputationService.cs"],
    ["Rank evaluation + certificate","RankEngine/"],
    ["Stat snapshot job",            "Repository/Jobs/MemberStatisticSnapshotJob.cs"],
    ["Admin commissions API",        "AdminAPI/Controllers/AdminCommissions*.cs"],
    ["Admin commissions UI",         "AdminWeb/Components/Pages/AdminCommissions*.razor"],
    ["Companion docs",               "docs/billing/BILLING-RULES.md · docs/superpowers/specs/"]
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

  s.addText("MLMConqueror — Commissions Reference · v1.0 · 2026-05-13",
    { x: 0.5, y: H - 0.34, w: 12, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, valign: "middle", margin: 0 });
  s.addText(`${TOTAL} / ${TOTAL}`,
    { x: W - 1.5, y: H - 0.34, w: 1.0, h: 0.32, fontFace: BODY_FONT, fontSize: 9, color: ICE, align: "right", valign: "middle", margin: 0 });
}

// Write file
pres.writeFile({ fileName: path.join(__dirname, "Commissions-Workflow.pptx") })
    .then(p => console.log("Wrote:", p));
