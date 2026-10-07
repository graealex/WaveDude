"""Replace one method body in a decompiled .cs file, preserving BOM + CRLF.

usage: replace_method.py <file.cs> <signature-substring> <replacement.txt>

The method is located by the first line containing <signature-substring>;
the replaced range runs from any directly preceding '// Token:' comment lines
to the closing brace at the same indentation as the signature line.
"""
import sys

path, sig, repl_path = sys.argv[1:4]
raw = open(path, 'rb').read()
bom = raw.startswith(b'\xef\xbb\xbf')
text = raw.decode('utf-8-sig')
lines = text.split('\r\n')

start = next(i for i, l in enumerate(lines) if sig in l)
indent = lines[start][:len(lines[start]) - len(lines[start].lstrip('\t'))]
# swallow preceding decompiler comment lines
while start > 0 and lines[start - 1].strip().startswith('//'):
    start -= 1
end = start
while end < len(lines) and lines[end] != indent + '}':
    end += 1
if end == len(lines):
    sys.exit('closing brace not found for ' + sig)

repl = open(repl_path, 'rb').read().decode('utf-8-sig').replace('\r\n', '\n').rstrip('\n').split('\n')
lines[start:end + 1] = repl
out = '\r\n'.join(lines).encode('utf-8')
open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + out)
print('%s: replaced lines %d-%d with %d lines' % (path, start + 1, end + 1, len(repl)))
