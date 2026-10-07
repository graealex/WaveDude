# Documentation + final cleanup guide (phase 3)

The code in `Wave_Dude_2D/` has been cleaned from decompiler output into readable C#. This pass
adds proper XML documentation and finishes any cleanup still left. All the **hard rules** of
`CLEANUP_GUIDE.md` still apply (behaviour-preserving, no signature/name/accessibility changes,
only your assigned files, tabs + Allman, verify with `csc`). Read that file first. Do not touch, check or mention file encodings or line endings; `.editorconfig` handles that.

## XML documentation — what is required

Every **type** (class, struct, interface, enum, delegate, nested type) gets a `/// <summary>` that
says what it is *for* and how it fits in (who creates it, who consumes it, which thread it lives
on if that matters).

Every **member** gets a `/// <summary>`:
- public/internal methods, properties, fields, events, constants: always, with `<param>` for every
  parameter whose meaning is not obvious from its name and type, `<returns>` when the return value
  needs explanation (what null/empty/-1 means, units, ranges), `<exception>` when the method throws
  deliberately.
- enum members: always, one line each.
- private fields: one line each (what it holds, units, when it is null, which thread touches it).
- private methods: a summary; `<param>`/`<returns>` only where they add information.
- WinForms event handlers (`btnX_Click`, `numY_ValueChanged`, …): one line saying what the user
  action results in — not "handles the click event".
- Constructors: only if they do more than assign parameters.

Quality bar:
- Write from the *code*, not from the name. Read the body before writing the summary. If the code
  does something surprising (side effects, re-entrancy guards like `_loadingScene`, swapping buffers,
  clamping, culture-specific parsing, COM late binding), say so.
- State units and coordinate systems: canvas pixels vs. picture-box pixels vs. screen; steps vs.
  seconds; refractive index vs. speed; 0..1 vs 0..255; radians vs degrees.
- State threading: anything called from `SimulationRunner`'s worker thread, anything that must
  run on the UI thread, anything marshalled through `PostToUi`.
- State ownership/disposal for `Bitmap`, `Graphics`, OpenCL and Media Foundation handles.
- Do not pad. "Gets or sets the width." is fine for a trivial property. Do not write a paragraph
  for a one-line getter. Do not restate parameter names as prose.
- Keep summaries that already exist if they are accurate; sharpen them if not.
- Use `<see cref="..."/>` for references to other members/types; `<paramref name="..."/>` for
  parameters. Standard XML doc syntax only (`<summary>`, `<param>`, `<returns>`, `<remarks>`,
  `<exception>`, `<see>`, `<paramref>`, `<c>`, `<list>`). Escape `<`, `>` and `&` in prose as
  `&lt;`, `&gt;`, `&amp;`.
- Inline `//` comments: add them where a non-obvious line needs one (magic numbers, why a
  workaround exists); do not comment every line.

## Final cleanup — whatever is still left

While you are reading each method anyway, finish the cleanup from `CLEANUP_GUIDE.md` where the
previous pass missed something: leftover placeholder names, `bool x = cond; if (x)` temps used
once, redundant casts, `string.Concat(new string[] {...})` → `+`, dead code, duplicated
null-checks, empty handlers that are never wired, unused `using`s, `catch (Exception ex)` with
unused `ex`. Still no signature or behaviour changes.

Also fix obvious typos in **identifiers you own privately** is NOT allowed (names are frozen);
typos in **comments** and in **existing string literals** are also off-limits (strings are UI
behaviour). Just document.

## Verify

Run the csc check from `CLEANUP_GUIDE.md` (use your own check exe name), **adding `-doc:"$(cygpath -w /tmp/check_<yourname>.xml)" -warnaserror+:1570,1572,1573,1574,1580,1584,1587,1589,1590,1591,1710`** so malformed
XML or wrong `<param>` names fail the build. Fix every such error in your files.

Finish with a 5-line report: files touched, number of members documented, anything you could not
understand well enough to document confidently (list them — a wrong comment is worse than none),
cleanup done beyond docs, csc result.
