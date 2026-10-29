from pathlib import Path
from collections import Counter
import json
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT, TA_CENTER
from reportlab.lib.pagesizes import letter
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parents[2]
EVIDENCE=ROOT/'artifacts/phase8-tests'
OUT=ROOT/'output/pdf'
OUT.mkdir(parents=True,exist_ok=True)
NS={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
cases=[];groups=Counter();runs=[]
for name in ['phase8-unit.trx','phase8-components.trx']:
    root=ET.parse(EVIDENCE/name).getroot()
    counters=root.find('t:ResultSummary/t:Counters',NS).attrib
    assert counters['failed']=='0' and counters['passed']==counters['total']
    definitions={x.attrib['id']:x.find('t:TestMethod',NS).attrib['className'].split(',')[0].split('.')[-1]
                 for x in root.findall('t:TestDefinitions/t:UnitTest',NS)}
    runs.append({'file':name,'total':int(counters['total']),'passed':int(counters['passed']),'failed':int(counters['failed'])})
    for item in root.findall('t:Results/t:UnitTestResult',NS):
        group=definitions[item.attrib['testId']];groups[group]+=1
        cases.append({'group':group,'name':item.attrib['testName'],'outcome':item.attrib['outcome'],'duration':item.attrib.get('duration')})
summary={'date':'2026-10-21','timezone':'America/Guatemala','total':len(cases),'passed':sum(x['outcome']=='Passed' for x in cases),'runs':runs,'cases':cases}
(EVIDENCE/'phase8-results.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
assert summary['passed']==44

coverage=[
('CTL-01','FEL por empresa','Expediente y bloqueos fiscales probados. Emisión real pendiente de contrato y adaptador.'),
('CTL-02','Consulta NIT','Fallback local y captura manual probados. Servicio externo pendiente de contrato.'),
('CTL-03','Auditoría contextual','Contexto, ocultación de credenciales y bloqueo de borrado comprobados.'),
('CTL-04','Ventas y cobros','Permisos y alcance negativo probados. Consultas con ventas reales pendientes.'),
('CTL-05','Inventario y diferencias','Modelo y precisión histórica comprobados. Operación de Kardex, conteos y merma pendiente con datos reales.'),
('CTL-06','Compras y proveedores','Reportes implementados. Recepción parcial, costos y facturas requieren aceptación funcional.'),
('CTL-07','Rentabilidad histórica','Cuatro casos de cálculo aprobados; conserva costo ante reembolso sin retorno físico.'),
('CTL-08','Centro de control','Controles de acceso probados. Indicadores, navegación y resultados reales requieren revisión funcional.'),
('CTL-09','Excel y PDF','Excel tipado y PDF paginado comprobados. Exportación completa desde la consulta requiere aceptación funcional.'),
('CTL-10','Respaldo y recuperación','Rechazo de configuración insegura y límites probados. Respaldo real y simulacro aún pendientes.')]

md=["# Reporte de pruebas de la fase 8", "", "Fecha: 21 de octubre de 2026. Zona: America/Guatemala.", "",
"## Resultado", "", "44 pruebas aprobadas, 0 fallidas y 0 omitidas: 21 unitarias y 23 de componentes/modelo. Compilación de los proyectos de prueba y dependencias correcta, sin errores ni advertencias.", "",
"Las pruebas de componentes usan el modelo PostgreSQL sin abrir conexiones. No certifican el funcionamiento de consultas completas sobre una base real, despliegue, interfaz, certificador ni recuperación del servidor.", "",
"## Distribución", "", "| Grupo | Casos aprobados |", "|---|---:|"]
for group,n in groups.items(): md.append(f'| {group} | {n} |')
md += ['', '## Cobertura del plan', '', '| Clave | Área | Resultado y límite |','|---|---|---|']
for row in coverage:md.append('| '+' | '.join(row)+' |')
md+=['','## Evidencia por caso','', '| Grupo | Caso | Resultado |','|---|---|---|']
for x in sorted(cases,key=lambda x:(x['group'],x['name'])):
    md.append('| '+x['group']+' | '+x['name'].replace('|','/')+' | Aprobada |')
md+=['','## Ejecución reproducible','','```powershell',
'dotnet build tests/SistemiPos.IntegrationTests/SistemiPos.IntegrationTests.csproj --no-restore -c Release -m:1 -nr:false -p:UseSharedCompilation=false',
'dotnet test tests/SistemiPos.UnitTests/SistemiPos.UnitTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~HistoricalProfitTests|FullyQualifiedName~FiscalSubmissionTests|FullyQualifiedName~NitLookupServiceTests|FullyQualifiedName~Orders.RestaurantOrderTests" --logger "trx;LogFileName=phase8-unit.trx" --results-directory artifacts/phase8-tests',
'dotnet test tests/SistemiPos.IntegrationTests/SistemiPos.IntegrationTests.csproj --no-build --no-restore -c Release --filter "FullyQualifiedName~PhaseEightControlTests|FullyQualifiedName~PhaseEightModelTests|FullyQualifiedName~ExcelExportServiceTests" --logger "trx;LogFileName=phase8-components.trx" --results-directory artifacts/phase8-tests',
'```','','El entorno restringido impedía la comunicación local de testhost (socket 10013). Las dos ejecuciones exitosas se realizaron fuera de esa restricción. No hubo fallos de casos en las ejecuciones finales. No se cambió código productivo en esta ronda de pruebas.','',
'## Pendientes para cierre operativo','',
'- Aplicar la migración `20261021054815_PhaseEightControl` en un entorno preparado y con respaldo. No se aplicó durante esta validación.',
'- Validar consultas y exportaciones con datos reales de ventas, pagos mixtos, monedas, devoluciones, compras e inventario.',
'- Revisar interfaz y navegación manualmente; el proyecto reserva esas pruebas al usuario.',
'- Contratar e implementar el adaptador FEL/NIT, homologar y resolver consulta de estado, rechazos y conciliación antes de producción.',
'- Configurar pg_dump y carpeta privada; realizar respaldo, copia externa y simulacro aislado con objetivos RPO/RTO acordados.',
'- Los históricos sin costo o ubicación documentados no se reconstruyen artificialmente.','',
'## Archivos de esta ronda','',
'- Nuevo: `tests/SistemiPos.IntegrationTests/Infrastructure/PhaseEightControlTests.cs`.',
'- Evidencia: `artifacts/phase8-tests/phase8-unit.trx`, `phase8-components.trx` y `phase8-results.json`.',
'- Reporte: `output/pdf/Reporte_Fase_8_SISTEMI_POS.pdf`.',
'- Manual de implementación: `docs/FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md`.','',
'## Conclusión','',
'Los 44 casos focalizados aprobaron y la compilación técnica es correcta. La fase dispone de implementación y evidencia de controles/cálculos; el cierre integral requiere aceptación funcional con base real, contratación fiscal y recuperación demostrada. No corresponde declararla certificada para producción únicamente con estas pruebas.','']
(ROOT/'docs/REPORTE_PRUEBAS_FASE_8.md').write_text('\n'.join(md),encoding='utf-8')

styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='ReportTitle',fontName='Helvetica-Bold',fontSize=24,leading=28,textColor=colors.black,spaceAfter=14))
styles.add(ParagraphStyle(name='ReportH1',fontName='Helvetica-Bold',fontSize=16,leading=20,textColor=colors.black,spaceAfter=12))
styles.add(ParagraphStyle(name='ReportH2',fontName='Helvetica-Bold',fontSize=11.5,leading=15,textColor=colors.black,spaceBefore=12,spaceAfter=5))
styles.add(ParagraphStyle(name='BodyReport',fontName='Helvetica',fontSize=10,leading=14,spaceAfter=9,textColor=colors.HexColor('#253342')))
styles.add(ParagraphStyle(name='SmallReport',fontName='Helvetica',fontSize=8.5,leading=11.5,spaceAfter=7,textColor=colors.HexColor('#52606D')))
styles.add(ParagraphStyle(name='TableReport',fontName='Helvetica',fontSize=9,leading=12,textColor=colors.HexColor('#253342')))
styles.add(ParagraphStyle(name='TableHead',fontName='Helvetica-Bold',fontSize=9,leading=12,textColor=colors.white))
flow=[]
def p(text,style='BodyReport'):
    flow.append(Paragraph(text,styles[style]))
def h(text):p(text,'ReportH2')
def bullet(text):p('• '+text)
def table(headers,rows,widths):
    data=[[Paragraph(escape(str(x)),styles['TableHead']) for x in headers]]
    data += [[Paragraph(escape(str(x)),styles['TableReport']) for x in row] for row in rows]
    t=Table(data,colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#19364B')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#F3F6F8')]),
        ('GRID',(0,0),(-1,-1),.5,colors.HexColor('#D9D9D9')),('VALIGN',(0,0),(-1,-1),'MIDDLE'),('LEFTPADDING',(0,0),(-1,-1),9),
        ('RIGHTPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),8),('BOTTOMPADDING',(0,0),(-1,-1),8)]))
    flow.extend([t,Spacer(1,12)])

p('Reporte de la fase 8','ReportTitle')
p('SISTEMI POS ERP<br/>Control reportes y respaldos','ReportH1')
p('21 de octubre de 2026 · America/Guatemala','SmallReport')
p('La fase 8 incorpora control por ubicaciones autorizadas, auditoría, rentabilidad histórica, exportaciones y preparación fiscal y de respaldos. Este reporte resume lo implementado, la evidencia técnica obtenida y las condiciones que faltan para su cierre operativo.')
h('Resultado de las pruebas')
p('<b>44 aprobadas · 0 fallidas · 0 omitidas</b>')
p('Se ejecutaron 21 pruebas unitarias y 23 de componentes y modelo. La compilación de las pruebas ampliadas y sus dependencias terminó con cero errores y cero advertencias. Las comprobaciones de sintaxis de JavaScript y del script de recuperación también finalizaron correctamente.')
labels={'RestaurantOrderTests':'Transiciones y protección de pedidos POS','HistoricalProfitTests':'Cálculo de rentabilidad histórica','NitLookupServiceTests':'Consulta NIT y captura manual','FiscalSubmissionTests':'Estado fiscal y clave estable','PhaseEightControlTests':'Permisos auditoría PDF respaldos y modelo','PhaseEightModelTests':'Unicidad y concurrencia del expediente fiscal','ExcelExportServiceTests':'Archivo Excel con datos tipados'}
table(['Área comprobada','Casos','Resultado'],[(labels[k],v,'Aprobados') for k,v in groups.items()],[362,56,104])
h('Conclusión de la entrega')
p('Los controles y cálculos cubiertos por estas pruebas funcionan en los escenarios ejecutados. La emisión FEL real, las consultas con una base de operación y la recuperación del servidor todavía requieren validación específica. <b>Los resultados no equivalen a un cierre integral de producción.</b>')
p('No se aplicaron migraciones, no se modificó una base de operación, no se enviaron documentos a un certificador y no se ejecutaron respaldos o restauraciones reales.','SmallReport')

flow.append(PageBreak())
p('Cobertura del plan de implementación','ReportH1')
p('La siguiente matriz distingue implementación y evidencia disponible. Las funciones existentes se consultan sin volver a registrar ventas, compras ni consumos de inventario.')
table(['Clave','Área','Estado y comprobación'],coverage,[53,128,341])
p('La migración preparada es <b>20261021054815_PhaseEightControl</b>. Incorpora ubicación en auditoría, expediente fiscal y campos de costo histórico; la coherencia del modelo con su snapshot pasó la comprobación automatizada. Aplicarla y validarla con datos de preparación sigue siendo un paso de despliegue.','SmallReport')

flow.append(PageBreak())
p('Explicación de las comprobaciones','ReportH1')
h('Acceso por ubicación y permisos')
p('Se comprobó el rechazo de consultas sin permiso de reportes, exportaciones sin autorización propia y auditoría sin su permiso específico. También se rechazaron sucursales o empresas no asignadas, otra sucursal sin permiso de consolidación y consultas sin sucursal activa. Los rangos invertidos o demasiado amplios se rechazan antes de consultar datos.')
h('Rentabilidad y continuidad del pedido')
p('Los casos de cálculo verifican que un reembolso de comida preparada conserva el costo consumido cuando no hay retorno físico; una devolución física reduce el costo proporcionalmente; los productos repetidos distribuyen el retorno por cantidad, y los costos conservan seis decimales. Las 12 pruebas de pedidos comprueban transiciones y restricciones del flujo existente. Estas pruebas no reconstruyen costos desconocidos de ventas antiguas.')
h('Auditoría y flujo fiscal')
p('Se comprobó la captura del contexto y usuario, la ocultación de una credencial de prueba y el rechazo del borrado de auditoría. El contenido fiscal original no se puede reescribir. El expediente conserva una clave estable por venta y empresa, y un resultado externo incierto bloquea otro envío. El proveedor sin configurar rechaza certificar; el fallback NIT permite continuar con captura manual.')
h('Archivos de exportación')
p('La prueba Excel verifica la estructura OpenXML, los valores numéricos y la fecha tipada. Las pruebas PDF verifican encabezados repetidos, varias páginas, caracteres especiales escapados, posiciones de referencias internas y mensaje de ausencia de registros. Esto comprueba el generador; la descarga de una consulta completa con datos reales requiere aceptación funcional.')
h('Respaldo y modelo de datos')
p('Se comprobó que una configuración ausente o una carpeta pública impide iniciar el respaldo, que intervalo y retención se mantienen dentro de sus límites y que no se inventan objetivos RPO/RTO. El modelo conserva unicidad por venta, concurrencia fiscal e índices contextuales. No se ejecutó pg_dump ni se midió una recuperación.')
h('Incidencia resuelta del ejecutor')
p('El entorno restringido bloqueaba el socket local de testhost con el error 10013. Las ejecuciones exitosas se realizaron fuera de esa restricción. El problema anterior era de comunicación del ejecutor; los 44 casos finales concluyeron aprobados.','SmallReport')

flow.append(PageBreak())
p('Uso y aceptación pendiente','ReportH1')
h('Cómo utilizar la fase 8')
for text in [
'Selecciona tu sucursal y abre <b>Control por ubicación</b>. Elige el reporte y revisa el texto de alcance aplicado.',
'Filtra por fechas y ubicación. Usa usuario/caja en ventas, proveedor en compras y bodega/producto en inventario, según corresponda al reporte.',
'Selecciona los productos y proveedores en las sugerencias. Pulsa <b>Consultar</b> y abre el documento desde <b>Ver</b>. Un detalle mantiene sus propios permisos y puede requerir cambiar de sucursal.',
'Para descargar, utiliza <b>Excel de esta página</b> o <b>PDF de esta página</b>. La descarga incluye hasta 100 filas; no equivale a exportar toda la base.',
'En <b>Facturación por empresa</b>, preparar una venta conserva su expediente. Sin contrato y conector, permanece pendiente de configuración y no produce un DTE certificado.',
'En <b>Respaldo y recuperación</b>, configura herramientas y carpeta privada antes de crear archivos. El hash verifica integridad; el simulacro aislado demuestra recuperación.'
]:bullet(text)
h('Validaciones para cerrar la fase')
for text in [
'Aplicar la migración con respaldo previo y verificar ventas reales, pagos mixtos, monedas y devoluciones con y sin retorno físico.',
'Contrastar compras y recepción parcial, existencias, Kardex, diferencias de conteo, producción y merma con documentos reales.',
'Revisar interfaz, navegación y exportaciones paginadas con usuarios de distintos permisos. Estas pruebas manuales corresponden al usuario.',
'Contratar y homologar el proveedor FEL/NIT. Completar adaptador, consulta de estado, rechazos y conciliación antes de activar emisión productiva.',
'Configurar respaldo y copia externa; ejecutar el simulacro en una base aislada y acordar y comprobar los objetivos RPO/RTO.'
]:bullet(text)
h('Evidencia y documentación')
p('Resultados por caso: <b>docs/REPORTE_PRUEBAS_FASE_8.md</b>.<br/>Archivos de ejecución: <b>artifacts/phase8-tests/phase8-unit.trx</b> y <b>phase8-components.trx</b>.<br/>Resumen estructurado: <b>phase8-results.json</b> en esa misma carpeta.<br/>Manual y permisos: <b>docs/FASE_8_CONTROL_REPORTES_Y_RESPALDOS.md</b>.','SmallReport')
p('En esta ronda se añadió el archivo de pruebas PhaseEightControlTests.cs y se actualizaron los reportes. No fue necesario modificar código productivo para obtener los resultados anteriores.','SmallReport')

def footer(canvas,doc):
    canvas.saveState();canvas.setFont('Helvetica',8);canvas.setFillColor(colors.HexColor('#64748B'))
    canvas.drawString(45,28,'SISTEMI POS ERP · Fase 8 · 21 de octubre de 2026')
    canvas.drawRightString(567,28,str(doc.page));canvas.restoreState()

target=OUT/'Reporte_Fase_8_SISTEMI_POS.pdf'
doc=SimpleDocTemplate(str(target),pagesize=letter,rightMargin=45,leftMargin=45,topMargin=42,bottomMargin=45,
    title='Reporte de la fase 8 SISTEMI POS ERP',author='SISTEMI POS ERP')
doc.build(flow,onFirstPage=footer,onLaterPages=footer)
reader=PdfReader(str(target))
print(json.dumps({'pdf':str(target),'pages':len(reader.pages),'total':summary['total'],'passed':summary['passed'],'groups':dict(groups)},ensure_ascii=False))
