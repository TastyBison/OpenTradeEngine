using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenTradeEngine.Screens;

public partial class QuickOptionsScreen : UserControl
{
    private static readonly (string Key, string Name, string Description)[] Options =
    [
        ("buy", "Marketplace", "Buy or sell max by clicking on the Marketplace grid."),
        ("warehouse", "Warehouse", "Store or take max by clicking on the Warehouse grid."),
        ("deposit", "Deposit", "Automatically deposit all your cash in the Bank before traveling."),
        ("bank", "Bank", "Deposit or withdraw max from the Main Menu and bank remaining cash before traveling."),
        ("borrow", "Loan", "Borrow or pay back money from the Main Menu."),
        ("passengers", "Passengers", "Pick up passengers from the Main Menu."),
        ("advertise", "Advertise", "Advertise from the Main Menu."),
        ("crew", "Crew", "Pay your crew from the Main Menu."),
        ("tax", "Taxes", "Pay your taxes from the Main Menu."),
        ("insurance", "Insurance", "Buy insurance from the Main Menu."),
        ("fuel", "Fuel", "Fill up your tank from the Main Menu."),
        ("journey", "Travel", "Skip the screen that shows your ship traveling."),
        ("explore", "Explore", "Go to the Planet Special straight from the Main Menu.")
    ];

    private readonly Dictionary<string, Button> _buttons = new(StringComparer.OrdinalIgnoreCase);
    private CompanyState? _company;
    public event EventHandler? ContinueRequested;
    public event EventHandler? OptionsChanged;
    public QuickOptionsScreen() => InitializeComponent();

    public void Load(GameInstallation installation, CompanyState company)
    {
        _company = company;
        var stars = SwfImageExtractor.TryExtractLargestEmbeddedImage(
            Path.Combine(installation.SwfDirectory, "STARS2.SWF"), "STARS2");
        if (stars.IsSuccessful) StarsImage.Source = new Bitmap(stars.ImagePath!);
        var help = SwfImageExtractor.TryExtractEmbeddedImage(
            installation.MainSwfPath, "Gazillionaire__embed_mxml_i_help");
        if (help.IsSuccessful) HelpIcon.Source = new Bitmap(help.ImagePath!);

        OptionsPanel.Children.Clear();
        DescriptionsPanel.Children.Clear();
        _buttons.Clear();
        foreach (var option in Options)
        {
            var button = new Button
            {
                Classes = { "quick-option" }, Height = 39, HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = option.Key
            };
            button.Click += OptionButton_Click;
            _buttons[option.Key] = button;
            OptionsPanel.Children.Add(button);
            DescriptionsPanel.Children.Add(new TextBlock
            {
                Text = option.Description, Height = 39, Foreground = Brushes.White, FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
            });
        }
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        if (_company is null) return;
        foreach (var option in Options)
            _buttons[option.Key].Content = $"Quick {option.Name} " +
                                           (_company.Shortcuts.GetValueOrDefault(option.Key) ? "On" : "Off");
    }

    private void OptionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null || sender is not Button { Tag: string key }) return;
        _company.Shortcuts[key] = !_company.Shortcuts.GetValueOrDefault(key);
        RefreshButtons();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleAllButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_company is null) return;
        var enable = Options.Any(option => !_company.Shortcuts.GetValueOrDefault(option.Key));
        foreach (var option in Options) _company.Shortcuts[option.Key] = enable;
        RefreshButtons();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HelpButton_Click(object? sender, RoutedEventArgs e) =>
        HelpOverlay.Show("Shortcuts Help", OriginalHelpCatalog.Shortcuts);
    private void ContinueButton_Click(object? sender, RoutedEventArgs e) => ContinueRequested?.Invoke(this, EventArgs.Empty);
}
