from pathlib import Path
from pypdf import PdfReader
from pdf2image import convert_from_path
from PIL import Image, ImageOps, ImageDraw

root = Path(__file__).parent / 'render'
pdf = root / 'reporte.pdf'
reader = PdfReader(pdf)
print(f'Páginas: {len(reader.pages)}')
for i, page in enumerate(reader.pages, 1):
    text = page.extract_text()
    lines = [x.strip() for x in text.splitlines() if x.strip()]
    print(i, len(text), ' | '.join(lines[:3]))
pages = convert_from_path(str(pdf), dpi=110, fmt='png')
for i, page in enumerate(pages, 1): page.save(root / f'page-{i}.png')
for start in range(0, len(pages), 4):
    sheet = Image.new('RGB', (1000, 1440), 'white')
    for j, page in enumerate(pages[start:start+4]):
        thumb = ImageOps.contain(page, (490, 685))
        x, y = (j % 2) * 500 + 5, (j // 2) * 720 + 25
        sheet.paste(thumb, (x, y))
        ImageDraw.Draw(sheet).text((x, y-20), f'Página {start+j+1}', fill='black')
    sheet.save(root / f'contact-{start//4+1}.png')
