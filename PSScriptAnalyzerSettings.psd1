@{
    # A CI falha apenas em problemas de severidade "Error"; avisos aparecem no relatório para revisão.
    Severity     = @('Error', 'Warning')
    ExcludeRules = @(
        # O modo terminal usa Write-Host de propósito (saída colorida para o usuário)
        'PSAvoidUsingWriteHost',
        # Os ajustes têm modo de simulação próprio (-DryRun) em vez de -WhatIf por função
        'PSUseShouldProcessForStateChangingFunctions',
        # Nomes no plural refletem o que a função faz (ex.: Clear-SystemTempFiles)
        'PSUseSingularNouns',
        # Arquivos com acentos são gravados em UTF-8 com BOM; get.ps1 é ASCII de propósito (sem BOM)
        'PSUseBOMForUnicodeEncodedFile'
    )
}
