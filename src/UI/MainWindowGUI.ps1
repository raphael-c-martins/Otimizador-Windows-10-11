<#
.SYNOPSIS
    Interface Gráfica Principal Moderna (Sidebar Fluent Design + Execução Fluida em Tempo Real).
.DESCRIPTION
    Apresenta uma interface moderna em Dark Mode com navegação por menu lateral estilo Windows 11,
    execução com bombeamento de UI contínuo e feedback de logs em tempo real com barra de progresso precisa.
#>

function Show-MainOptimizerWindow {
    param(
        [array]$AllApps,
        [array]$AllTweaks,
        [string]$CurrentOS,
        [string]$ScriptDir
    )

    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms | Out-Null

    [string]$Xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="⚡ Otimizador de Windows (v13.0 Modular)"
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
                        <Border Background="#0077B6" CornerRadius="4" Margin="12,0,0,0" Padding="8,2" VerticalAlignment="Center">
                            <TextBlock Text="v13.0 MODULAR" FontSize="11" FontWeight="Bold" Foreground="#FFFFFF"/>
                        </Border>
                    </StackPanel>
                    <TextBlock Text="Suíte profissional de debloat, privacidade e otimização de alta performance." 
                               FontSize="12" Foreground="#8E8E8E" Margin="0,3,0,0"/>
                </StackPanel>

                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                    <TextBlock Name="HostInfoText" Text="Host: Windows 11" FontSize="12" Foreground="#00B4D8" TextAlignment="Right"/>
                    <TextBlock Name="UserInfoText" Text="Admin: Raphael.martins" FontSize="11" Foreground="#7E7E7E" TextAlignment="Right"/>
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
                            <TextBlock Text="Selecione um perfil pronto para aplicar a combinação ideal de configurações:" 
                                       FontSize="13" Foreground="#B0B0B0" Margin="0,0,0,14"/>

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
                                        <TextBlock Text="Recomendado para uso diário. Remove bloatwares comuns, desativa telemetria e otimiza CPU/RAM mantendo recursos essenciais intactos." 
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetPadrao" Content="Carregar Perfil Padrão" Background="#0077B6" BorderBrush="#0096C7"/>
                                    </StackPanel>
                                </Border>

                                <!-- Card Gamer -->
                                <Border Grid.Row="0" Grid.Column="1" Background="#1C1C1C" BorderBrush="#2ECC71" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="🎮 GAMER (Máximo FPS)" FontSize="15" FontWeight="Bold" Foreground="#2ECC71"/>
                                        <TextBlock Text="Preserva o ecossistema Xbox e Game Pass, desativa gravação de fundo (GameDVR) e aplica plano de energia Ultimate Performance." 
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetGamer" Content="Carregar Perfil Gamer" Background="#27AE60" BorderBrush="#2ECC71"/>
                                    </StackPanel>
                                </Border>

                                <!-- Card Corporativo -->
                                <Border Grid.Row="1" Grid.Column="0" Background="#1C1C1C" BorderBrush="#9B59B6" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="💼 CORPORATIVO (Escritório)" FontSize="15" FontWeight="Bold" Foreground="#AF7AC5"/>
                                        <TextBlock Text="Foco em produtividade e privacidade corporativa. Preserva Microsoft Teams e Office, bloqueia jogos e telemetrias invasivas." 
                                                   FontSize="12" Foreground="#A0A0A0" TextWrapping="Wrap" Margin="0,6,0,12"/>
                                        <Button Name="BtnPresetCorp" Content="Carregar Perfil Corporativo" Background="#8E44AD" BorderBrush="#9B59B6"/>
                                    </StackPanel>
                                </Border>

                                <!-- Card Extremo -->
                                <Border Grid.Row="1" Grid.Column="1" Background="#1C1C1C" BorderBrush="#E74C3C" BorderThickness="2" CornerRadius="6" Margin="6" Padding="14">
                                    <StackPanel>
                                        <TextBlock Text="🔥 EXTREMO (Hardware Antigo)" FontSize="15" FontWeight="Bold" Foreground="#E74C3C"/>
                                        <TextBlock Text="Otimização agressiva para computadores lentos. Desativa SysMain/WSearch, remove o Microsoft Edge e bloqueia todos os extras." 
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
                                <TextBlock Text="Diretivas de Proteção de Dados, Bloqueio de Telemetria e Inteligência Artificial Local:" 
                                           FontSize="13" Foreground="#B0B0B0" Margin="0,0,0,12"/>
                            </StackPanel>
                        </ScrollViewer>
                    </Border>
                </Grid>

                <!-- PAINEL 4: PERFORMANCE & SISTEMA -->
                <Grid Name="PanelPerformance" Visibility="Collapsed">
                    <Border Background="#1C1C1C" BorderBrush="#2C2C2C" BorderThickness="1" CornerRadius="4" Padding="12">
                        <ScrollViewer VerticalScrollBarVisibility="Auto">
                            <StackPanel Name="SystemTweaksContainer">
                                <TextBlock Text="Otimizações de Baixo Nível de CPU, I/O, Energia e Interface Gráfica:" 
                                           FontSize="13" Foreground="#B0B0B0" Margin="0,0,0,12"/>
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
                    <Button Name="BtnOpenLogsFolder" Grid.Row="1" Content="📁 Abrir Pasta Física de Logs" Margin="0,8,0,0" HorizontalAlignment="Right"/>
                </Grid>

                <!-- PAINEL 6: UNDO -->
                <Grid Name="PanelUndo" Visibility="Collapsed">
                    <Border Background="#1C1C1C" BorderBrush="#F39C12" BorderThickness="1" CornerRadius="6" Padding="20" VerticalAlignment="Top">
                        <StackPanel>
                            <TextBlock Text="🔄 Restauração e Reversão do Sistema" FontSize="18" FontWeight="Bold" Foreground="#F39C12"/>
                            <TextBlock Text="Caso você necessite desfazer as modificações aplicadas, utilize o botão abaixo. Isso reativará os serviços de indexação, Superfetch (SysMain), o menu de contexto original do Windows 11 e os manipuladores de jogos." 
                                       FontSize="13" Foreground="#C0C0C0" TextWrapping="Wrap" Margin="0,10,0,20"/>
                            <Button Name="BtnUndoAction" Content="Restaurar Configurações Padrão Agora" Background="#D35400" BorderBrush="#E67E22" 
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

                <!-- Checkbox Ponto de Restauração -->
                <CheckBox Name="ChkRestorePoint" Grid.Column="0" Content="Criar Ponto de Restauração antes de aplicar" 
                          IsChecked="True" VerticalAlignment="Center" Foreground="#00B4D8" FontWeight="SemiBold"/>

                <!-- Status de Seleção e Progresso -->
                <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="14,0">
                    <ProgressBar Name="GlobalProgressBar" Height="4" Background="#222222" Foreground="#00B4D8" BorderThickness="0" Visibility="Collapsed"/>
                    <TextBlock Name="StatusBarText" Text="Pronto para otimizar." 
                               Foreground="#8E8E8E" FontSize="12" TextAlignment="Center" Margin="0,4,0,0"/>
                </StackPanel>

                <!-- Botões de Ação -->
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

    $XmlDoc = [xml]$Xaml
    $Reader = (New-Object System.Xml.XmlNodeReader $XmlDoc)
    $Window = [System.Windows.Markup.XamlReader]::Load($Reader)

    # Identificar Controles
    $HostInfoText = $Window.FindName("HostInfoText")
    $UserInfoText = $Window.FindName("UserInfoText")
    
    # Navegação
    $NavBtnPresets = $Window.FindName("NavBtnPresets")
    $NavBtnApps = $Window.FindName("NavBtnApps")
    $NavBtnPrivacy = $Window.FindName("NavBtnPrivacy")
    $NavBtnPerformance = $Window.FindName("NavBtnPerformance")
    $NavBtnLogs = $Window.FindName("NavBtnLogs")
    $NavBtnUndo = $Window.FindName("NavBtnUndo")

    # Painéis
    $PanelPresets = $Window.FindName("PanelPresets")
    $PanelApps = $Window.FindName("PanelApps")
    $PanelPrivacy = $Window.FindName("PanelPrivacy")
    $PanelPerformance = $Window.FindName("PanelPerformance")
    $PanelLogs = $Window.FindName("PanelLogs")
    $PanelUndo = $Window.FindName("PanelUndo")

    # Containers
    $AppsContainerList = $Window.FindName("AppsContainerList")
    $PrivacyTweaksContainer = $Window.FindName("PrivacyTweaksContainer")
    $SystemTweaksContainer = $Window.FindName("SystemTweaksContainer")
    $AppSearchBox = $Window.FindName("AppSearchBox")
    $BtnAppsSelectDefault = $Window.FindName("BtnAppsSelectDefault")
    $BtnAppsSelectAll = $Window.FindName("BtnAppsSelectAll")
    $BtnAppsDeselectAll = $Window.FindName("BtnAppsDeselectAll")
    $BtnPresetPadrao = $Window.FindName("BtnPresetPadrao")
    $BtnPresetGamer = $Window.FindName("BtnPresetGamer")
    $BtnPresetCorp = $Window.FindName("BtnPresetCorp")
    $BtnPresetExtremo = $Window.FindName("BtnPresetExtremo")
    $BtnUndoAction = $Window.FindName("BtnUndoAction")
    $BtnApplyOptimization = $Window.FindName("BtnApplyOptimization")
    $BtnCloseWindow = $Window.FindName("BtnCloseWindow")
    $BtnOpenLogsFolder = $Window.FindName("BtnOpenLogsFolder")
    $ChkRestorePoint = $Window.FindName("ChkRestorePoint")
    $StatusBarText = $Window.FindName("StatusBarText")
    $LogOutputBox = $Window.FindName("LogOutputBox")
    $GlobalProgressBar = $Window.FindName("GlobalProgressBar")

    $HostInfoText.Text = "Host: $CurrentOS"
    $UserInfoText.Text = "SysAdmin: $env:USERNAME | PC: $env:COMPUTERNAME"

    # Lógica de Troca de Abas da Sidebar
    $AllNavButtons = @($NavBtnPresets, $NavBtnApps, $NavBtnPrivacy, $NavBtnPerformance, $NavBtnLogs, $NavBtnUndo)
    $AllPanels = @($PanelPresets, $PanelApps, $PanelPrivacy, $PanelPerformance, $PanelLogs, $PanelUndo)

    $SwitchTab = {
        param($TargetBtn, $TargetPanel)
        foreach ($btn in $AllNavButtons) {
            $btn.Background = [System.Windows.Media.Brushes]::Transparent
            $btn.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#C0C0C0")
        }
        foreach ($panel in $AllPanels) {
            $panel.Visibility = [System.Windows.Visibility]::Collapsed
        }
        $TargetBtn.Background = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#222222")
        $TargetBtn.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#00B4D8")
        $TargetPanel.Visibility = [System.Windows.Visibility]::Visible
    }

    $NavBtnPresets.Add_Click({ & $SwitchTab $NavBtnPresets $PanelPresets })
    $NavBtnApps.Add_Click({ & $SwitchTab $NavBtnApps $PanelApps })
    $NavBtnPrivacy.Add_Click({ & $SwitchTab $NavBtnPrivacy $PanelPrivacy })
    $NavBtnPerformance.Add_Click({ & $SwitchTab $NavBtnPerformance $PanelPerformance })
    $NavBtnLogs.Add_Click({ & $SwitchTab $NavBtnLogs $PanelLogs })
    $NavBtnUndo.Add_Click({ & $SwitchTab $NavBtnUndo $PanelUndo })

    # Mapeamento de Checkboxes
    $AppCheckBoxes = [System.Collections.Generic.List[System.Windows.Controls.CheckBox]]::new()
    $TweakCheckBoxes = [System.Collections.Generic.Dictionary[string, System.Windows.Controls.CheckBox]]::new()

    $CatDisplayNames = @{
        "Gaming"          = "🎮 Jogos & Xbox"
        "AI"              = "🤖 Inteligência Artificial & Copilot"
        "Productivity"    = "💼 Produtividade & Office"
        "Communication"   = "💬 Comunicação & Mensagens"
        "Media"           = "🎬 Mídia, Vídeo & Áudio"
        "Utilities"       = "🛠️ Utilitários & Notícias"
        "ThirdPartyBloat" = "📦 Bloatware Pré-instalado (OEM / Patrocinados)"
    }

    # 1. Renderizar Apps
    $GroupedApps = $AllApps | Group-Object -Property Category
    foreach ($Group in $GroupedApps) {
        $HeaderTitle = if ($CatDisplayNames.ContainsKey($Group.Name)) { $CatDisplayNames[$Group.Name] } else { $Group.Name }

        $HeaderBlock = New-Object System.Windows.Controls.TextBlock
        $HeaderBlock.Text = $HeaderTitle
        $HeaderBlock.FontSize = 14
        $HeaderBlock.FontWeight = [System.Windows.FontWeights]::Bold
        $HeaderBlock.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#00B4D8")
        $HeaderBlock.Margin = New-Object System.Windows.Thickness(4, 10, 4, 4)
        $AppsContainerList.Children.Add($HeaderBlock) | Out-Null

        foreach ($App in $Group.Group) {
            $Cb = New-Object System.Windows.Controls.CheckBox
            $Cb.Content = "$($App.FriendlyName)"
            $Cb.Tag = $App
            $Cb.ToolTip = "$($App.Description)`nImpacto: $(if($App.Recommendation -eq 'caution'){'Requer cautela'}else{'Seguro'})"
            $Cb.IsChecked = [bool]$App.SelectedByDefault

            if ($App.Recommendation -eq "caution") {
                $Cb.Foreground = [System.Windows.Media.BrushConverter]::new().ConvertFromString("#F39C12")
            }

            $AppCheckBoxes.Add($Cb)
            $AppsContainerList.Children.Add($Cb) | Out-Null
        }
    }

    # 2. Renderizar Tweaks
    foreach ($Tweak in $AllTweaks) {
        $Cb = New-Object System.Windows.Controls.CheckBox
        $Cb.Content = "$($Tweak.Name)"
        $Cb.Tag = $Tweak
        $Cb.ToolTip = "$($Tweak.Description)"
        $Cb.IsChecked = [bool]$Tweak.SelectedByDefault

        $TweakCheckBoxes[$Tweak.Id] = $Cb

        if ($Tweak.Category -in @("Privacy", "AI")) {
            $PrivacyTweaksContainer.Children.Add($Cb) | Out-Null
        } else {
            $SystemTweaksContainer.Children.Add($Cb) | Out-Null
        }
    }

    # Função de Log com Dispatcher (Segura e fluida)
    $AppendLog = {
        param([string]$Text)
        $Timestamp = Get-Date -Format "HH:mm:ss"
        $LogOutputBox.AppendText("[$Timestamp] $Text`r`n")
        $LogOutputBox.ScrollToEnd()
        Invoke-DoEvents
    }

    $SetStatus = {
        param([string]$Text)
        $StatusBarText.Text = $Text
        Invoke-DoEvents
    }

    # Busca em tempo real de apps
    $AppSearchBox.Add_TextChanged({
        $Filter = $AppSearchBox.Text.ToLower().Trim()
        foreach ($Cb in $AppCheckBoxes) {
            $app = $Cb.Tag
            $Match = ($app.FriendlyName.ToLower().Contains($Filter)) -or ($app.Description.ToLower().Contains($Filter))
            $Cb.Visibility = if ($Match) { [System.Windows.Visibility]::Visible } else { [System.Windows.Visibility]::Collapsed }
        }
    })

    # Botões Rápidos
    $BtnAppsSelectDefault.Add_Click({
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = [bool]$Cb.Tag.SelectedByDefault }
        $StatusBarText.Text = "Seleção padrão de aplicativos carregada."
    })
    $BtnAppsSelectAll.Add_Click({
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = $true }
        $StatusBarText.Text = "Todos os aplicativos marcados para remoção."
    })
    $BtnAppsDeselectAll.Add_Click({
        foreach ($Cb in $AppCheckBoxes) { $Cb.IsChecked = $false }
        $StatusBarText.Text = "Todos os aplicativos desmarcados."
    })

    # Carregar Presets
    $LoadPreset = {
        param([string]$PresetName)
        $PresetPath = "$ScriptDir\Config\Presets\$PresetName.json"
        if (Test-Path $PresetPath) {
            $PresetJson = Get-Content -LiteralPath $PresetPath -Raw -Encoding UTF8 | ConvertFrom-Json
            
            foreach ($Cb in $AppCheckBoxes) {
                $Cb.IsChecked = ($PresetJson.RemoveApps -contains $Cb.Tag.Id)
            }

            foreach ($TweakId in $TweakCheckBoxes.Keys) {
                $TweakCheckBoxes[$TweakId].IsChecked = ($PresetJson.ApplyTweaks -contains $TweakId)
            }

            $StatusBarText.Text = "Perfil '$($PresetJson.DisplayName)' carregado com sucesso!"
            & $AppendLog "Perfil '$($PresetJson.DisplayName)' aplicado na interface."
            [System.Windows.MessageBox]::Show("Perfil '$($PresetJson.DisplayName)' carregado!`n`nVocê pode revisar os aplicativos e tweaks nas opções laterais antes de aplicar.", "Perfil Carregado", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null
        }
    }

    $BtnPresetPadrao.Add_Click({ & $LoadPreset "Padrao" })
    $BtnPresetGamer.Add_Click({ & $LoadPreset "Gamer" })
    $BtnPresetCorp.Add_Click({ & $LoadPreset "Corporativo" })
    $BtnPresetExtremo.Add_Click({ & $LoadPreset "Extremo" })

    # Botão Undo
    $BtnUndoAction.Add_Click({
        $Confirm = [System.Windows.MessageBox]::Show("Deseja realmente restaurar as configurações padrão do Windows e reativar os serviços?", "Confirmar Restauração", [System.Windows.MessageBoxButton]::YesNo, [System.Windows.MessageBoxImage]::Question)
        if ($Confirm -eq [System.Windows.MessageBoxResult]::Yes) {
            & $SwitchTab $NavBtnLogs $PanelLogs
            & $AppendLog "Iniciando reversão de otimizações (Undo Engine)..."
            $IsWin11 = ($CurrentOS -like "*Windows 11*")
            Invoke-SystemUndoTweaks -IsWin11 $IsWin11
            & $AppendLog "Restauração finalizada com sucesso."
            [System.Windows.MessageBox]::Show("Configurações padrão restauradas com sucesso!", "Restauração Concluída", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null
        }
    })

    # Abrir Logs
    $BtnOpenLogsFolder.Add_Click({
        $LogDir = "$ScriptDir\logs"
        if (!(Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
        Start-Process explorer.exe -ArgumentList "`"$LogDir`""
    })

    # Botão Fechar
    $BtnCloseWindow.Add_Click({ $Window.Close() })

    # EXECUÇÃO DE OTIMIZAÇÃO COM RESPONSIVIDADE EM TEMPO REAL E DISPATCHER PUMPING (ZERO TRAVAMENTOS)
    $BtnApplyOptimization.Add_Click({
        $BtnApplyOptimization.IsEnabled = $false
        $GlobalProgressBar.Visibility = [System.Windows.Visibility]::Visible
        $GlobalProgressBar.IsIndeterminate = $true
        
        # Muda automaticamente para a aba de Logs para o usuário acompanhar tudo acontecendo ao vivo!
        & $SwitchTab $NavBtnLogs $PanelLogs

        & $SetStatus "Otimizando o sistema em segundo plano..."
        & $AppendLog "=== INICIANDO PROTOCOLO DE OTIMIZAÇÃO ==="

        # Coleta de Parâmetros da UI
        $DoRestorePoint = ($ChkRestorePoint.IsChecked -eq $true)
        $SelectedAppObjects = @()
        foreach ($Cb in $AppCheckBoxes) {
            if ($Cb.IsChecked -eq $true) {
                $SelectedAppObjects += $Cb.Tag
            }
        }

        $TweaksState = @{}
        foreach ($k in $TweakCheckBoxes.Keys) {
            $TweaksState[$k] = [bool]$TweakCheckBoxes[$k].IsChecked
        }

        Invoke-DoEvents

        # 1. Ponto de Restauração (Execução não-bloqueante a 60 FPS)
        if ($DoRestorePoint) {
            & $AppendLog "Criando Ponto de Restauração do Windows (Proteção de Sistema)..."
            & $SetStatus "Criando Ponto de Restauração..."
            Invoke-DoEvents
            Invoke-SystemRestorePoint -Description "Pre_Debloat_GUI" | Out-Null
            & $AppendLog "Ponto de Restauração concluído com êxito."
            Invoke-DoEvents
        }

        # 2. Remoção de Apps com Pré-Cache Rápido e Barra de Progresso Real
        if ($SelectedAppObjects.Count -gt 0) {
            & $AppendLog "Removendo $($SelectedAppObjects.Count) aplicativos selecionados..."
            
            $GlobalProgressBar.IsIndeterminate = $false
            $GlobalProgressBar.Minimum = 0
            $GlobalProgressBar.Maximum = $SelectedAppObjects.Count
            $GlobalProgressBar.Value = 0

            # Pré-carregar pacotes provisionados uma única vez para performance instantânea
            $CachedProv = @()
            try {
                $CachedProv = @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue)
            } catch {}

            $CurIndex = 0
            $TotApps = $SelectedAppObjects.Count
            foreach ($AppObj in $SelectedAppObjects) {
                $CurIndex++
                $GlobalProgressBar.Value = $CurIndex
                & $SetStatus "Removendo: $($AppObj.FriendlyName) ($CurIndex/$TotApps)"
                & $AppendLog "  [$CurIndex/$TotApps] Removendo: $($AppObj.FriendlyName)..."
                Invoke-DoEvents
                
                Remove-AppByDefinition -AppInfo $AppObj -CachedProvisioned $CachedProv
                Invoke-DoEvents
            }
            $GlobalProgressBar.IsIndeterminate = $true
        }

        # Remoção OneDrive
        & $AppendLog "Limpando Microsoft OneDrive..."
        & $SetStatus "Removendo Microsoft OneDrive..."
        Invoke-DoEvents
        Remove-OneDriveDeep
        Invoke-DoEvents

        # 3. Tweaks de Privacidade e IA
        if ($TweaksState["DisableTelemetry"]) {
            & $AppendLog "Desativando Telemetria e Diagnósticos..."
            & $SetStatus "Desativando Telemetria..."
            Invoke-DoEvents
            Disable-SystemTelemetry
        }

        if ($TweaksState["DisableRecallAndAI"] -or $TweaksState["DisableCopilot"]) {
            & $AppendLog "Bloqueando Windows Recall, Click-to-Do e Copilot..."
            & $SetStatus "Bloqueando Recursos de IA..."
            Invoke-DoEvents
            $IsWin11 = ($CurrentOS -like "*Windows 11*")
            Disable-WindowsAIAndRecall -IsWin11 $IsWin11
        }

        if ($TweaksState["DisableWindowsSuggestions"]) {
            & $AppendLog "Desativando Sugestões e Anúncios do Windows..."
            & $SetStatus "Desativando Sugestões e Anúncios..."
            Invoke-DoEvents
            Disable-WindowsSuggestionsAndAds
        }

        if ($TweaksState["DisableEdgeBackgroundAndAds"]) {
            & $AppendLog "Desativando background e anúncios do Edge..."
            & $SetStatus "Ajustando Microsoft Edge..."
            Invoke-DoEvents
            Disable-EdgeBackgroundAndTelemetry
        }

        # 4. Jogos e Xbox
        if ($TweaksState["DisableGameDVR"]) {
            & $AppendLog "Desativando gravação em segundo plano (GameDVR)..."
            & $SetStatus "Desativando GameDVR..."
            Invoke-DoEvents
            Disable-GameDVRBackgroundCapture
        }

        $XboxSelected = $SelectedAppObjects | Where-Object { $_.Id -eq "XboxGamingOverlay" }
        if ($null -ne $XboxSelected -or $TweaksState["FixGameBarProtocols"]) {
            & $AppendLog "Aplicando Null-Routing nos protocolos ms-gamingoverlay (Anti-Popup Fix)..."
            & $SetStatus "Aplicando correção de protocolos do Xbox..."
            Invoke-DoEvents
            Apply-GameBarProtocolNullRoute
        }

        # 5. Performance e Interface
        if ($TweaksState["RestoreClassicContextMenu"] -or $TweaksState["ShowFileExtensions"]) {
            & $AppendLog "Ajustando interface do Windows Explorer..."
            & $SetStatus "Configurando Explorer..."
            Invoke-DoEvents
            $IsWin11 = ($CurrentOS -like "*Windows 11*")
            Set-ExplorerAndUITweaks -IsWin11 $IsWin11
        }

        if ($TweaksState["InjectUltimatePerformancePlan"] -or $TweaksState["DisableFastStartup"] -or $TweaksState["OptimizeVisualEffects"]) {
            & $AppendLog "Aplicando plano Ultimate Performance e ajustes de energia..."
            & $SetStatus "Aplicando Ultimate Performance..."
            Invoke-DoEvents
            Set-PowerAndPerformanceTweaks
        }

        if ($TweaksState["DisableSysMainAndWSearch"]) {
            & $AppendLog "Desativando SysMain e Windows Search..."
            & $SetStatus "Desativando SysMain e WSearch..."
            Invoke-DoEvents
            Disable-LegacyHeavyServices
        }

        if ($TweaksState["ForceUninstallEdge"]) {
            & $AppendLog "Desinstalando Microsoft Edge profundamente..."
            & $SetStatus "Removendo Microsoft Edge..."
            Invoke-DoEvents
            Invoke-EdgeForceRemoveClean
        }

        # 6. Limpeza e Finalização
        & $AppendLog "Limpando arquivos temporários (%TEMP%)..."
        & $SetStatus "Limpando temporários..."
        Invoke-DoEvents
        Clear-SystemTempFiles

        & $AppendLog "Reiniciando Windows Explorer..."
        & $SetStatus "Reiniciando Explorer..."
        Invoke-DoEvents
        Restart-WindowsExplorer

        & $AppendLog "=== OTIMIZAÇÃO CONCLUÍDA COM SUCESSO ==="
        & $SetStatus "Otimização concluída com sucesso!"
        $GlobalProgressBar.Visibility = [System.Windows.Visibility]::Collapsed
        $BtnApplyOptimization.IsEnabled = $true

        [System.Windows.MessageBox]::Show("Otimização concluída com sucesso!`n`nO Windows Explorer foi reiniciado para aplicar as alterações.", "Otimização Concluída", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null
    })

    $Window.ShowDialog() | Out-Null
}
