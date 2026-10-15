"""List every private field/const of the classes in the given .cs files with usage counts, grouped by method."""
import re, sys

STRING = re.compile(r'"(?:[^"\\]|\\.)*"')
FIELD = re.compile(r'^\t\tprivate (?:readonly |static |const )*([A-Za-z0-9_<>\[\],]+) (_[A-Za-z0-9_]+|[A-Z][A-Za-z0-9_]*)(?: =|;)', re.M)
METHOD = re.compile(r'^\t\t(?:public|private|internal|protected)[^\r\n=;]*? ([A-Za-z0-9_]+)\([^)]*\)\s*(?:=>|\r?\n\t\t\{)', re.M)

for f in sys.argv[1:]:
    src = open(f, encoding='utf-8').read()
    code = '\n'.join(l for l in src.split('\n') if not l.strip().startswith('///'))
    code = STRING.sub('""', code)
    fields = FIELD.findall(code)
    meths = [(m.group(1), m.start()) for m in METHOD.finditer(code)]
    print('=== %s: %d private fields/consts' % (f, len(fields)))
    for typ, name in fields:
        uses = []
        for m in re.finditer(r'(?<![A-Za-z0-9_.])' + re.escape(name) + r'\b', code):
            line = code[code.rfind('\n', 0, m.start()) + 1:m.end()]
            if not line.startswith('\t\tprivate'):
                uses.append(m.start())
        where = {}
        for u in uses:
            owner = [n for n, s in meths if s <= u]
            key = owner[-1] if owner else '<decl>'
            where[key] = where.get(key, 0) + 1
        flag = '<-- ' if len(uses) <= 2 or len(where) <= 1 else '    '
        print('%s%-26s %-20s uses=%-3d %s' % (flag, name, typ, len(uses), ', '.join('%s(%d)' % kv for kv in sorted(where.items()))))
