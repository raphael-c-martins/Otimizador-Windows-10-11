<#
.SYNOPSIS
    Modal Gráfico WPF Moderno para Seleção Granular de Aplicativos (Dark Mode).
.DESCRIPTION
    Apresenta uma janela XAML nativa do Windows Presentation Foundation (WPF) permitindo
    ao usuário marcar e desmarcar individualmente os aplicativos antes do debloat.
#>

function Show-AppSelectionDialog {
    param(
        [array]$AllApps,
        [array]$InitiallySelectedIds = @()
    )

    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase | Out-Null

    # Definição do XAML com Estilo Dark Moderno
    [string]$XamlString = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Selecao de Aplicativos para Remocao | Otimizador de Windows"
        Height="650" Width="720"
        WindowStartupLocation="CenterScreen"
        Background="#181818" Foreground="#F0F0F0"
        FontFamily="Segoe UI" WindowStyle="ThreeDBorderWindow" ResizeMode="CanResize">
    <Window.Resources>
        <Style TargetType="CheckBox">
            <Setter Property="Foreground" Value="#E0E0E0"/>
            <Setter Property="Margin" Value="4,4,4,4"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
        <Style TargetType="Button">
            <Setter Property="Background" Value="#2A2A2A"/>
            <Setter Property="Foreground" Value="#FFFFFF"/>
            <Setter Property="BorderBrush" Value="#3D3D3D"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="Margin" Value="4"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontSize" Value="12"/>
        </Style>
    </Window.Resources>
    
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <StackPanel Grid.Row="0" Margin="0,0,0,12">
            <TextBlock Text="Personalizar Remocao de Aplicativos (Debloat)" FontSize="18" FontWeight="Bold" Foreground="#00B4D8"/>
            <TextBlock Text="Marque os itens que deseja desinstalar. Desmarque itens como Xbox Game Bar ou Teams se voce os utiliza no dia a dia." 
                       FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,4,0,0"/>
        </StackPanel>
        
        <!-- Barra de Acoes Rapidas e Busca -->
        <Grid Grid.Row="1" Margin="0,0,0,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            
            <TextBox Name="SearchBox" Grid.Column="0" Height="30" VerticalContentAlignment="Center"
                     Background="#222222" Foreground="#FFFFFF" BorderBrush="#444444" Padding="8,2"
                     FontSize="13" ToolTip="Digite para filtrar os aplicativos..."/>
                     
            <StackPanel Grid.Column="1" Orientation="Horizontal" Margin="8,0,0,0">
                <Button Name="BtnSelectDefault" Content="Padrao Recomendado" Background="#0077B6" BorderBrush="#0096C7"/>
                <Button Name="BtnSelectAll" Content="Marcar Todos"/>
                <Button Name="BtnDeselectAll" Content="Desmarcar Todos"/>
            </StackPanel>
        </Grid>
        
        <!-- Lista de Apps com Scroll -->
        <Border Grid.Row="2" Background="#202020" BorderBrush="#333333" BorderThickness="1" CornerRadius="4" Padding="8">
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel Name="AppsContainer"/>
            </ScrollViewer>
        </Border>
        
        <!-- Rodape com Confirmacao -->
        <Grid Grid.Row="3" Margin="0,14,0,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            
            <TextBlock Name="SelectionCountText" Grid.Column="0" VerticalAlignment="Center" 
                       FontSize="12" Foreground="#48CAE4" Text="0 aplicativo(s) selecionado(s)"/>
                       
            <StackPanel Grid.Column="1" Orientation="Horizontal">
                <Button Name="BtnCancel" Content="Cancelar" Width="100"/>
                <Button Name="BtnConfirm" Content="Confirmar e Continuar" Width="170" 
                        Background="#2B9348" BorderBrush="#55A630" FontWeight="SemiBold"/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>
"@

    $XmlDoc = [xml]$XamlString
    $Reader = (New-Object System.Xml.XmlNodeReader $XmlDoc)
    $Window = [System.Windows.Markup.XamlReader]::Load($Reader)

    # Obter referencias dos controles
    $SearchBox = $Window.FindName("SearchBox")
    $BtnSelectDefault = $Window.FindName("BtnSelectDefault")
    $BtnSelectAll = $Window.FindName("BtnSelectAll")
    $BtnDeselectAll = $Window.FindName("BtnDeselectAll")
    $BtnConfirm = $Window.FindName("BtnConfirm")
    $BtnCancel = $Window.FindName("BtnCancel")
    $AppsContainer = $Window.FindName("AppsContainer")
    $SelectionCountText = $Window.FindName("SelectionCountText")

    $CheckBoxList = [System.Collections.Generic.List[System.Windows.Controls.CheckBox]]::new()

    # Mapeamento de Categoria para Titulo Legivel
    $CategoryNames = @{
        "Gaming"          = "[+] Jogos e Xbox"
        "AI"              = "[+] Inteligencia Artificial e Copilot"
        "Productivity"    = "[+] Produtividade e Office"
        "Communication"   = "[+] Comunicacao e Mensagens"
        "Media"           = "[+] Midia, Video e Audio"
        "Utilities"       = "[+] Utilitarios e Noticias"
        "ThirdPartyBloat" = "[+] Bloatware Pre-instalado (OEM / Patrocinados)"
    }

    # Funcao para atualizar o contador
    $UpdateCount = {
        $SelectedCount = 0
        foreach ($cb in $CheckBoxList) {
            if ($cb.IsChecked -eq $true) { $SelectedCount++ }
        }
        $SelectionCountText.Text = "$SelectedCount aplicativo(s) selecionado(s) para remocao"
    }

    # Agrupar e Renderizar os Aplicativos
    $GroupedApps = $AllApps | Group-Object -Property Category

    foreach ($Group in $GroupedApps) {
        $CatTitle = if ($CategoryNames.ContainsKey($Group.Name)) { $CategoryNames[$Group.Name] } else { $Group.Name }

        # Header do Grupo
        $GroupHeader = New-Object System.Windows.Controls.TextBlock
        $GroupHeader.Text = $CatTitle
        $GroupHeader.FontSize = 14
        $GroupHeader.FontWeight = [System.Windows.FontWeights]::Bold
        $GroupHeader.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#90E0EF")
        $GroupHeader.Margin = New-Object System.Windows.Thickness(4, 10, 4, 4)
        $AppsContainer.Children.Add($GroupHeader) | Out-Null

        foreach ($App in $Group.Group) {
            $Cb = New-Object System.Windows.Controls.CheckBox
            $Cb.Content = "$($App.FriendlyName)"
            $Cb.Tag = $App
            $Cb.ToolTip = "$($App.Description)`nImpacto: $(if($App.Recommendation -eq 'caution'){'Requer cautela'}else{'Seguro'})"
            
            # Checa se deve vir marcado
            if ($InitiallySelectedIds -contains $App.Id) {
                $Cb.IsChecked = $true
            } elseif ($InitiallySelectedIds.Count -eq 0 -and $App.SelectedByDefault) {
                $Cb.IsChecked = $true
            } else {
                $Cb.IsChecked = $false
            }

            # Destacar visualmente apps com cautela
            if ($App.Recommendation -eq "caution") {
                $Cb.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#F4A261")
            }

            $Cb.Add_Checked({ & $UpdateCount })
            $Cb.Add_Unchecked({ & $UpdateCount })

            $CheckBoxList.Add($Cb)
            $AppsContainer.Children.Add($Cb) | Out-Null
        }
    }

    & $UpdateCount

    # Eventos de Acoes Rapidas
    $BtnSelectDefault.Add_Click({
        foreach ($cb in $CheckBoxList) {
            $app = $cb.Tag
            $cb.IsChecked = [bool]$app.SelectedByDefault
        }
        & $UpdateCount
    })

    $BtnSelectAll.Add_Click({
        foreach ($cb in $CheckBoxList) { $cb.IsChecked = $true }
        & $UpdateCount
    })

    $BtnDeselectAll.Add_Click({
        foreach ($cb in $CheckBoxList) { $cb.IsChecked = $false }
        & $UpdateCount
    })

    # Filtro de Busca
    $SearchBox.Add_TextChanged({
        $Filter = $SearchBox.Text.ToLower().Trim()
        foreach ($cb in $CheckBoxList) {
            $app = $cb.Tag
            $Match = ($app.FriendlyName.ToLower().Contains($Filter)) -or ($app.Description.ToLower().Contains($Filter))
            $cb.Visibility = if ($Match) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        }
    })

    $script:DialogResultIds = $null

    $BtnConfirm.Add_Click({
        $Selected = @()
        foreach ($cb in $CheckBoxList) {
            if ($cb.IsChecked -eq $true) {
                $Selected += $cb.Tag.Id
            }
        }
        $script:DialogResultIds = $Selected
        $Window.Close()
    })

    $BtnCancel.Add_Click({
        $script:DialogResultIds = $null
        $Window.Close()
    })

    $Window.ShowDialog() | Out-Null

    return $script:DialogResultIds
}
