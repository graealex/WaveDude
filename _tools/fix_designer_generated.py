"""Remove VB [DesignerGenerated] attributes and replace the _rootBlinkTimer WithEvents property with a field."""
import glob, re

def rw(path, fn):
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    new = fn(text)
    if new != text:
        open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + new.encode('utf-8'))
        print('patched', path)

for p in glob.glob('Wave_Dude_2D/*.cs'):
    rw(p, lambda t: re.sub(r'^\t\[(global::Microsoft\.VisualBasic\.CompilerServices\.)?DesignerGenerated\]\r\n', '', t, flags=re.M))

def fix_timer(t):
    # property -> field
    t, n = re.subn(
        r'(\t\t// Token:[^\r\n]*\r\n)*\t\tprivate virtual global::System\.Windows\.Forms\.Timer _rootBlinkTimer\r\n\t\t\{.*?\r\n\t\t\}\r\n',
        '\t\tprivate readonly global::System.Windows.Forms.Timer _rootBlinkTimer;\r\n', t, count=1, flags=re.S)
    assert n == 1, 'timer property not found'
    # drop backing field
    t, n = re.subn(r'(\t\t// Token:[^\r\n]*\r\n)*\t\tprivate global::System\.Windows\.Forms\.Timer __rootBlinkTimer;\r\n\r\n?', '', t, count=1)
    # (the decompiler never emitted the backing field, so n may be 0)
    # wire the Tick handler where the timer is created
    t, n = re.subn(r'(\t\t\tthis\._rootBlinkTimer = new global::System\.Windows\.Forms\.Timer\r\n\t\t\t\{\r\n\t\t\t\tInterval = 700\r\n\t\t\t\};\r\n)',
                   r'\1\t\t\tthis._rootBlinkTimer.Tick += this.RootBlinkTimer_Tick;\r\n', t, count=1)
    assert n == 1, 'timer construction not found'
    return t

rw('Wave_Dude_2D/Form1.cs', fix_timer)
