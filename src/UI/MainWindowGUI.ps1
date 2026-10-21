<#
.SYNOPSIS
    Interface Gráfica Principal (WPF Dark Mode com menu lateral).
.DESCRIPTION
    A janela apenas coleta as escolhas do usuário: toda otimização, ponto de restauração e reversão
    roda em segundo plano (ThreadingManager.ps1). Um temporizador lê a fila de eventos e atualiza
    log e barra de progresso, mantendo a janela responsiva. Durante a execução, os controles que
    poderiam disparar uma segunda operação ficam bloqueados.
#>

$script:MainWindowXaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Otimizador de Windows"
        Height="700" Width="1000" MinHeight="620" MinWidth="880"
        WindowStartupLocation="CenterScreen"
        Background="#121212" Foreground="#F0F0F0"
        FontFamily="Segoe UI">
    <Window.Resources>
        <!-- Estilo Botoes do Menu Lateral -->
        <Style x:Key="NavBtnStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="#C0C0C0"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="Padding" Value="16,12"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Margin" Value="0,2,0,2"/>
        </Style>

        <!-- Estilo Botoes Comuns -->
        <Style TargetType="Button">
            <Setter Property="Background" Value="#252525"/>
            <Setter Property="Foreground" Value="#FFFFFF"/>
            <Setter Property="BorderBrush" Value="#383838"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="FontSize" Value="12"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>

        <!-- Estilo CheckBox -->
        <Style TargetType="CheckBox">
            <Setter Property="Foreground" Value="#E0E0E0"/>
            <Setter Property="Margin" Value="4,6,4,6"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 1. HEADER TOPO -->
        <Border Grid.Row="0" Background="#181818" BorderBrush="#2A2A2A" BorderThickness="0,0,0,1" Padding="20,14">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="⚡ OTIMIZADOR DE WINDOWS" FontSize="19" FontWeight="Bold" Foreground="#00B4D8"/>
                        <Border Name="DryRunBadge" Background="#B7791F" CornerRadius="4" Margin="12,0,0,0" Padding="8,2" VerticalAlignment="Center" Visibility="Collapsed">
                            <TextBlock Text="SIMULAÇÃO: NADA SERÁ ALTERADO" FontSize="11" FontWeight="Bold" Foreground="#FFFFFF"/>
                        </Border>
                    </StackPanel>
                    <TextBlock Text="Debloat, privacidade e otimização com reversão por diário de alterações."
                               FontSize="12" Foreground="#8E8E8E" Margin="0,3,0,0"/>
                </StackPanel>

                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                    <TextBlock Name="HostInfoText" Text="" FontSize="12" Foreground="#00B4D8" TextAlignment="Right"/>
                    <TextBlock Name="UserInfoText" Text="" FontSize="11" Foreground="#7E7E7E" TextAlignment="Right"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- 2. CORPO (MENU LATERAL + CONTEÚDO) -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="220"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- MENU LATERAL (SIDEBAR) -->
            <Border Grid.Column="0" Background="#161616" BorderBrush="#242424" BorderThickness="0,0,1,0" Padding="8,12">
                <StackPanel>
                    <Button Name="NavBtnPresets" Style="{StaticResource NavBtnStyle}" Content="⚡ Perfis Rápidos (Presets)" Background="#222222" Foreground="#00B4D8"/>
                    <Button Name="NavBtnApps" Style="{StaticResource NavBtnStyle}" Content="📦 Aplicativos (Debloat)"/>
                    <Button Name="NavBtnPrivacy" Style="{StaticResource NavBtnStyle}" Content="🛡️ Privacidade &amp; IA"/>
                    <Button Name="NavBtnPerformance" Style="{StaticResource NavBtnStyle}" Content="⚙️ Performance &amp; Sistema"/>
                    <Button Name="NavBtnLogs" Style="{StaticResource NavBtnStyle}" Content="📝 Terminal de Logs"/>
                    <Separator Background="#2A2A2A" Margin="4,10"/>
                    <Button Name="NavBtnUndo" Style="{StaticResource NavBtnStyle}" Content="🔄 Restaurar (Undo)" Foreground="#F39C12"/>
                </StackPanel>
            </Border>

            <!-- CONTEÚDO PRINCIPAL (PAINÉIS ALTERNÁVEIS) -->
            <Grid Grid.Column="1" Margin="16,12,16,12">

                <!-- PAINEL 1: PRESETS -->
                <Grid Name="PanelPresets" Visibility="Visible">
                    <ScrollViewer VerticalScrollBarVisibility="Auto">
                        <StackPanel>
                            <TextBlock Text="Selecione um perfil pronto. Você pode revisar aplicativos e ajustes nas outras abas antes de executar."
                                       FontSize="13" Foreground="#B0B0B0" Margin="0,0,0,14" TextWrapping="Wrap"/>

                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>

                                <!-- Card Padrao -->
                                <Border Grid.Row="0" Grid.Column="0" Background="#1C1C1C" BorderBrush="#0077B6" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="🚀 PADRÃO (Equilibrado)" FontSize="15" FontWeight="Bold" Foreground="#00B4D8"/>
                                        <TextBlock Text="Recomendado para uso diário. Remove bloatwares comuns, desativa telemetria e ajusta interface e energia mantendo recursos essenciais."
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetPadrao" Content="Carregar Perfil Padrão" Background="#0077B6" BorderBrush="#0096C7"/>
                                    </StackPanel>
                                </Border>

                                <!-- Card Gamer -->
                                <Border Grid.Row="0" Grid.Column="1" Background="#1C1C1C" BorderBrush="#2ECC71" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="🎮 GAMER (Máximo FPS)" FontSize="15" FontWeight="Bold" Foreground="#2ECC71"/>
                                        <TextBlock Text="Preserva o ecossistema Xbox e Game Pass, desativa gravação de fundo (GameDVR) e aplica plano de energia Ultimate Performance (exceto em notebooks)."
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetGamer" Content="Carregar Perfil Gamer" Background="#27AE60" BorderBrush="#2ECC71"/>
                                    </StackPanel>
                                </Border>

                                <!-- Card Corporativo -->
                                <Border Grid.Row="1" Grid.Column="0" Background="#1C1C1C" BorderBrush="#9B59B6" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="💼 CORPORATIVO (Escritório)" FontSize="15" FontWeight="Bold" Foreground="#AF7AC5"/>
                                        <TextBlock Text="Foco em produtividade e privacidade. Preserva Teams, Outlook e OneDrive, remove jogos e bloqueia telemetrias invasivas."
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetCorp" Content="Carregar Perfil Corporativo" Background="#8E44AD" BorderBrush="#9B59B6"/>
                                    </StackPanel>
                                </Border>

                                <!-- Card Extremo -->
                                <Border Grid.Row="1" Grid.Column="1" Background="#1C1C1C" BorderBrush="#E74C3C" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="🔥 EXTREMO (Hardware Antigo)" FontSize="15" FontWeight="Bold" Foreground="#E74C3C"/>
                                        <TextBlock Text="Otimização agressiva para computadores lentos. Desativa SysMain/WSearch e remove o Microsoft Edge."
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetExtremo" Content="Carregar Perfil Extremo" Background="#C0392B" BorderBrush="#E74C3C"/>
                                    </StackPanel>
                                </Border>
                            </Grid>
                        </StackPanel>
                    </ScrollViewer>
                </Grid>

                <!-- PAINEL 2: APPS -->
                <Grid Name="PanelApps" Visibility="Collapsed">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <Grid Grid.Row="0" Margin="0,0,0,10">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBox Name="AppSearchBox" Grid.Column="0" Height="30" VerticalContentAlignment="Center"
                                 Background="#222222" Foreground="#FFFFFF" BorderBrush="#444444" Padding="8,2"
                                 FontSize="13" ToolTip="Digite para filtrar os aplicativos..."/>
                        <StackPanel Grid.Column="1" Orientation="Horizontal" Margin="8,0,0,0">
                            <Button Name="BtnAppsSelectDefault" Content="Padrão Recomendado" Background="#0077B6" Margin="2,0"/>
                            <Button Name="BtnAppsSelectAll" Content="Marcar Todos" Margin="2,0"/>
                            <Button Name="BtnAppsDeselectAll" Content="Desmarcar Todos" Margin="2,0"/>
                        </StackPanel>
                    </Grid>

                    <Border Grid.Row="1" Background="#1C1C1C" BorderBrush="#2C2C2C" BorderThickness="1" CornerRadius="4" Padding="10">
                        <ScrollViewer VerticalScrollBarVisibility="Auto">
                            <StackPanel Name="AppsContainerList"/>
                        </ScrollViewer>
                    </Border>
                </Grid>

                <!-- PAINEL 3: PRIVACIDADE & IA -->
                <Grid Name="PanelPrivacy" Visibility="Collapsed">
                    <Border Background="#1C1C1C" BorderBrush="#2C2C2C" BorderThickness="1" CornerRadius="4" Padding="12">
                        <ScrollViewer VerticalScrollBarVisibility="Auto">
                            <StackPanel Name="PrivacyTweaksContainer">
                                <TextBlock Text="Proteção de dados, bloqueio de telemetria e de recursos de Inteligência Artificial:"
                                           FontSize="13" Foreground="#B0B0B0" Margin="0,0,0,6"/>
                            </StackPanel>
                        </ScrollViewer>
                    </Border>
                </Grid>

                <!-- PAINEL 4: PERFORMANCE & SISTEMA -->
                <Grid Name="PanelPerformance" Visibility="Collapsed">
                    <Border Background="#1C1C1C" BorderBrush="#2C2C2C" BorderThickness="1" CornerRadius="4" Padding="12">
                        <ScrollViewer VerticalScrollBarVisibility="Auto">
                            <StackPanel Name="SystemTweaksContainer">
                                <TextBlock Text="Ajustes de energia, serviços, interface, jogos e limpeza:"
                                           FontSize="13" Foreground="#B0B0B0" Margin="0,0,0,6"/>
                            </StackPanel>
                        </ScrollViewer>
                    </Border>
                </Grid>

                <!-- PAINEL 5: LOGS -->
                <Grid Name="PanelLogs" Visibility="Collapsed">
                    <Grid.RowDefinitions>
                        <RowDefinition Height="*"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>
                    <TextBox Name="LogOutputBox" Grid.Row="0" Background="#0D0D0D" Foreground="#00FF66"
                             FontFamily="Consolas" FontSize="12" IsReadOnly="True" VerticalScrollBarVisibility="Auto"
                             TextWrapping="Wrap" Padding="10"/>
                    <Button Name="BtnOpenLogsFolder" Grid.Row="1" Content="📁 Abrir Pasta de Logs" Margin="0,8,0,0" HorizontalAlignment="Right"/>
                </Grid>

                <!-- PAINEL 6: UNDO -->
                <Grid Name="PanelUndo" Visibility="Collapsed">
                    <Border Background="#1C1C1C" BorderBrush="#F39C12" BorderThickness="1" CornerRadius="6" Padding="20" VerticalAlignment="Top">
                        <StackPanel>
                            <TextBlock Text="🔄 Restauração e Reversão do Sistema" FontSize="18" FontWeight="Bold" Foreground="#F39C12"/>
                            <TextBlock Text="Cada execução grava um diário com os valores originais de registro, serviços, tarefas agendadas e plano de energia. A restauração devolve tudo ao estado exato anterior, da execução mais recente para a mais antiga. Aplicativos removidos e o Microsoft Edge não são reinstalados (use a Microsoft Store)."
                                       FontSize="13" Foreground="#C0C0C0" TextWrapping="Wrap" Margin="0,10,0,20"/>
                            <Button Name="BtnUndoAction" Content="Restaurar Configurações Anteriores Agora" Background="#D35400" BorderBrush="#E67E22"
                                    FontSize="14" Padding="18,10" HorizontalAlignment="Left"/>
                        </StackPanel>
                    </Border>
                </Grid>
            </Grid>
        </Grid>

        <!-- 3. PAINEL INFERIOR DE AÇÕES -->
        <Border Grid.Row="2" Background="#181818" BorderBrush="#2A2A2A" BorderThickness="0,1,0,0" Padding="16,12">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <CheckBox Name="ChkRestorePoint" Grid.Column="0" Content="Criar Ponto de Restauração antes de aplicar"
                          IsChecked="True" VerticalAlignment="Center" Foreground="#00B4D8" FontWeight="SemiBold"/>

                <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="14,0">
                    <ProgressBar Name="GlobalProgressBar" Height="4" Background="#222222" Foreground="#00B4D8" BorderThickness="0" Visibility="Collapsed"/>
                    <TextBlock Name="StatusBarText" Text="Pronto para otimizar."
                               Foreground="#8E8E8E" FontSize="12" TextAlignment="Center" Margin="0,4,0,0" TextTrimming="CharacterEllipsis"/>
                </StackPanel>

                <StackPanel Grid.Column="2" Orientation="Horizontal">
                    <Button Name="BtnCloseWindow" Content="Fechar" Width="85" Margin="0,0,8,0"/>
                    <Button Name="BtnApplyOptimization" Content="⚡ EXECUTAR OTIMIZAÇÃO"
                            Background="#27AE60" BorderBrush="#2ECC71" FontWeight="Bold" FontSize="13" Padding="16,8"/>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

$script:MainWindowControlNames = @(
    'DryRunBadge', 'HostInfoText', 'UserInfoText',
    'NavBtnPresets', 'NavBtnApps', 'NavBtnPrivacy', 'NavBtnPerformance', 'NavBtnLogs', 'NavBtnUndo',
    'PanelPresets', 'PanelApps', 'PanelPrivacy', 'PanelPerformance', 'PanelLogs', 'PanelUndo',
    'AppsContainerList', 'PrivacyTweaksContainer', 'SystemTweaksContainer', 'AppSearchBox',
    'BtnAppsSelectDefault', 'BtnAppsSelectAll', 'BtnAppsDeselectAll',
    'BtnPresetPadrao', 'BtnPresetGamer', 'BtnPresetCorp', 'BtnPresetExtremo',
    'BtnUndoAction', 'BtnApplyOptimization', 'BtnCloseWindow', 'BtnOpenLogsFolder',
    'ChkRestorePoint', 'StatusBarText', 'LogOutputBox', 'GlobalProgressBar'
)

# Categorias de ajustes exibidas no painel "Privacidade & IA"; as demais vão para "Performance & Sistema"
$script:PrivacyPanelCategories = @('Privacy', 'AI')

function New-MainWindow {
    <#
    .SYNOPSIS
        Carrega o XAML e devolve a janela com o mapa de controles nomeados (sem exibi-la).
    #>
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase | Out-Null

    $Reader = New-Object System.Xml.XmlNodeReader ([xml]$script:MainWindowXaml)
    $Window = [System.Windows.Markup.XamlReader]::Load($Reader)

    $Controls = @{}
    foreach ($Name in $script:MainWindowControlNames) {
        $Control = $Window.FindName($Name)
        if ($null -eq $Control) { throw "Controle '$Name' não encontrado no XAML da janela principal." }
        $Controls[$Name] = $Control
    }
    return [pscustomobject]@{ Window = $Window; Controls = $Controls }
}

function ConvertTo-Brush {
    param([Parameter(Mandatory)][string]$Hex)
    return [System.Windows.Media.BrushConverter]::new().ConvertFromString($Hex)
}

function Show-MainOptimizerWindow {
    param(
        [Parameter(Mandatory)]$Catalog,
        [Parameter(Mandatory)][hashtable]$Context,
        [Parameter(Mandatory)][string]$ScriptDir,
        [Parameter(Mandatory)][string]$LogFilePath,
        [string]$OSCaption = 'Windows',
        [switch]$DryRun
    )

    $Ui = New-MainWindow
    $Window = $Ui.Window
    $C = $Ui.Controls
    $IsDryRun = [bool]$DryRun

    # Estado compartilhado pelos eventos (alterado por propriedade, pois cada evento tem seu próprio escopo)
    $State = @{ Task = $null; Busy = $false; OnComplete = $null; PendingOptimize = $null; Timer = $null }

    # A versão fica só no log (controle interno); a janela mostra apenas o nome do produto
    $Window.Title = "Otimizador de Windows$(if ($IsDryRun) { ' [SIMULAÇÃO]' })"
    if ($IsDryRun) { $C.DryRunBadge.Visibility = [System.Windows.Visibility]::Visible }
    $C.HostInfoText.Text = "Sistema: $OSCaption"
    $C.UserInfoText.Text = "Usuário: $env:USERNAME | PC: $env:COMPUTERNAME | $(if ($Context.IsPortable) { 'Notebook' } else { 'Desktop' })"

    #region Log e status

    $AppendLogLine = {
        param([string]$Level, [string]$Message)
        $C.LogOutputBox.AppendText("[$(Get-Date -Format 'HH:mm:ss')] [$Level] $Message`r`n")
        $C.LogOutputBox.ScrollToEnd()
    }
    # Mensagens geradas na própria janela vão para o arquivo de log e para o painel
    Set-OptimizerLogTarget -FilePath $LogFilePath -Sink $AppendLogLine

    $SetStatus = {
        param([string]$Text)
        $C.StatusBarText.Text = $Text
    }

    #endregion

    #region Navegação

    $AllNavButtons = @($C.NavBtnPresets, $C.NavBtnApps, $C.NavBtnPrivacy, $C.NavBtnPerformance, $C.NavBtnLogs, $C.NavBtnUndo)
    $AllPanels = @($C.PanelPresets, $C.PanelApps, $C.PanelPrivacy, $C.PanelPerformance, $C.PanelLogs, $C.PanelUndo)

    $SwitchTab = {
        param($TargetBtn, $TargetPanel)
        foreach ($Btn in $AllNavButtons) {
            $Btn.Background = [System.Windows.Media.Brushes]::Transparent
            $Btn.Foreground = ConvertTo-Brush '#C0C0C0'
        }
        foreach ($Panel in $AllPanels) { $Panel.Visibility = [System.Windows.Visibility]::Collapsed }
        $TargetBtn.Background = ConvertTo-Brush '#222222'
        $TargetBtn.Foreground = ConvertTo-Brush '#00B4D8'
        $TargetPanel.Visibility = [System.Windows.Visibility]::Visible
    }

    $C.NavBtnPresets.Add_Click({ & $SwitchTab $C.NavBtnPresets $C.PanelPresets })
    $C.NavBtnApps.Add_Click({ & $SwitchTab $C.NavBtnApps $C.PanelApps })
    $C.NavBtnPrivacy.Add_Click({ & $SwitchTab $C.NavBtnPrivacy $C.PanelPrivacy })
    $C.NavBtnPerformance.Add_Click({ & $SwitchTab $C.NavBtnPerformance $C.PanelPerformance })
    $C.NavBtnLogs.Add_Click({ & $SwitchTab $C.NavBtnLogs $C.PanelLogs })
    $C.NavBtnUndo.Add_Click({ & $SwitchTab $C.NavBtnUndo $C.PanelUndo })

    #endregion

    #region Listas de aplicativos e ajustes

    $AppCheckBoxes = [System.Collections.Generic.List[System.Windows.Controls.CheckBox]]::new()
    $TweakCheckBoxes = [System.Collections.Generic.List[System.Windows.Controls.CheckBox]]::new()

    $NewHeader = {
        param([string]$Text)
        $Header = New-Object System.Windows.Controls.TextBlock
        $Header.Text = $Text
        $Header.FontSize = 14
        $Header.FontWeight = [System.Windows.FontWeights]::Bold
        $Header.Foreground = ConvertTo-Brush '#00B4D8'
        $Header.Margin = New-Object System.Windows.Thickness(4, 10, 4, 4)
        return $Header
    }

    $AppCategoryNames = @{
        'Gaming'          = '🎮 Jogos & Xbox'
        'AI'              = '🤖 Inteligência Artificial & Copilot'
        'Productivity'    = '💼 Produtividade & Office'
        'Communication'   = '💬 Comunicação & Mensagens'
        'Media'           = '🎬 Mídia, Vídeo & Áudio'
        'Utilities'       = '🛠️ Utilitários & Notícias'
        'ThirdPartyBloat' = '📦 Bloatware Pré-instalado (OEM / Patrocinados)'
    }

    foreach ($Group in ($Catalog.Apps | Group-Object -Property Category)) {
        $Title = if ($AppCategoryNames.ContainsKey($Group.Name)) { $AppCategoryNames[$Group.Name] } else { $Group.Name }
        $C.AppsContainerList.Children.Add((& $NewHeader $Title)) | Out-Null

        foreach ($App in $Group.Group) {
            $Cb = New-Object System.Windows.Controls.CheckBox
            $Cb.Content = "$($App.FriendlyName)"
            $Cb.Tag = $App
            $Cb.ToolTip = "$($App.Description)`nImpacto: $(if ($App.Recommendation -eq 'caution') { 'Requer cautela' } else { 'Seguro' })"
            $Cb.IsChecked = [bool]$App.SelectedByDefault
            if ($App.Recommendation -eq 'caution') { $Cb.Foreground = ConvertTo-Brush '#F39C12' }
            $AppCheckBoxes.Add($Cb)
            $C.AppsContainerList.Children.Add($Cb) | Out-Null
        }
    }

    $PortableNotes = @{
        'DisableFastStartup'            = 'Notebook detectado: apenas a Inicialização Rápida será desligada; a hibernação é mantida.'
        'InjectUltimatePerformancePlan' = 'Notebook detectado: este ajuste será ignorado para preservar a bateria.'
    }

    $CategoryIds = @($Catalog.Categories | ForEach-Object { $_.Id })
    $OrderedCategories = @($Catalog.Categories) + @(
        $Catalog.Tweaks | Where-Object { $CategoryIds -notcontains $_.Category } |
            Select-Object -ExpandProperty Category -Unique | ForEach-Object { [pscustomobject]@{ Id = $_; Name = $_ } }
    )

    foreach ($Category in $OrderedCategories) {
        $Tweaks = @($Catalog.Tweaks | Where-Object { $_.Category -eq $Category.Id })
        if ($Tweaks.Count -eq 0) { continue }
        $Container = if ($script:PrivacyPanelCategories -contains $Category.Id) { $C.PrivacyTweaksContainer } else { $C.SystemTweaksContainer }
        $Container.Children.Add((& $NewHeader $Category.Name)) | Out-Null

        foreach ($Tweak in $Tweaks) {
            $Cb = New-Object System.Windows.Controls.CheckBox
            $Cb.Content = "$($Tweak.Name)"
            $Cb.Tag = $Tweak
            $Tip = "$($Tweak.Description)"
            if ($Context.IsPortable -and $PortableNotes.ContainsKey($Tweak.Id)) { $Tip += "`n$($PortableNotes[$Tweak.Id])" }
            $Cb.ToolTip = $Tip
            $Cb.IsChecked = [bool]$Tweak.SelectedByDefault
            $TweakCheckBoxes.Add($Cb)
            $Container.Children.Add($Cb) | Out-Null
        }
    }

    $C.AppSearchBox.Add_TextChanged({
        $Filter = $C.AppSearchBox.Text.ToLower().Trim()
        foreach ($Cb in $AppCheckBoxes) {
            $App = $Cb.Tag
            $Match = ($App.FriendlyName.ToLower().Contains($Filter)) -or ($App.Description.ToLower().Contains($Filter))
            $Cb.Visibility = if ($Match) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        }
    })

    $C.BtnAppsSelectDefault.Add_Click({
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = [bool]$Cb.Tag.SelectedByDefault }
        & $SetStatus 'Seleção padrão de aplicativos carregada.'
    })
    $C.BtnAppsSelectAll.Add_Click({
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = $true }
        & $SetStatus 'Todos os aplicativos marcados para remoção.'
    })
    $C.BtnAppsDeselectAll.Add_Click({
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = $false }
        & $SetStatus 'Todos os aplicativos desmarcados.'
    })

    #endregion

    #region Perfis

    $LoadPreset = {
        param([string]$PresetName)
        try {
            $PresetJson = Import-OptimizerPreset -Catalog $Catalog -NameOrPath $PresetName
        } catch {
            Write-OptimizerLog -Level ERRO -Message $_.Exception.Message
            [System.Windows.MessageBox]::Show($_.Exception.Message, 'Perfil inválido', 'OK', 'Error') | Out-Null
            return
        }
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = (@($PresetJson.RemoveApps) -contains $Cb.Tag.Id) }
        foreach ($Cb in $TweakCheckBoxes) { $Cb.IsChecked = (@($PresetJson.ApplyTweaks) -contains $Cb.Tag.Id) }
        & $SetStatus "Perfil '$($PresetJson.DisplayName)' carregado: $(@($PresetJson.RemoveApps).Count) aplicativos e $(@($PresetJson.ApplyTweaks).Count) ajustes marcados. Revise as abas e clique em Executar."
        Write-OptimizerLog -Level INFO -Message "Perfil '$($PresetJson.DisplayName)' carregado na interface."
    }

    $C.BtnPresetPadrao.Add_Click({ & $LoadPreset 'Padrao' })
    $C.BtnPresetGamer.Add_Click({ & $LoadPreset 'Gamer' })
    $C.BtnPresetCorp.Add_Click({ & $LoadPreset 'Corporativo' })
    $C.BtnPresetExtremo.Add_Click({ & $LoadPreset 'Extremo' })

    #endregion

    #region Execução em segundo plano

    $LockableControls = @(
        $C.BtnPresetPadrao, $C.BtnPresetGamer, $C.BtnPresetCorp, $C.BtnPresetExtremo,
        $C.BtnAppsSelectDefault, $C.BtnAppsSelectAll, $C.BtnAppsDeselectAll, $C.AppSearchBox,
        $C.AppsContainerList, $C.PrivacyTweaksContainer, $C.SystemTweaksContainer,
        $C.ChkRestorePoint, $C.BtnUndoAction, $C.BtnApplyOptimization, $C.BtnCloseWindow
    )

    $SetBusy = {
        param([bool]$Busy)
        $State.Busy = $Busy
        foreach ($Ctl in $LockableControls) { $Ctl.IsEnabled = -not $Busy }
        $C.GlobalProgressBar.Visibility = if ($Busy) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        if ($Busy) { $C.GlobalProgressBar.IsIndeterminate = $true }
    }

    $ProcessQueue = {
        foreach ($QueueItem in (Receive-OptimizerBackgroundEvents -Task $State.Task)) {
            if ($QueueItem.Type -eq 'Log') {
                # O runspace já gravou a linha no arquivo: aqui só exibe no painel
                $C.LogOutputBox.AppendText("[$($QueueItem.Time.ToString('HH:mm:ss'))] [$($QueueItem.Level)] $($QueueItem.Message)`r`n")
                $C.LogOutputBox.ScrollToEnd()
            } elseif ($QueueItem.Type -eq 'Progress' -and $QueueItem.Total -gt 0) {
                $C.GlobalProgressBar.IsIndeterminate = $false
                $C.GlobalProgressBar.Maximum = $QueueItem.Total
                $C.GlobalProgressBar.Value = $QueueItem.Current
                & $SetStatus "$($QueueItem.Activity) ($($QueueItem.Current)/$($QueueItem.Total))"
            }
        }
    }

    $State.Timer = New-Object System.Windows.Threading.DispatcherTimer
    $State.Timer.Interval = [TimeSpan]::FromMilliseconds(120)
    $State.Timer.Add_Tick({
        if ($null -eq $State.Task) { $State.Timer.Stop(); return }
        & $ProcessQueue
        if (-not (Test-OptimizerBackgroundTaskCompleted -Task $State.Task)) { return }

        $State.Timer.Stop()
        & $ProcessQueue
        $Completion = Complete-OptimizerBackgroundTask -Task $State.Task
        $State.Task = $null
        & $SetBusy $false
        foreach ($ErrText in $Completion.Errors) { Write-OptimizerLog -Level ERRO -Message $ErrText }
        & $State.OnComplete $Completion.Result
    })

    $StartTask = {
        param([string]$Operation, [hashtable]$Arguments, [scriptblock]$OnComplete)
        & $SetBusy $true
        & $SwitchTab $C.NavBtnLogs $C.PanelLogs
        $State.OnComplete = $OnComplete
        $State.Task = Start-OptimizerBackgroundTask -ScriptDir $ScriptDir -LogFilePath $LogFilePath -Operation $Operation -Arguments $Arguments
        $State.Timer.Start()
    }

    $OnOptimizeComplete = {
        param($Result)
        if ($null -eq $Result -or $Result.PSObject.Properties['Unexpected']) {
            & $SetStatus 'A otimização foi interrompida por um erro inesperado.'
            [System.Windows.MessageBox]::Show("A otimização foi interrompida por um erro inesperado.`n`nConsulte o log: $LogFilePath", 'Erro', 'OK', 'Error') | Out-Null
            return
        }
        if ($Result.DryRun) {
            $Text = "Simulação concluída: $($Result.Simulated) etapas seriam executadas. Nada foi alterado."
            $Icon = 'Information'
        } else {
            $Text = "Otimização concluída: $($Result.Ok) OK, $($Result.Warnings) com aviso, $($Result.Failed) com erro."
            $Icon = if ($Result.Failed -gt 0) { 'Warning' } else { 'Information' }
        }
        & $SetStatus $Text
        [System.Windows.MessageBox]::Show("$Text`n`nLog detalhado: $LogFilePath", 'Otimização', 'OK', $Icon) | Out-Null
    }

    $OnRestorePointComplete = {
        param($Result)
        if ($null -ne $Result -and $Result.Success) {
            & $StartTask 'Optimize' $State.PendingOptimize $OnOptimizeComplete
            return
        }
        $Reason = if ($null -ne $Result) { $Result.Message } else { 'erro desconhecido' }
        $Answer = [System.Windows.MessageBox]::Show("Não foi possível criar o Ponto de Restauração:`n$Reason`n`nDeseja continuar SEM ponto de restauração?", 'Ponto de Restauração', 'YesNo', 'Warning')
        if ($Answer -eq [System.Windows.MessageBoxResult]::Yes) {
            Write-OptimizerLog -Level AVISO -Message 'Continuando sem Ponto de Restauração por decisão do usuário.'
            & $StartTask 'Optimize' $State.PendingOptimize $OnOptimizeComplete
        } else {
            Write-OptimizerLog -Level INFO -Message 'Otimização cancelada pelo usuário. Nada foi alterado.'
            & $SetStatus 'Otimização cancelada (sem ponto de restauração).'
        }
    }

    $OnUndoComplete = {
        param($Result)
        if ($null -eq $Result -or $Result.PSObject.Properties['Unexpected']) {
            [System.Windows.MessageBox]::Show("A restauração foi interrompida por um erro inesperado.`n`nConsulte o log: $LogFilePath", 'Erro', 'OK', 'Error') | Out-Null
            return
        }
        $Text = "Restauração concluída: $($Result.Restored) itens revertidos, $($Result.Failed) com erro."
        if ($Result.Mode -eq 'Padroes') { $Text += "`nNenhum diário encontrado: foram restaurados os padrões do Windows." }
        & $SetStatus ($Text -split "`n")[0]
        $Icon = if ($Result.Failed -gt 0) { 'Warning' } else { 'Information' }
        [System.Windows.MessageBox]::Show("$Text`n`nLog detalhado: $LogFilePath", 'Restauração', 'OK', $Icon) | Out-Null
    }

    $C.BtnApplyOptimization.Add_Click({
        $AppIds = @($AppCheckBoxes | Where-Object { $_.IsChecked -eq $true } | ForEach-Object { $_.Tag.Id })
        $TweakIds = @($TweakCheckBoxes | Where-Object { $_.IsChecked -eq $true } | ForEach-Object { $_.Tag.Id })
        if ($AppIds.Count -eq 0 -and $TweakIds.Count -eq 0) {
            [System.Windows.MessageBox]::Show('Nenhum aplicativo ou ajuste selecionado.', 'Nada a fazer', 'OK', 'Information') | Out-Null
            return
        }

        $Question = "Serão removidos $($AppIds.Count) aplicativo(s) e aplicados $($TweakIds.Count) ajuste(s)."
        if ($IsDryRun) { $Question += "`n`nMODO SIMULAÇÃO: nada será alterado." }
        $Answer = [System.Windows.MessageBox]::Show("$Question`n`nDeseja continuar?", 'Confirmar Otimização', 'YesNo', 'Question')
        if ($Answer -ne [System.Windows.MessageBoxResult]::Yes) { return }

        $State.PendingOptimize = @{ AppIds = $AppIds; TweakIds = $TweakIds; Context = $Context; DryRun = $IsDryRun }
        if ($C.ChkRestorePoint.IsChecked -eq $true -and -not $IsDryRun) {
            & $StartTask 'RestorePoint' @{ Description = (New-RestorePointDescription -Label 'GUI') } $OnRestorePointComplete
        } else {
            if (-not $IsDryRun) { Write-OptimizerLog -Level AVISO -Message 'Ponto de Restauração desmarcado pelo usuário.' }
            & $StartTask 'Optimize' $State.PendingOptimize $OnOptimizeComplete
        }
    })

    $C.BtnUndoAction.Add_Click({
        $Answer = [System.Windows.MessageBox]::Show('Deseja restaurar as configurações anteriores às otimizações?', 'Confirmar Restauração', 'YesNo', 'Question')
        if ($Answer -ne [System.Windows.MessageBoxResult]::Yes) { return }
        $UndoArgs = @{ JournalDirectory = $Context.JournalDirectory; IsWin11 = $Context.IsWin11; UserSid = $Context.UserSid; DryRun = $IsDryRun }
        & $StartTask 'Undo' $UndoArgs $OnUndoComplete
    })

    #endregion

    #region Janela

    $C.BtnOpenLogsFolder.Add_Click({
        Start-Process explorer.exe -ArgumentList "`"$(Split-Path -Parent $LogFilePath)`""
    })

    $C.BtnCloseWindow.Add_Click({ $Window.Close() })

    $Window.Add_Closing({
        param($WindowSender, $CloseArgs)
        if ($State.Busy) {
            $CloseArgs.Cancel = $true
            [System.Windows.MessageBox]::Show('Aguarde o término da operação em andamento antes de fechar.', 'Operação em andamento', 'OK', 'Warning') | Out-Null
        }
    })

    #endregion

    Write-OptimizerLog -Level INFO -Message "Janela aberta. Log desta sessão: $LogFilePath"
    $Window.ShowDialog() | Out-Null
}
