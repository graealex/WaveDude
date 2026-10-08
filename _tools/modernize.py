"""Mechanical C# modernisation of the hand-maintained files (not *.Designer.cs, not My/):

 1. block-bodied single-return getters  -> expression-bodied (`=> expr;` / `get => expr;`)
 2. single-statement setters            -> `set => stmt;`
 3. single-return / single-statement methods of trivial shape stay as they are (readability)
 4. `this.member` qualification removed (the project style; a following build with
    CS1717 (self-assignment) as a tripwire catches any parameter/field shadowing)
"""
import glob, re, sys

SRC = 'Wave_Dude_2D'
NL = r'\r?\n'

# { get { return X; } }  with no setter -> => X;
GETTER_ONLY = re.compile(
    r'(?P<head>^(?P<ind>\t+)(?P<decl>(?:public|internal|protected|private)(?: static| override| virtual| new| readonly)* [^\r\n=;{]+?[A-Za-z0-9_]+))' + NL +
    r'(?P=ind)\{' + NL +
    r'(?P=ind)\tget' + NL +
    r'(?P=ind)\t\{' + NL +
    r'(?P=ind)\t\treturn (?P<expr>[^\r\n]+);' + NL +
    r'(?P=ind)\t\}' + NL +
    r'(?P=ind)\}', re.M)

# get { return X; }  (inside a property that also has a setter) -> get => X;
GETTER_IN = re.compile(
    r'^(?P<ind>\t+)get' + NL + r'(?P=ind)\{' + NL + r'(?P=ind)\treturn (?P<expr>[^\r\n]+);' + NL + r'(?P=ind)\}', re.M)

# set { one statement; } -> set => one statement;
SETTER_IN = re.compile(
    r'^(?P<ind>\t+)set' + NL + r'(?P=ind)\{' + NL + r'(?P=ind)\t(?P<stmt>[^\r\n{}]+;)' + NL + r'(?P=ind)\}', re.M)

THIS = re.compile(r'\bthis\.(?=[A-Za-z_])')

def modernize(text):
    before = text
    text = GETTER_ONLY.sub(lambda m: '%s%s => %s;' % (m.group('ind'), m.group('decl'), m.group('expr')), text)
    text = GETTER_IN.sub(lambda m: '%sget => %s;' % (m.group('ind'), m.group('expr')), text)
    text = SETTER_IN.sub(lambda m: '%sset => %s' % (m.group('ind'), m.group('stmt')), text)
    text = THIS.sub('', text)
    return text

total = 0
for path in sorted(glob.glob(SRC + '/*.cs')):
    if path.endswith('.Designer.cs'):
        continue
    raw = open(path, 'rb').read().decode('utf-8')
    new = modernize(raw)
    if new != raw:
        open(path, 'wb').write(new.encode('utf-8'))
        total += 1
        print('modernized', path)
print(total, 'files changed')
