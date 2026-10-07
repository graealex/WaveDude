"""Split Form1.cs into partial-class files along its feature seams (no code changes).

Each section starts at the named method (including the decompiler comment lines
directly above it) and runs to the line before the next section. The Sources
section ends before the first field token comment (`// Token: 0x04...`).
Fields and nested types stay in Form1.cs. Adds the new files to the csproj.
"""
import re

SRC = 'Wave_Dude_2D/'
SECTIONS = [
    ('Form1.Projects.cs',   'BuildProjectEntries'),
    ('Form1.Output.cs',     'InitializeOutputsTab'),
    ('Form1.Scene.cs',      'InitializeSceneControls'),
    ('Form1.Colors.cs',     'btbChooseFFolder_Click'),
    ('Form1.Simulation.cs', 'InitializeSimulationControls'),
    ('Form1.Probes.cs',     'ProbeUi'),
    ('Form1.Sources.cs',    'InitializeSourceControls'),
]

raw = open(SRC + 'Form1.cs', 'rb').read()
lines = raw.decode('utf-8-sig').split('\r\n')

def method_start(name):
    sig = re.compile(r'^\t\t(private|public|internal|protected)[^=]*\b' + re.escape(name) + r'\(')
    i = next(i for i, l in enumerate(lines) if sig.match(l))
    while lines[i - 1].strip().startswith('//'):
        i -= 1
    return i

starts = [method_start(m) for _, m in SECTIONS]
last_method = starts[-1]
fields_start = next(i for i in range(last_method, len(lines)) if re.match(r'^\t\t// Token: 0x04', lines[i]))
bounds = starts + [fields_start]

header_end = next(i for i, l in enumerate(lines) if l.startswith('namespace '))
usings = lines[:header_end]

def wrap(body):
    out = list(usings) + ['namespace _2D_wave_sim_V01', '{', '\tpublic partial class Form1', '\t{']
    # trim leading/trailing blank lines of the body
    while body and body[0] == '':
        body.pop(0)
    while body and body[-1] == '':
        body.pop()
    out += body + ['\t}', '}', '']
    return out

for (fname, _), a, b in zip(SECTIONS, bounds, bounds[1:]):
    open(SRC + fname, 'wb').write(b'\xef\xbb\xbf' + '\r\n'.join(wrap(lines[a:b])).encode('utf-8'))
    print('%-22s lines %5d-%5d (%d)' % (fname, a + 1, b, b - a))

core = lines[:starts[0]] + lines[fields_start:]
open(SRC + 'Form1.cs', 'wb').write(b'\xef\xbb\xbf' + '\r\n'.join(core).encode('utf-8'))
print('Form1.cs now %d lines' % len(core))

proj = SRC + 'Wave_Dude_2D.csproj'
p = open(proj, 'rb').read().decode('utf-8-sig')
items = ''.join('    <Compile Include="%s">\r\n      <DependentUpon>Form1.cs</DependentUpon>\r\n    </Compile>\r\n' % f for f, _ in SECTIONS)
anchor = '    <Compile Include="Form1.Designer.cs">\r\n      <DependentUpon>Form1.cs</DependentUpon>\r\n    </Compile>\r\n'
assert anchor in p
p = p.replace(anchor, anchor + items, 1)
open(proj, 'wb').write(b'\xef\xbb\xbf' + p.encode('utf-8'))
print('csproj updated')
