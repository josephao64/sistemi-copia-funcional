$ErrorActionPreference = 'Stop'
$reportSource = 'C:\Users\Elizabeth\Downloads\sistema rest\sistemi-pos-restaurante\docs\FASE_6_REPORTE_Y_MANUAL_DE_USUARIO.docx'
$reportPdf = 'C:\Users\Elizabeth\Downloads\sistema rest\reportes-fase6-trabajo\render\reporte.pdf'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $reportPdf) | Out-Null
$reportWord = $null
$reportDocument = $null
try {
    $reportWord = New-Object -ComObject Word.Application
    $reportWord.Visible = $false
    $reportWord.DisplayAlerts = 0
    $reportWord.AutomationSecurity = 3
    $reportDocument = $reportWord.Documents.Open($reportSource, $false, $true, $false)
    $reportDocument.Fields.Update() | Out-Null
    $reportDocument.Repaginate()
    $reportDocument.ExportAsFixedFormat($reportPdf, 17)
    Write-Output "PDF generado para revisión del documento."
} finally {
    if ($null -ne $reportDocument) { $reportDocument.Close(0); [Runtime.InteropServices.Marshal]::FinalReleaseComObject($reportDocument) | Out-Null }
    if ($null -ne $reportWord) { $reportWord.Quit(0); [Runtime.InteropServices.Marshal]::FinalReleaseComObject($reportWord) | Out-Null }
}
