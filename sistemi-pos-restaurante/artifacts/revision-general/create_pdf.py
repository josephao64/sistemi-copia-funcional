from pathlib import Path
from html import escape
import re
import json
from reportlab.pdfgen import canvas
from reportlab.platypus import BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, Table, TableStyle, KeepTogether, PageBreak
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from pypdf import PdfReader

ROOT = Path.cwd()
SOURCE = ROOT / 'docs/REVISION_GENERAL_COHERENCIA_Y_ROLES_2026-10-21.md'
OUT = ROOT / 'output/pdf/Reporte_Revision_General_Coherencia_y_Roles_SISTEMI_POS_ERP.pdf'
OUT.parent.mkdir(parents=True, exist_ok=True)

fontroot = Path('C:/Windows/Fonts')
for name, file in [('Body', 'arial.ttf'), ('BodyBold', 'arialbd.ttf'), ('BodyItalic', 'ariali.ttf'), ('Mono', 'consola.ttf')]:
    pdfmetrics.registerFont(TTFont(name, str(fontroot / file)))
pdfmetrics.registerFontFamily('Body', normal='Body', bold='BodyBold', italic='BodyItalic', boldItalic='BodyBold')
INK = colors.HexColor('#202B39')
MUTED = colors.HexColor('#586575')
BLUE = colors.HexColor('#183F59')
LIGHT = colors.HexColor('#F2F5F7')
WIDTH = 504

styles = {
    'title': ParagraphStyle('Title', fontName='BodyBold', fontSize=23, leading=28, textColor=colors.black, spaceAfter=14),
    'h2': ParagraphStyle('Section', fontName='BodyBold', fontSize=15, leading=20, textColor=colors.black, spaceBefore=18, spaceAfter=9, keepWithNext=True),
    'h3': ParagraphStyle('Finding', fontName='BodyBold', fontSize=12, leading=16, textColor=colors.black, spaceBefore=9, spaceAfter=8, keepWithNext=True),
    'body': ParagraphStyle('Body', fontName='Body', fontSize=10.4, leading=15.1, textColor=INK, spaceAfter=9, splitLongWords=True, allowWidows=False, allowOrphans=False),
    'source': ParagraphStyle('Sources', fontName='Body', fontSize=8, leading=11.4, textColor=MUTED, spaceAfter=10, splitLongWords=True),
    'cell': ParagraphStyle('Cell', fontName='Body', fontSize=8.8, leading=12.2, textColor=INK, spaceAfter=0, splitLongWords=True),
    'th': ParagraphStyle('TableHeader', fontName='BodyBold', fontSize=9, leading=12, textColor=colors.white),
    'code': ParagraphStyle('Code', fontName='Mono', fontSize=8.1, leading=11.8, textColor=INK, leftIndent=10, rightIndent=8, spaceAfter=10, splitLongWords=True),
    'bullet': ParagraphStyle('Bullet', fontName='Body', fontSize=10.4, leading=15.1, textColor=INK, leftIndent=13, firstLineIndent=-10, spaceAfter=7),
    'date': ParagraphStyle('Date', fontName='Body', fontSize=9, leading=13, textColor=MUTED, spaceAfter=12),
}

def inline(text):
    # Preserve human-readable source labels. Local source paths remain in the Markdown original.
    text = re.sub(r'\[([^\]]+)\]\(<[^>]+>\)', lambda m: m.group(1), text)
    text = text.replace('\u2011', '-').replace('\u2013', '-').replace('\u2014', '-')
    # The one emoji is inside a quoted source identifier; its omission does not change report content.
    text = text.replace('🔔', '')
    parts = re.split(r'(`[^`]+`|\*\*[^*]+\*\*)', text)
    out = []
    for part in parts:
        if part.startswith('`') and part.endswith('`'):
            out.append('<font name="Mono" size="9">' + escape(part[1:-1]) + '</font>')
        elif part.startswith('**') and part.endswith('**'):
            out.append('<b>' + escape(part[2:-2]) + '</b>')
        else:
            out.append(escape(part))
    return ''.join(out)

def para(text, style='body'):
    return Paragraph(inline(text), styles[style])

def make_table(lines):
    rows = [[c.strip() for c in line.strip().strip('|').split('|')] for line in lines]
    rows = [row for row in rows if not all(re.fullmatch(r':?-+:?', c) for c in row)]
    column_count = len(rows[0])
    if rows[0][0] == 'Rol inicial':
        widths = [90, 145, 269]
    elif rows[0][0] == 'Fase del plan':
        widths = [85, 154, 265]
    else:
        widths = [88, 208, 208]
    assert sum(widths) == WIDTH and column_count == len(widths)
    data = [[para(c, 'th' if i == 0 else 'cell') for c in row] for i, row in enumerate(rows)]
    table = Table(data, colWidths=widths, repeatRows=1, hAlign='LEFT', splitByRow=True)
    table.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), BLUE),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, LIGHT]),
        ('GRID', (0, 0), (-1, -1), 0.45, colors.HexColor('#D4DBE1')),
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('LEFTPADDING', (0, 0), (-1, -1), 9),
        ('RIGHTPADDING', (0, 0), (-1, -1), 9),
        ('TOPPADDING', (0, 0), (-1, -1), 9),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 9),
    ]))
    return table

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.saved = []
    def showPage(self):
        self.saved.append(dict(self.__dict__))
        self._startPage()
    def save(self):
        total = len(self.saved)
        for state in self.saved:
            self.__dict__.update(state)
            self.setFillColor(MUTED)
            self.setFont('Body', 8)
            self.drawString(54, 32, '21 de octubre de 2026  |  America/Guatemala')
            self.drawRightString(558, 32, f'{self._pageNumber} / {total}')
            super().showPage()
        super().save()

def draw_header(canv, document):
    canv.saveState()
    canv.setFillColor(MUTED)
    canv.setFont('Body', 8)
    canv.drawString(54, 757, 'SISTEMI POS ERP  |  Revisión general de coherencia y roles')
    canv.setStrokeColor(colors.HexColor('#D4DBE1'))
    canv.setLineWidth(0.4)
    canv.line(54, 747, 558, 747)
    canv.restoreState()

raw = SOURCE.read_text(encoding='utf-8')
lines = raw.splitlines()
story = []
finding_group = None

def append(flowable):
    (finding_group if finding_group is not None else story).append(flowable)

def flush_finding():
    global finding_group
    if finding_group:
        story.append(KeepTogether(finding_group))
    finding_group = None

i = 0
while i < len(lines):
    line = lines[i].strip()
    if not line:
        i += 1
        continue
    if line.startswith('# '):
        append(para('Revisión general de coherencia y roles', 'title'))
        append(Paragraph('SISTEMI POS ERP', ParagraphStyle('Subtitle', fontName='BodyBold', fontSize=13, leading=18, textColor=colors.black, spaceAfter=10)))
        i += 1
        continue
    if line.startswith('## '):
        flush_finding()
        heading = line[3:]
        if heading in ['Relación entre las fases', 'Pantallas y capacidades de cada rol', 'Otros puntos que requieren una decisión o aceptación', 'Recorridos de aceptación por usuario']:
            story.append(PageBreak())
        story.append(para(heading, 'h2'))
        i += 1
        continue
    if line.startswith('### '):
        flush_finding()
        finding_group = []
        append(para(line[4:], 'h3'))
        i += 1
        continue
    if line.startswith('|'):
        table_lines = []
        while i < len(lines) and lines[i].strip().startswith('|'):
            table_lines.append(lines[i])
            i += 1
        append(make_table(table_lines))
        append(Spacer(1, 10))
        continue
    if line.startswith('```'):
        code = []
        i += 1
        while i < len(lines) and not lines[i].strip().startswith('```'):
            code.append(escape(lines[i]))
            i += 1
        append(Paragraph('<br/>'.join(code), styles['code']))
        i += 1
        continue
    if line.startswith('- '):
        append(para('• ' + line[2:], 'bullet'))
        i += 1
        continue
    if re.match(r'^\d+\. ', line):
        append(para(line, 'bullet'))
        i += 1
        continue
    block = [line]
    i += 1
    while i < len(lines) and lines[i].strip() and not re.match(r'^(#|\||```|- |\d+\. )', lines[i].strip()):
        block.append(lines[i].strip())
        i += 1
    text = ' '.join(block)
    style = 'source' if text.startswith(('Fuente:', 'Fuentes:', 'Referencias:')) else 'date' if text.startswith('Fecha:') else 'body'
    append(para(text, style))
flush_finding()

doc = BaseDocTemplate(str(OUT), pagesize=letter, rightMargin=54, leftMargin=54,
    topMargin=59, bottomMargin=51,
    title='Revisión general de coherencia y roles de SISTEMI POS ERP',
    author='SISTEMI POS ERP', subject='Revisión de fases, roles, permisos, cocina, caja y administración')
doc.addPageTemplates([PageTemplate(id='Report', frames=[Frame(54, 51, WIDTH, 682,
    leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)], onPage=draw_header)])
doc.build(story, canvasmaker=NumberedCanvas)

reader = PdfReader(str(OUT))
text = '\n'.join(p.extract_text() or '' for p in reader.pages)
found = re.findall(r'\bR(\d{2}) P[12]\b', text)
assert set(found) == {f'{n:02}' for n in range(1, 25)}, found
assert all((p.extract_text() or '').strip() for p in reader.pages)
assert '116' in text or ('92' in text and '24' in text)
assert 'Recorridos de aceptación por usuario' in text
summary = {'pdf': str(OUT), 'pages': len(reader.pages), 'size': OUT.stat().st_size,
           'findingsPresent': len(set(found)), 'source': str(SOURCE)}
(ROOT / 'artifacts/revision-general/pdf-validation.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(summary, ensure_ascii=False))
