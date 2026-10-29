$ErrorActionPreference = 'Stop'
$reportSource = 'C:\Users\Elizabeth\Downloads\sistema rest\sistemi-pos-restaurante\docs\entrega_fase6\Informe_Entrega_Fase_6_SISTEMI_POS.docx'
$reportPdf = 'C:\Users\Elizabeth\Downloads\sistema rest\sistemi-pos-restaurante\docs\entrega_fase6\Informe_Entrega_Fase_6_SISTEMI_POS.pdf'
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
    Write-Output 'Informe exportado a PDF.'
} finally {
    if ($null -ne $reportDocument) { $reportDocument.Close(0); [Runtime.InteropServices.Marshal]::FinalReleaseComObject($reportDocument) | Out-Null }
    if ($null -ne $reportWord) { $reportWord.Quit(0); [Runtime.InteropServices.Marshal]::FinalReleaseComObject($reportWord) | Out-Null }
}
