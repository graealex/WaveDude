"""Second-pass fixes for decompiled-VB residue that is syntactically valid but does not compile.

Order matters: step 1 uses line numbers from the current build log (/tmp/build.log),
so it runs before anything that shifts lines.

 1. CS0165 ByRef locals  : `double num;` passed as `ref num` -> `double num = default(double);`
 2. bogus try/finally    : `finally { List<T>.Enumerator enumerator; ((IDisposable)enumerator).Dispose(); }`
                           -> drop the finally, unwrap the try
 3. VB event backing refs: `this.FooEvent` -> `this.Foo`
 4. AviMjpegWriter       : two mis-decompiled unsigned constants
 5. WithEvents properties: `internal virtual T name { get{return this._name;} set{...hook events...} }`
                           -> `private T name;` in the Designer + `this.name.Event += handler;` in InitializeComponent
"""
import re, sys, os

SRC = 'Wave_Dude_2D'

def read(path):
    raw = open(path, 'rb').read()
    return raw.startswith(b'\xef\xbb\xbf'), raw.decode('utf-8-sig').split('\r\n')

def write(path, bom, lines):
    open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + '\r\n'.join(lines).encode('utf-8'))

def indent_of(line):
    return len(line) - len(line.lstrip('\t'))

# ---------------------------------------------------------------- 1. ByRef locals
def fix_byref_locals(log_path):
    sites = {}
    for m in re.finditer(r'\\([A-Za-z0-9_.]+\.cs)\((\d+),\d+\): error CS0165: Use of unassigned local variable \'(\w+)\'', open(log_path, encoding='utf-8', errors='replace').read()):
        f, line, var = m.group(1), int(m.group(2)), m.group(3)
        if var.startswith('enumerator'):
            continue
        sites.setdefault(f, set()).add((line, var))
    for f, items in sites.items():
        path = os.path.join(SRC, f)
        bom, lines = read(path)
        for line, var in sorted(items):
            i = line - 1
            decl = re.compile(r'^(\t+)([A-Za-z0-9_<>\[\].]+) ' + re.escape(var) + r';$')
            while i >= 0 and not decl.match(lines[i]):
                i -= 1
            if i < 0:
                sys.exit('decl of %s not found near %s:%d' % (var, f, line))
            m = decl.match(lines[i])
            lines[i] = '%s%s %s = default(%s);' % (m.group(1), m.group(2), var, m.group(2))
        write(path, bom, lines)
        print('byref locals: %s (%d)' % (f, len(items)))

# ---------------------------------------------------------------- 2. bogus try/finally
BOGUS = [re.compile(p) for p in (
    r'^\t+[A-Za-z0-9_<>.\[\]]*Enumerator enumerator\d*;$',
    r'^\t+\(\(IDisposable\)enumerator\d*\)\.Dispose\(\);$',
    r'^\t+if \(enumerator\d* is IDisposable\)$',
    r'^\t+\(enumerator\d* as IDisposable\)\.Dispose\(\);$',
    r'^\t+[{}]$',
)]

def fix_bogus_finally(path):
    bom, lines = read(path)
    changed = 0
    i = 0
    while i < len(lines):
        if lines[i].lstrip('\t') == 'finally' and lines[i].startswith('\t'):
            k = indent_of(lines[i])
            close = i + 2
            while close < len(lines) and lines[close] != '\t' * k + '}':
                close += 1
            body = lines[i + 2:close]
            if body and all(any(p.match(l) for p in BOGUS) for l in body) and any('numerator' in l for l in body):
                # block directly before `finally` is `{ ... }` at indent k; find its opening brace
                j = i - 1
                assert lines[j] == '\t' * k + '}', (path, i)
                depth = 0
                while True:
                    s = lines[j].strip()
                    if s == '}':
                        depth += 1
                    elif s == '{':
                        depth -= 1
                        if depth == 0:
                            break
                    j -= 1
                head = lines[j - 1].strip()
                del lines[i:close + 1]                      # drop finally block
                if head == 'try':
                    inner = [l[1:] if l.startswith('\t') else l for l in lines[j + 1:i - 1]]
                    lines[j - 1:i] = inner                  # unwrap try { ... }
                    i = j - 1 + len(inner)
                else:
                    # try/catch/finally: keep try/catch, finally alone is dropped
                    i = i
                changed += 1
                continue
        i += 1
    if changed:
        write(path, bom, lines)
        print('bogus finally: %s (%d)' % (path, changed))

# ---------------------------------------------------------------- 3. event backing refs
def fix_event_refs(path):
    bom, lines = read(path)
    new = [re.sub(r'this\.(ColorChanged|EngineReady|ProgressChanged|PreviewReady|ProbesUpdated|Finished)Event\b', r'this.\1', l) for l in lines]
    if new != lines:
        write(path, bom, new)
        print('event refs:', path)

# ---------------------------------------------------------------- 4. AVI constants
def fix_avi():
    path = os.path.join(SRC, 'AviMjpegWriter.cs')
    bom, lines = read(path)
    text = '\r\n'.join(lines)
    a = text.replace('this._writer.Write((uint)((unchecked((long)width) * unchecked((long)height) * 3L) & (long)(unchecked((ulong)(-1)))));',
                     'this._writer.Write((uint)(((long)width * height * 3L) & 0xFFFFFFFFL));')
    b = a.replace('+ 64L > (long)(unchecked((ulong)(-394967296)))', '+ 64L > MaxAviBytes')
    assert a != text and b != a, 'AVI constants not found'
    b = b.replace('\t\tpublic void AddFrame(byte[] data, int length)',
                  '\t\t// stay safely below the 4 GB RIFF limit\r\n\t\tprivate const long MaxAviBytes = 3900000000L;\r\n\r\n\t\tpublic void AddFrame(byte[] data, int length)', 1)
    write(path, bom, b.split('\r\n'))
    print('avi constants fixed')

# ---------------------------------------------------------------- 5. WithEvents properties
PROP = re.compile(r'^\t\tinternal virtual ([A-Za-z0-9_<>.:]+) ([A-Za-z0-9_]+)$')
HANDLER = re.compile(r'^\t{4}([A-Za-z0-9_.]+) ([A-Za-z0-9_]+) = new ([A-Za-z0-9_.]+)\(this\.([A-Za-z0-9_]+)\);$')
HOOK = re.compile(r'^\t{5}([A-Za-z0-9_]+)\.([A-Za-z0-9_]+) \+= ([A-Za-z0-9_]+);$')

def fix_withevents(code_path, designer_path):
    bom, lines = read(code_path)
    fields = []      # (type, name)
    wiring = []      # (control, event, delegateType, handlerMethod)
    i = 0
    out = []
    while i < len(lines):
        m = PROP.match(lines[i])
        if not m:
            out.append(lines[i]); i += 1; continue
        typ, name = m.group(1), m.group(2)
        # strip preceding decompiler comments already emitted to out
        while out and out[-1].strip().startswith('// '):
            out.pop()
        end = i + 1
        while lines[end] != '\t\t}':
            end += 1
        block = lines[i:end + 1]
        delegates = {}
        for l in block:
            h = HANDLER.match(l)
            if h:
                delegates[h.group(2)] = (h.group(3), h.group(4))
            k = HOOK.match(l)
            if k:
                dt, method = delegates[k.group(3)]
                wiring.append((name, k.group(2), dt, method))
        fields.append((typ, name))
        i = end + 1
        # drop one trailing blank line
        if i < len(lines) and lines[i] == '' and out and out[-1] == '':
            i += 1
    # sanity: no stray references to the backing fields remain
    leftover = [n for _, n in fields if re.search(r'\b_' + re.escape(n) + r'\b', '\r\n'.join(out))]
    assert not leftover, ('backing field refs remain', code_path, leftover[:5])
    write(code_path, bom, out)

    dbom, dlines = read(designer_path)
    comp = next(j for j, l in enumerate(dlines) if l.strip().startswith('private global::System.ComponentModel.IContainer components;'))
    # drop the Token comment above `components`
    if dlines[comp - 1].strip().startswith('// Token'):
        del dlines[comp - 1]; comp -= 1
    decls = ['\t\tprivate %s %s;' % (t.replace('global::', ''), n) for t, n in fields]
    dlines[comp:comp] = decls
    if wiring:
        created = set(re.findall(r'this\.([A-Za-z0-9_]+) = new ', '\r\n'.join(dlines)))
        missing = sorted({c for c, _, _, _ in wiring if c not in created})
        assert not missing, ('wired controls not created in InitializeComponent', designer_path, missing)
        anchor = next(j for j, l in enumerate(dlines) if re.match(r'^\t\t\t(base|this)\.(AutoScaleDimensions|ResumeLayout)', l))
        hooks = ['\t\t\tthis.%s.%s += new %s(this.%s);' % w for w in wiring]
        dlines[anchor:anchor] = ['\t\t\t// event wiring (was VB WithEvents/Handles)'] + hooks
    write(designer_path, dbom, dlines)
    print('withevents: %s -> %d fields, %d event hooks' % (code_path, len(fields), len(wiring)))

if __name__ == '__main__':
    fix_byref_locals('/tmp/build.log' if os.path.exists('/tmp/build.log') else sys.argv[1])
    for f in sorted(os.listdir(SRC)):
        if f.endswith('.cs'):
            fix_bogus_finally(os.path.join(SRC, f))
    fix_event_refs(os.path.join(SRC, 'ColorPickerControl.cs'))
    fix_event_refs(os.path.join(SRC, 'SimulationRunner.cs'))
    fix_avi()
    for form in ('Form1', 'frmDescription', 'AboutForm', 'HelpForm'):
        fix_withevents(os.path.join(SRC, form + '.cs'), os.path.join(SRC, form + '.Designer.cs'))
