using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;
using Otimizador.Core;

namespace Otimizador.UI
{
    public static class AppSelectionModal
    {
        private const string ModalXaml = @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        Title=""Seleção de Aplicativos para Remoção | Otimizador de Windows""
        Height=""650"" Width=""720""
        WindowStartupLocation=""CenterScreen""
        Background=""#181818"" Foreground=""#F0F0F0""
        FontFamily=""Segoe UI"" WindowStyle=""ThreeDBorderWindow"" ResizeMode=""CanResize"">
    <Window.Resources>
        <Style TargetType=""CheckBox"">
            <Setter Property=""Foreground"" Value=""#E0E0E0""/>
            <Setter Property=""Margin"" Value=""4,4,4,4""/>
            <Setter Property=""FontSize"" Value=""13""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
        </Style>
        <Style TargetType=""Button"">
            <Setter Property=""Background"" Value=""#2A2A2A""/>
            <Setter Property=""Foreground"" Value=""#FFFFFF""/>
            <Setter Property=""BorderBrush"" Value=""#3D3D3D""/>
            <Setter Property=""BorderThickness"" Value=""1""/>
            <Setter Property=""Padding"" Value=""12,6""/>
            <Setter Property=""Margin"" Value=""4""/>
            <Setter Property=""Cursor"" Value=""Hand""/>
            <Setter Property=""FontSize"" Value=""12""/>
        </Style>
    </Window.Resources>
    <Grid Margin=""16"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""Auto""/>
            <RowDefinition Height=""Auto""/>
            <RowDefinition Height=""*""/>
            <RowDefinition Height=""Auto""/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row=""0"" Margin=""0,0,0,12"">
            <TextBlock Text=""Personalizar Remoção de Aplicativos (Debloat)"" FontSize=""18"" FontWeight=""Bold"" Foreground=""#00B4D8""/>
            <TextBlock Text=""Marque os itens que deseja desinstalar. Desmarque itens como Xbox Game Bar ou Teams se você os utiliza no dia a dia.""
                       FontSize=""12"" Foreground=""#A0A0A0"" TextWrapping=""Wrap"" Margin=""0,4,0,0""/>
        </StackPanel>
        <Grid Grid.Row=""1"" Margin=""0,0,0,10"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>
            <TextBox Name=""SearchBox"" Grid.Column=""0"" Height=""30"" VerticalContentAlignment=""Center""
                     Background=""#222222"" Foreground=""#FFFFFF"" BorderBrush=""#444444"" Padding=""8,2""
                     FontSize=""13"" ToolTip=""Digite para filtrar os aplicativos...""/>
            <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" Margin=""8,0,0,0"">
                <Button Name=""BtnSelectDefault"" Content=""Padrão Recomendado"" Background=""#0077B6"" BorderBrush=""#0096C7""/>
                <Button Name=""BtnSelectAll"" Content=""Marcar Todos""/>
                <Button Name=""BtnDeselectAll"" Content=""Desmarcar Todos""/>
            </StackPanel>
        </Grid>
        <Border Grid.Row=""2"" Background=""#202020"" BorderBrush=""#333333"" BorderThickness=""1"" CornerRadius=""4"" Padding=""8"">
            <ScrollViewer VerticalScrollBarVisibility=""Auto"">
                <StackPanel Name=""AppsContainer""/>
            </ScrollViewer>
        </Border>
        <Grid Grid.Row=""3"" Margin=""0,14,0,0"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>
            <TextBlock Name=""SelectionCountText"" Grid.Column=""0"" VerticalAlignment=""Center""
                       FontSize=""12"" Foreground=""#48CAE4"" Text=""0 aplicativo(s) selecionado(s)""/>
            <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                <Button Name=""BtnCancel"" Content=""Cancelar"" Width=""100""/>
                <Button Name=""BtnConfirm"" Content=""Confirmar e Continuar"" Width=""170""
                        Background=""#2B9348"" BorderBrush=""#55A630"" FontWeight=""SemiBold""/>
            </StackPanel>
        </Grid>
    </Grid>
</Window>";

        private static readonly Dictionary<string, string> CategoryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Gaming", "[+] Jogos e Xbox" },
            { "AI", "[+] Inteligência Artificial e Copilot" },
            { "Productivity", "[+] Produtividade e Office" },
            { "Communication", "[+] Comunicação e Mensagens" },
            { "Media", "[+] Mídia, Vídeo e Áudio" },
            { "Utilities", "[+] Utilitários e Noticias" },
            { "ThirdPartyBloat", "[+] Bloatware Pré-instalado (OEM / Patrocinados)" }
        };

        public static string[] ShowDialog(IList<AppItem> allApps, IEnumerable<string> initiallySelectedIds)
        {
            Window window;
            using (var sr = new StringReader(ModalXaml))
            using (var xr = XmlReader.Create(sr))
            {
                window = (Window)XamlReader.Load(xr);
            }

            ImageSource appIcon = MainWindowGUI.LoadApplicationIcon();
            if (appIcon != null)
            {
                window.Icon = appIcon;
            }

            var searchBox = (TextBox)window.FindName("SearchBox");
            var btnSelectDefault = (Button)window.FindName("BtnSelectDefault");
            var btnSelectAll = (Button)window.FindName("BtnSelectAll");
            var btnDeselectAll = (Button)window.FindName("BtnDeselectAll");
            var btnConfirm = (Button)window.FindName("BtnConfirm");
            var btnCancel = (Button)window.FindName("BtnCancel");
            var appsContainer = (StackPanel)window.FindName("AppsContainer");
            var selectionCountText = (TextBlock)window.FindName("SelectionCountText");

            var initialSet = new HashSet<string>(initiallySelectedIds ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var checkBoxes = new List<CheckBox>();
            var converter = new BrushConverter();

            Action updateCount = delegate
            {
                int selected = 0;
                foreach (CheckBox cb in checkBoxes)
                {
                    if (cb.IsChecked == true) selected++;
                }
                selectionCountText.Text = string.Format("{0} aplicativo(s) selecionado(s) para remoção", selected);
            };

            var groups = new Dictionary<string, List<AppItem>>(StringComparer.OrdinalIgnoreCase);
            var groupOrder = new List<string>();
            foreach (AppItem app in allApps)
            {
                string cat = app.Category ?? "Outros";
                if (!groups.ContainsKey(cat))
                {
                    groups[cat] = new List<AppItem>();
                    groupOrder.Add(cat);
                }
                groups[cat].Add(app);
            }

            foreach (string cat in groupOrder)
            {
                string title = CategoryNames.ContainsKey(cat) ? CategoryNames[cat] : cat;
                var header = new TextBlock
                {
                    Text = title,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)converter.ConvertFromString("#90E0EF"),
                    Margin = new Thickness(4, 10, 4, 4)
                };
                appsContainer.Children.Add(header);

                foreach (AppItem app in groups[cat])
                {
                    var cb = new CheckBox
                    {
                        Content = app.FriendlyName,
                        Tag = app,
                        ToolTip = string.Format("{0}\nImpacto: {1}", app.Description, app.Recommendation == "caution" ? "Requer cautela" : "Seguro")
                    };

                    if (initialSet.Count > 0)
                    {
                        cb.IsChecked = initialSet.Contains(app.Id);
                    }
                    else
                    {
                        cb.IsChecked = app.SelectedByDefault;
                    }

                    if (string.Equals(app.Recommendation, "caution", StringComparison.OrdinalIgnoreCase))
                    {
                        cb.Foreground = (Brush)converter.ConvertFromString("#F4A261");
                    }

                    cb.Checked += delegate { updateCount(); };
                    cb.Unchecked += delegate { updateCount(); };

                    checkBoxes.Add(cb);
                    appsContainer.Children.Add(cb);
                }
            }

            updateCount();

            btnSelectDefault.Click += delegate
            {
                foreach (CheckBox cb in checkBoxes)
                {
                    var app = (AppItem)cb.Tag;
                    cb.IsChecked = app.SelectedByDefault;
                }
                updateCount();
            };

            btnSelectAll.Click += delegate
            {
                foreach (CheckBox cb in checkBoxes) cb.IsChecked = true;
                updateCount();
            };

            btnDeselectAll.Click += delegate
            {
                foreach (CheckBox cb in checkBoxes) cb.IsChecked = false;
                updateCount();
            };

            searchBox.TextChanged += delegate
            {
                string filter = (searchBox.Text ?? string.Empty).ToLowerInvariant().Trim();
                foreach (CheckBox cb in checkBoxes)
                {
                    var app = (AppItem)cb.Tag;
                    bool match = (app.FriendlyName ?? string.Empty).ToLowerInvariant().Contains(filter)
                              || (app.Description ?? string.Empty).ToLowerInvariant().Contains(filter);
                    cb.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                }
            };

            string[] dialogResultIds = null;

            btnConfirm.Click += delegate
            {
                var selected = new List<string>();
                foreach (CheckBox cb in checkBoxes)
                {
                    if (cb.IsChecked == true)
                    {
                        selected.Add(((AppItem)cb.Tag).Id);
                    }
                }
                dialogResultIds = selected.ToArray();
                window.Close();
            };

            btnCancel.Click += delegate
            {
                dialogResultIds = null;
                window.Close();
            };

            window.ShowDialog();
            return dialogResultIds;
        }
    }
}
