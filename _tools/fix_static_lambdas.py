"""Replace the two decompiled cached-static-lambda Sort calls with plain lambdas."""
import re

def patch(path, pattern, replacement):
    raw = open(path, 'rb').read()
    text = raw.decode('utf-8-sig')
    new, n = re.subn(pattern, replacement, text, flags=re.S)
    if n != 1:
        raise SystemExit('%s: expected 1 match, got %d' % (path, n))
    open(path, 'wb').write(b'\xef\xbb\xbf' + new.encode('utf-8'))
    print(path, 'patched')

patch('Wave_Dude_2D/GpuDeviceInfo.cs',
      r'list\.Sort\(\(GpuDeviceInfo\._Closure\$__\.\$I9-0 == null\) \? \(GpuDeviceInfo\._Closure\$__\.\$I9-0 = delegate\(GpuDeviceInfo a, GpuDeviceInfo b\)\r\n(\t+)\{.*?\}\) : GpuDeviceInfo\._Closure\$__\.\$I9-0\);',
      lambda m: ('list.Sort((a, b) =>\r\n'
                 + m.group(1) + '{\r\n'
                 + m.group(1) + '\t// discrete GPUs first, then by score descending\r\n'
                 + m.group(1) + '\tif (a.Discrete != b.Discrete)\r\n'
                 + m.group(1) + '\t{\r\n'
                 + m.group(1) + '\t\treturn a.Discrete ? -1 : 1;\r\n'
                 + m.group(1) + '\t}\r\n'
                 + m.group(1) + '\treturn b.Score.CompareTo(a.Score);\r\n'
                 + m.group(1) + '});'))

patch('Wave_Dude_2D/ProjectStore.cs',
      r'list\.Sort\(\(ProjectStore\._Closure\$__\.\$I14-0 == null\) \? \(ProjectStore\._Closure\$__\.\$I14-0 = \(string a, string b\) => string\.Compare\(a, b, StringComparison\.OrdinalIgnoreCase\)\) : ProjectStore\._Closure\$__\.\$I14-0\);',
      'list.Sort((a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));')
