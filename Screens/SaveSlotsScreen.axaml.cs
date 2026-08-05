using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenTradeEngine.Screens;

public partial class SaveSlotsScreen : UserControl
{
    public event EventHandler<int>? SlotSelected;
    public event EventHandler? AutosaveSelected;
    public event EventHandler? BackRequested;

    public SaveSlotsScreen() => InitializeComponent();

    public void Load(bool saveMode)
    {
        Heading.Text = saveMode ? "Save Game" : "Load Game";
        SlotButtons.Children.Clear();
        if (!saveMode && GameSaveService.AutosaveExists) AddButton("Autosave", -1);
        for (var slot = 1; slot <= 6; slot++)
        {
            var session = GameSaveService.LoadSlot(slot);
            var company = session?.Companies.Find(candidate => candidate.IsHuman)?.Name;
            AddButton(session is null ? $"Slot {slot} — Empty" : $"Slot {slot} — {company}, Week {session.Week}", slot);
        }
    }

    public void ShowStatus(string text) => StatusText.Text = text;

    private void AddButton(string text, int slot)
    {
        var button = new Button
        {
            Content = text,
            Height = 64,
            FontSize = 21,
            Tag = slot,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        button.Click += SlotButton_Click;
        SlotButtons.Children.Add(button);
    }

    private void SlotButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int slot }) return;
        if (slot < 0) AutosaveSelected?.Invoke(this, EventArgs.Empty);
        else SlotSelected?.Invoke(this, slot);
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
}
