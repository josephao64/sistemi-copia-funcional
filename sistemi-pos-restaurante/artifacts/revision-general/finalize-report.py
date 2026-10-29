from pathlib import Path
import json
import re
import xml.etree.ElementTree as ET

root = Path.cwd()
report = root / 'docs/REVISION_GENERAL_COHERENCIA_Y_ROLES_2026-10-21.md'
content = report.read_text(encoding='utf-8')
references = []

def replace_refs(text):
    result = []
    offset = 0
    while True:
        start = text.find('@ref(', offset)
        if start < 0:
            result.append(text[offset:])
            break
        result.append(text[offset:start])
        depth = 1
        end = start + 5
        while depth and end < len(text):
            if text[end] == '(':
                depth += 1
            elif text[end] == ')':
                depth -= 1
            end += 1
        if depth:
            raise ValueError('Unclosed source reference')
        relative, needle = text[start + 5:end - 1].split('|', 1)
        target = root / relative
        lines = target.read_text(encoding='utf-8-sig').splitlines()
        matches = [i for i, line in enumerate(lines, 1) if needle in line]
        if not matches:
            raise ValueError(f'Source reference not found: {relative} | {needle}')
        line = matches[0]
        link = f'[{target.name}:{line}](<{target.as_posix()}:{line}>)'
        references.append({'path': relative, 'line': line, 'needle': needle})
        result.append(link)
        offset = end
    return ''.join(result)

rendered = replace_refs(content)
assert '@ref(' not in rendered
findings = re.findall(r'^### (R\d+) (P[12]) (.+)$', rendered, re.M)
assert len(findings) == 24
assert len([x for x in findings if x[1] == 'P1']) == 14
assert len([x for x in findings if x[1] == 'P2']) == 10

namespace = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
runs = []
for filename in ['review-unit-validated.trx', 'review-components-validated.trx']:
    document = ET.parse(root / 'artifacts/revision-general' / filename)
    summary = document.find('.//t:ResultSummary', namespace)
    counters = summary.find('t:Counters', namespace)
    assert summary.attrib['outcome'] == 'Completed'
    assert int(counters.attrib['failed']) == 0
    assert int(counters.attrib['executed']) == int(counters.attrib['passed'])
    runs.append({'file': filename, 'outcome': summary.attrib['outcome'], 'counters': counters.attrib})

summary = {
    'date': '2026-10-21', 'timezone': 'America/Guatemala',
    'findings': [{'id': identifier, 'priority': priority, 'title': title} for identifier, priority, title in findings],
    'tests': runs, 'passed': sum(int(x['counters']['passed']) for x in runs),
    'scope': {'databaseAcceptanceExecuted': False, 'liveRoleNavigationExecuted': False,
              'productionCodeModified': False, 'buildErrors': 0, 'buildWarnings': 0},
    'checkedSourceReferences': references
}
report.write_text(rendered, encoding='utf-8')
(root / 'artifacts/revision-general/review-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'report': str(report), 'findings': len(findings), 'sourceReferencesVerified': len(references),
                  'passed': summary['passed']}, ensure_ascii=False))
