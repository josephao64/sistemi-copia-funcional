from pathlib import Path
from pypdf import PdfReader
from pdf2image import convert_from_path
from PIL import Image, ImageOps, ImageDraw
pdf=Path(r'C:\Users\Elizabeth\Downloads\sistema rest\sistemi-pos-restaurante\docs\entrega_fase6\Informe_Entrega_Fase_6_SISTEMI_POS.pdf')
out=Path(r'C:\Users\Elizabeth\Downloads\sistema rest\reportes-fase6-trabajo\entrega_render')
out.mkdir(parents=True,exist_ok=True)
reader=PdfReader(pdf)
print('Paginas:',len(reader.pages))
for i,p in enumerate(reader.pages,1):
    text=p.extract_text() or ''
    lines=[x.strip() for x in text.splitlines() if x.strip()]
    print(i,len(text),' | '.join(lines[:3]),'FINAL', ' | '.join(lines[-3:]))
images=convert_from_path(str(pdf),dpi=110,fmt='png')
for i,page in enumerate(images,1): page.save(out/f'page-{i}.png')
for offset in range(0,len(images),4):
    contact=Image.new('RGB',(1050,1420),'white')
    for j,page in enumerate(images[offset:offset+4]):
        thumb=ImageOps.contain(page,(510,665))
        x,y=10+(j%2)*525,30+(j//2)*705
        contact.paste(thumb,(x,y))
        ImageDraw.Draw(contact).text((x,y-20),f'Pagina {offset+j+1}',fill='black')
    contact.save(out/f'contact-{offset//4+1}.png')
