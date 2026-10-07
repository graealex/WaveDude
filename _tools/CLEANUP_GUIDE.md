# Decompiled-code cleanup guide (phase 2)

The C# in `Wave_Dude_2D/` is ILSpy output from a VB.NET binary. Your job is to make the
files assigned to you read like hand-written C# **without changing behaviour**. The project
compiles right now; it must still compile when you are done.

## Hard rules

1. **Behaviour-preserving only.** No feature changes, no "improvements" to logic, no reordering
   of side effects. If you are unsure whether a rewrite is equivalent, leave the original.
2. **Do not change the signature, name, accessibility or type of any member** (method, property,
   field, event, constant, nested type). Other agents are editing other files in parallel and
   call into yours. This includes `ref` parameters — leave them `ref` for now.
3. **Only edit the files assigned to you.** Never touch `*.Designer.cs`, `My/*`, `*.resx`,
   `*.csproj`, or any other file.
4. Keep the file format: **tab indentation, Allman braces**,
   `this.` prefixes as they are. Do not reflow or reformat code you are not otherwise changing.
5. Keep `using` directives that are still needed; drop ones that become unused
   (`Microsoft.VisualBasic.CompilerServices`, `System.Runtime.CompilerServices` usually do).

## What to fix

### Names
Decompiler placeholders → descriptive names based on what the value *is*:
`num`, `num2`, … `flag`, `flag3`, … `text`, `text2`, … `array`, `list`, `list2`, `obj`,
`item`, `ex2`, `dictionary`, `bitmap2`, `graphics`, `stringBuilder`, `result`, `enumerator`…
- A loop bound `int num = x.Length - 1; for (int i = 0; i <= num; i++)` becomes
  `for (int i = 0; i < x.Length; i++)` (drop the temp).
- A hoisted `bool flag = <cond>;` used once in an `if` is inlined (see control flow).
- Unused catch variables: `catch (Exception ex) { }` → `catch (Exception) { }`
  (or `catch { }` when the type is `Exception`). Keep the variable if it is used.
- Keep names already meaningful (`stride`, `width`, `probe`, `scheme`...).
- Naming style: camelCase locals, no Hungarian, no `_` prefix for locals.

### Control flow
- `bool flag = cond; if (flag) {…}` → `if (cond) {…}`; `if (!flag)` → `if (!cond)`.
- `if (!cond) { <whole rest of method> }` → `if (cond) return;` + flat body (guard clauses),
  when it removes a nesting level and the method has no other tail code.
- `T result; if (c) { result = a; } else { result = b; } return result;` →
  `return c ? a : b;` for simple expressions, otherwise two `return`s.
- `do { … num++; } while (num <= 255);` counting loops → `for`.
- `for (;;) { … if (x) break; … }` → `while (!x)` when it reads naturally; otherwise leave.
- `foreach (object obj in coll) { T t = (T)obj; … }` → `foreach (T t in coll) { … }`.
- `checked { … }` blocks and `checked(...)`/`unchecked(...)` expression wrappers: remove.
  Keep `unchecked` only where it is needed to compile a constant/cast
  (e.g. `unchecked((int)0xFF000000)`).
- `RuntimeHelpers.GetObjectValue(x)` → `x`.
- VB runtime helpers → C#, preserving semantics:
  - `Conversions.ToString(x)` → `x.ToString()` (for numbers use the same culture the original
    would have used: VB uses the current culture → `x.ToString(CultureInfo.CurrentCulture)`,
    or keep `CultureInfo.InvariantCulture` when the surrounding code already uses it).
  - `Conversions.ToInteger(s)` / `ToDouble(s)` → `int.Parse` / `double.Parse` with
    `CultureInfo.CurrentCulture`; if the original was wrapped in try/catch keep it.
  - `Conversions.ToBoolean`, `ToInteger(bool)` etc. → plain C# casts/expressions.
  - `Operators.CompareString(a, b, false) == 0` → `string.CompareOrdinal(a, b) == 0`
    (or `a == b` when neither side can be null-vs-empty sensitive — if unsure use `CompareOrdinal`).
  - `Strings.Len(s)` → `s.Length`; `Strings.Trim` → `.Trim()`; `Interaction.MsgBox` → `MessageBox.Show`.
- Redundant casts the decompiler added (`(int)c.R`, `(double)intValue * 1.0`, `(long)` widening
  in index arithmetic) → remove where the result type is unchanged.
- Empty `try { } finally { }` leftovers, dead `if (true)`, duplicated null checks → remove.

### Comments / attributes
- Delete every `// Token: …`, `// (get) Token…`, `// (set) Token…`, `// RVA…` comment.
- Delete decompiler attributes `[CompilerGenerated]`, `[DebuggerStepThrough]`,
  `[MethodImpl(MethodImplOptions.Synchronized)]`, `[DesignerGenerated]`, `[GeneratedCode(...)]`
  when they sit on ordinary members. Keep `[DllImport]`, `[StructLayout]`, `[STAThread]`,
  `[DesignerCategory]`, `[Serializable]` and anything with runtime meaning.
- You may add a short `///` summary on a class and on methods whose purpose is not obvious from
  the name. Keep it sparse — the surrounding code has few comments; do not comment every line.

## Verify before you finish

Run the standalone compiler check (does not touch `bin/obj`, safe in parallel):

```
cd /c/Work/WaveDude/Wave_Dude_2D && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo -target:winexe -out:"$(cygpath -w /tmp/check_<yourname>.exe)" -langversion:7.3 -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:Microsoft.VisualBasic.dll -r:System.Web.Extensions.dll -nowarn:0168,0649,0414,0675 *.cs My/*.cs My/Resources/*.cs Properties/*.cs
```

- Errors in **your** files: fix them.
- Errors in other files: another agent is mid-edit; wait ~30 s and re-run. Ignore if they persist
  and your files are clean (report it).


Finish with a 5-line report: files touched, approximate number of renames, anything you left
alone because you were unsure, and the csc result.
