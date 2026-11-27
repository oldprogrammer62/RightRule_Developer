using Microsoft.Extensions.DependencyInjection;
using RightRule.LanguageProcessor.Evaluator;
using RightRule.Models;
using RightRule.RightRuleSession;
using RightRule.RuleServices.RuleModels;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace RightRule_Developer;

public partial class MainWindow : Window
{
    private SessionDocument? _document;
    private IServiceScope? _scope;
    private IRightRuleSession? _engine;
    private bool _dirty;
    private bool _refreshing;
    private readonly ObservableCollection<string> _contexts = new();
    private sealed record DataRow(string DatasetName, string ItemName, string Value);
    private enum RuleEditorAction { Cancel, Save, Delete }

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(SessionFiles.Root);
        RefreshSessions();
        AddLog("RightRule Developer ready. Create or load a session to begin.");
    }

    private void RefreshSessions(string? selected = null)
    {
        SessionNameText.Text = string.IsNullOrWhiteSpace(selected) ? "Not loaded" : selected;
    }
    private void AddLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
    private void MarkDirty() { _dirty = true; if (_document != null) Title = $"RightRule Developer — {_document.Name} *"; }
    private bool SaveIfNeeded()
    {
        if (!_dirty || _document == null) return true;
        var answer = MessageBox.Show($"Save changes to '{_document.Name}'?", "Unsaved session", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) SaveDocument();
        return true;
    }
    private void CloseEngine()
    {
        _scope?.Dispose(); _scope = null; _engine = null;
        OperatorsBox.ItemsSource = null; OperatorsBox.IsEnabled = false;
    }
    private void Activate(SessionDocument document)
    {
        CloseEngine();
        _scope = App.Services.CreateScope();
        _engine = _scope.ServiceProvider.GetRequiredService<IRightRuleSession>();
        _document = document;
        RefreshOperators();
        var migratedValues = false;
        foreach (var ds in document.Datasets) migratedValues |= RegisterDataset(ds.Key, ds.Value);
        foreach (var rule in document.Rules) RegisterRule(rule);
        foreach (var rule in document.Defaults) RegisterDefault(rule);
        _dirty = migratedValues;
        Title = $"RightRule Developer — {document.Name}{(_dirty ? " *" : "")}";
        RefreshSessions(document.Name); RefreshLists();
        AddLog($"Session '{document.Name}' loaded: {document.Datasets.Count} dataset(s), {document.Rules.Count} rule(s), {document.Defaults.Count} default rule(s).");
        if (migratedValues) AddLog("Converted numeric text values used in numeric rule comparisons to numeric data types. Save the session to keep this correction.");
        StatusText.Text = $"Session: {document.Name}";
    }
    private void RefreshLists()
    {
        _refreshing = true;
        DatasetsBox.ItemsSource = _document == null ? [] : new[] { "(all datasets)" }.Concat(_document.Datasets.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)).ToList();
        if (_document != null) DatasetsBox.SelectedIndex = 0;
        _contexts.Clear();
        if (_document != null) foreach (var context in _document.Rules.Select(r => r.Context).Concat(_document.Defaults.Select(r => r.Context)).Distinct().OrderBy(c => c)) _contexts.Add(context);
        ContextsBox.ItemsSource = null;
        ContextsBox.ItemsSource = _document == null ? Array.Empty<string>() : new[] { "(all contexts)" }.Concat(_contexts).ToList();
        if (_contexts.Count > 0 && ContextsBox.SelectedIndex < 0) ContextsBox.SelectedIndex = 0;
        if (_document == null) { RulesGrid.ItemsSource = null; DataGridView.ItemsSource = null; RuleCountText.Text = ""; }
        else { RefreshRules(); RefreshDatasetView(); }
        _refreshing = false;
    }
    private void RefreshOperators()
    {
        var operators = _engine?.OperatorService?.GetOperators() ?? [];
        OperatorsBox.ItemsSource = operators.Select(op => $"{op.Name}  ({string.Join(", ", op.Aliases)})").ToList();
        OperatorsBox.SelectedIndex = OperatorsBox.Items.Count > 0 ? 0 : -1;
        OperatorsBox.IsEnabled = OperatorsBox.Items.Count > 0;
    }
    private void ShowOperators_Click(object sender, RoutedEventArgs e)
    {
        var operators = _engine?.OperatorService?.GetOperators() ?? [];
        var dialog = new Window
        {
            Title = "Loaded operators",
            Width = 520,
            Height = 420,
            MinWidth = 380,
            MinHeight = 260,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (operators.Count == 0)
        {
            dialog.Content = new TextBlock
            {
                Text = "No operators are loaded. Create or load a session to load the configured operators.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16),
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            var grid = new DataGrid
            {
                ItemsSource = operators.Select(op => new
                {
                    op.Name,
                    Aliases = string.Join(", ", op.Aliases),
                    Default = op.GetType().Namespace?.Contains(".DefaultOperators", StringComparison.Ordinal) == true ? "✓" : ""
                }).ToList(),
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                Margin = new Thickness(12)
            };
            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Auto) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Aliases", Binding = new System.Windows.Data.Binding("Aliases"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Default", Binding = new System.Windows.Data.Binding("Default"), Width = new DataGridLength(75) });
            dialog.Content = grid;
        }
        dialog.ShowDialog();
    }
    private void About_Click(object sender, RoutedEventArgs e)
    {
        static string VersionOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => assembly.GetName().Name == name)?.GetName().Version?.ToString() ?? "not loaded";
        MessageBox.Show(this,
            $"RightRule Developer\n\nRightRule: {VersionOf("RightRule")}\nRightRule.System.Common: {VersionOf("RightRule.System.Common")}",
            "About RightRule Developer", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void RefreshRules()
    {
        var ctx = ContextsBox.SelectedItem as string;
        var rules = _document?.Rules
            .Where(r => ctx == "(all contexts)" || r.Context == ctx)
            .OrderBy(r => r.Context, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Number)
            .ToList() ?? [];
        RulesGrid.ItemsSource = rules;
        RulesHeader.Text = ctx == null ? "Registered Rules" : $"Registered Rules - {ctx}";
        RuleCountText.Text = $"{rules.Count} rule(s)";
    }
    private void RefreshDatasetView()
    {
        if (_document == null) { DataGridView.ItemsSource = null; return; }
        var selectedDataset = DatasetsBox.SelectedItem as string;
        var datasets = _document.Datasets.Where(ds => selectedDataset == "(all datasets)" || ds.Key == selectedDataset);
        DataGridView.ItemsSource = datasets.SelectMany(ds => ds.Value.Select(item => new DataRow(ds.Key, item.Key, item.Value.ToString()))).ToList();
    }
    private bool RegisterDataset(string name, Dictionary<string, JsonElement> values)
    {
        var data = new Dictionary<string, object?>();
        var converted = false;
        foreach (var item in values.ToArray())
        {
            var value = item.Value;
            if (value.ValueKind == JsonValueKind.String && IsNumericRuleOperand(name, item.Key))
            {
                try
                {
                    using var parsed = JsonDocument.Parse(value.GetString() ?? "");
                    if (parsed.RootElement.ValueKind == JsonValueKind.Number)
                    {
                        value = parsed.RootElement.Clone();
                        values[item.Key] = value;
                        converted = true;
                    }
                }
                catch (JsonException) { }
            }
            data[item.Key] = ToObject(value);
        }
        var result = _engine!.DataService.RegisterData(name, data);
        AddLog($"Data registration {result.Status}: {name} — {result.Message}");
        return converted;
    }
    private static object? ToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(), JsonValueKind.Number => element.TryGetInt64(out var i) ? i : element.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(), _ => element.ToString()
    };
    private JsonElement ParseEditedDataValue(string text, JsonElement previousValue, string datasetName, string itemName)
    {
        bool numericOperand = IsNumericRuleOperand(datasetName, itemName);
        bool parseAsJson = previousValue.ValueKind is JsonValueKind.Number or JsonValueKind.Object or JsonValueKind.Array
            || previousValue.ValueKind == JsonValueKind.String && numericOperand;
        if (parseAsJson)
        {
            using var parsed = JsonDocument.Parse(text);
            if (previousValue.ValueKind == JsonValueKind.Number && parsed.RootElement.ValueKind != JsonValueKind.Number)
                throw new FormatException("Enter a numeric value for this data item.");
            if (numericOperand && parsed.RootElement.ValueKind != JsonValueKind.Number)
                throw new FormatException("This data item is used in a numeric comparison. Enter a numeric value.");
            return parsed.RootElement.Clone();
        }
        if (previousValue.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            if (!bool.TryParse(text, out var booleanValue)) throw new FormatException("Enter true or false for this data item.");
            return JsonDocument.Parse(JsonSerializer.Serialize(booleanValue)).RootElement.Clone();
        }
        if (previousValue.ValueKind == JsonValueKind.Null && string.IsNullOrWhiteSpace(text))
            return JsonDocument.Parse("null").RootElement.Clone();
        return JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement.Clone();
    }
    private bool IsNumericRuleOperand(string datasetName, string itemName)
    {
        if (_document == null) return false;
        var operand = Regex.Escape(datasetName) + @"\s*\.\s*" + Regex.Escape(itemName);
        var numericOperator = @"(?:>=|<=|>|<|greaterthanorequals|lessthan)";
        return _document.Rules.Any(rule =>
            Regex.IsMatch(rule.Statement, operand + @"\s*" + numericOperator, RegexOptions.IgnoreCase)
            || Regex.IsMatch(rule.Statement, numericOperator + @"\s*" + operand, RegexOptions.IgnoreCase));
    }
    private void RegisterRule(RuleDocument r)
    {
        var definition = _engine!.RuleService.BuildRule().Number(r.Number).Name(r.Name).Context(r.Context).Statement(r.Statement).Decision(r.Decision).DecisionData(r.DecisionData).OptionalData(r.OptionalData).Build();
        var result = _engine.RuleService.RegisterRule(definition);
        AddLog($"Rule registration {result.Status}: {r.Context}/{r.Name} — {result.Message}");
    }
    private void RegisterDefault(RuleDocument r)
    {
        var definition = _engine!.RuleService.BuildDefaultRule().Name(r.Name).Context(r.Context).Decision(r.Decision).DecisionData(r.DecisionData).Build();
        var result = _engine.RuleService.RegisterDefaultRule(definition);
        AddLog($"Default rule registration {result.Status}: {r.Context}/{r.Name} — {result.Message}");
    }
    private static string? Prompt(string title, string label, string initial = "")
    {
        var dialog = new Window { Title = title, Width = 520, Height = 165, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Owner = Application.Current.MainWindow, Background = System.Windows.Media.Brushes.White };
        var panel = new DockPanel { Margin = new Thickness(16) };
        var input = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 12), MinWidth = 460, Padding = new Thickness(6) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var stack = new StackPanel(); stack.Children.Add(new TextBlock { Text = label }); stack.Children.Add(input); panel.Children.Add(stack); dialog.Content = panel;
        ok.Click += (_, _) => dialog.DialogResult = true;
        return dialog.ShowDialog() == true ? input.Text : null;
    }
    private bool SelectSession(out string? name, string action = "Load")
    {
        name = null;
        var existing = SessionFiles.List().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (existing.Count == 0) { MessageBox.Show($"There are no saved sessions to {action.ToLowerInvariant()}."); return false; }
        if (existing.Count == 1) { name = existing[0]; return true; }
        var dialog = new Window { Title = $"{action} RightRule session", Width = 400, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Owner = this };
        var panel = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var load = new Button { Content = action, IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(load); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var picker = new ComboBox { ItemsSource = existing, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(5) };
        panel.Children.Add(picker); dialog.Content = panel;
        load.Click += (_, _) => dialog.DialogResult = true;
        if (dialog.ShowDialog() != true || picker.SelectedItem is not string selected) return false;
        name = selected;
        return true;
    }

    private void EditOptions_Click(object sender, RoutedEventArgs e)
    {
        var current = App.CurrentOptions;
        var dialog = new Window { Title = "RightRule options", Width = 650, Height = 650, MinWidth = 560, MinHeight = 500, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.CanResize };
        var root = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "Save options", IsDefault = true, MinWidth = 100, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 5, 8, 5) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 85, Padding = new Thickness(8, 5, 8, 5) };
        buttons.Children.Add(save); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var form = new StackPanel();
        CheckBox AddCheck(string label, bool value)
        {
            var check = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 5, 0, 5) };
            form.Children.Add(check); return check;
        }
        TextBox AddText(string label, string value, int height = 30)
        {
            form.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
            var box = new TextBox { Text = value, Height = height, Padding = new Thickness(5), AcceptsReturn = height > 30, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = height > 30 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
            form.Children.Add(box); return box;
        }
        var dataCheck = AddCheck("Enable data checks", current.DataCheck);
        var diagnostics = AddCheck("Enable diagnostics", current.Diagnostics);
        form.Children.Add(new Border { Height = 2, Background = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 12, 0, 8) });
        var loadDefault = AddCheck("Load default operators", current.LoadDefaultOperators);
        var defaultNames = AddText("Default operator names (one per line; blank means all)", string.Join(Environment.NewLine, current.DefaultOperatorNames), 65);
        form.Children.Add(new Border { Height = 2, Background = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 12, 0, 8) });
        var loadCustom = AddCheck("Load custom operators", current.LoadCustomOperators);
        var customNames = AddText("Custom operator names (one per line; blank means all)", string.Join(Environment.NewLine, current.CustomOperatorNames), 65);
        form.Children.Add(new Border { Height = 2, Background = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 12, 0, 8) });
        form.Children.Add(new TextBlock { Text = "Custom operator configuration path", Margin = new Thickness(0, 8, 0, 3) });
        var pathRow = new DockPanel();
        var browsePath = new Button { Content = "Browse…", MinWidth = 68, Height = 25, FontSize = 11, Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(browsePath, Dock.Right);
        var customPath = new TextBox { Text = current.CustomOperatorLibraryPath, MinWidth = 450, Height = 96, Padding = new Thickness(5), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalContentAlignment = VerticalAlignment.Top };
        pathRow.Children.Add(browsePath); pathRow.Children.Add(customPath); form.Children.Add(pathRow);
        browsePath.Click += (_, _) =>
        {
            var initialDirectory = SessionFiles.Root;
            try
            {
                var configuredPath = customPath.Text.Trim();
                var configuredDirectory = Directory.Exists(configuredPath)
                    ? configuredPath
                    : Path.GetDirectoryName(Path.GetFullPath(configuredPath));
                if (!string.IsNullOrWhiteSpace(configuredDirectory) && Directory.Exists(configuredDirectory))
                    initialDirectory = configuredDirectory;
            }
            catch { }
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select custom operator configuration",
                Filter = "JSON configuration files|*.json|All files|*.*",
                CheckFileExists = true,
                InitialDirectory = initialDirectory,
                FileName = Path.GetFileName(customPath.Text)
            };
            if (picker.ShowDialog(dialog) == true) customPath.Text = picker.FileName;
        };
        root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = root;
        save.Click += (_, _) =>
        {
            if (loadCustom.IsChecked == true && string.IsNullOrWhiteSpace(customPath.Text))
            {
                MessageBox.Show(dialog, "A custom operator configuration path is required when custom operators are enabled.", "Invalid options", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true) return;

        static List<string> ParseNames(string text) => text.Split([Environment.NewLine, ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var updated = new RightRuleOptions
        {
            DataCheck = dataCheck.IsChecked == true,
            Diagnostics = diagnostics.IsChecked == true,
            LoadDefaultOperators = loadDefault.IsChecked == true,
            DefaultOperatorNames = ParseNames(defaultNames.Text),
            LoadCustomOperators = loadCustom.IsChecked == true,
            CustomOperatorLibraryPath = customPath.Text.Trim(),
            CustomOperatorNames = ParseNames(customNames.Text)
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(App.OptionsFilePath)!);
            File.WriteAllText(App.OptionsFilePath, JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true }));
            var activeDocument = _document;
            var wasDirty = _dirty;
            CloseEngine();
            App.ConfigureServices(updated);
            if (activeDocument != null)
            {
                Activate(activeDocument);
                _dirty |= wasDirty;
                Title = $"RightRule Developer — {activeDocument.Name}{(_dirty ? " *" : "")}";
            }
            AddLog($"RightRule options saved to '{Path.GetFullPath(App.OptionsFilePath)}' and applied to the active session.");
            StatusText.Text = "RightRule options saved";
        }
        catch (Exception ex) { ShowError("Could not save RightRule options", ex); }
    }

    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveIfNeeded()) return;
        var dialog = new Window { Title = "New RightRule session", Width = 460, Height = 245, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Owner = this };
        var form = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var create = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(create); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); form.Children.Add(buttons);
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = "Session name (also used for its folder):" });
        var nameBox = new TextBox { Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(5) }; fields.Children.Add(nameBox);
        fields.Children.Add(new TextBlock { Text = "Description:" });
        var descriptionBox = new TextBox { Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(5) }; fields.Children.Add(descriptionBox);
        form.Children.Add(fields); dialog.Content = form;
        create.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) { MessageBox.Show(dialog, "Session name is required."); return; }
            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true) return;
        var name = nameBox.Text.Trim();
        name = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (name.Length == 0 || name is "." or "..") { MessageBox.Show("Choose a valid session name."); return; }
        if (Directory.Exists(Path.Combine(SessionFiles.Root, name))) { MessageBox.Show("A session with that name already exists. Load it or choose another name."); return; }
        var description = descriptionBox.Text.Trim();
        var options = App.CurrentOptions;
        var confirmation = MessageBox.Show(this,
            $"Create this session?\n\nName: {name}\nDescription: {(string.IsNullOrWhiteSpace(description) ? "(none)" : description)}\nSession folder: {Path.Combine(SessionFiles.Root, name)}\n\nData checks: {options.DataCheck}\nDiagnostics: {options.Diagnostics}\nLoad default operators: {options.LoadDefaultOperators}\nDefault operator names: {(options.DefaultOperatorNames.Count == 0 ? "(all)" : string.Join(", ", options.DefaultOperatorNames))}\nLoad custom operators: {options.LoadCustomOperators}\nCustom operator path: {options.CustomOperatorLibraryPath}\nCustom operator names: {(options.CustomOperatorNames.Count == 0 ? "(all)" : string.Join(", ", options.CustomOperatorNames))}",
            "Confirm session options", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.OK) return;
        var doc = new SessionDocument { Name = name, Description = description };
        try { SessionFiles.Write(doc); Activate(doc); }
        catch (Exception ex) { ShowError("Could not create session", ex); }
    }
    private void LoadSession_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveIfNeeded() || !SelectSession(out var name)) return;
        try { Activate(SessionFiles.Read(SessionFiles.FileFor(name!))); }
        catch (Exception ex) { ShowError("Could not load session", ex); }
    }
    private void RemoveSession_Click(object sender, RoutedEventArgs e)
    {
        if (!SelectSession(out var name, "Remove") || name == null) return;
        if (MessageBox.Show($"Remove session '{name}' and permanently delete its folder?", "Remove RightRule session", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (_document?.Name == name) { if (!SaveIfNeeded()) return; CloseEngine(); _document = null; _dirty = false; RefreshLists(); Title = "RightRule Developer"; StatusText.Text = "No session loaded"; }
        var sessionDirectory = Path.Combine(SessionFiles.Root, name);
        if (Directory.Exists(sessionDirectory)) Directory.Delete(sessionDirectory, true);
        RefreshSessions(_document?.Name); AddLog($"Removed session '{name}'.");
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveDocument();
    private void SaveDocument()
    {
        if (_document == null) { MessageBox.Show("There is no active session to save."); return; }
        try
        {
            SessionFiles.Write(_document);
            _dirty = false;
            Title = $"RightRule Developer — {_document.Name}";
            var sessionPath = Path.GetFullPath(SessionFiles.FileFor(_document.Name));
            AddLog($"Session data and rules saved to '{sessionPath}'.");
            StatusText.Text = "Session saved";
            RefreshSessions(_document.Name);
        }
        catch (Exception ex) { ShowError("Could not save session", ex); }
    }
    private void NewDataset_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) { MessageBox.Show("Create or load a session first."); return; }
        var name = Prompt("Create dataset", "Dataset name:"); if (string.IsNullOrWhiteSpace(name)) return;
        if (_document.Datasets.ContainsKey(name)) { MessageBox.Show("A dataset with that name already exists."); return; }
        var values = new Dictionary<string, JsonElement>();
        while (true)
        {
            var line = Prompt("Add data item", "Enter item as name=value. Leave blank to finish:");
            if (string.IsNullOrWhiteSpace(line)) break;
            var split = line.IndexOf('='); if (split < 1) { MessageBox.Show("Use name=value."); continue; }
            try { values[line[..split].Trim()] = JsonDocument.Parse(line[(split + 1)..].Trim()).RootElement.Clone(); }
            catch { values[line[..split].Trim()] = JsonDocument.Parse(JsonSerializer.Serialize(line[(split + 1)..].Trim())).RootElement.Clone(); }
        }
        _document.Datasets[name] = values;
        try { RegisterDataset(name, values); MarkDirty(); RefreshLists(); DatasetsBox.SelectedItem = name; AddLog($"Dataset '{name}' registered with {values.Count} item(s)."); }
        catch (Exception ex) { ShowError("Dataset registration failed", ex); }
    }
    private void AddDataItem_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null || _engine == null) { MessageBox.Show("Create or load a session first."); return; }
        if (_document.Datasets.Count == 0) { MessageBox.Show("Create a dataset before adding a data item."); return; }
        var dialog = new Window { Title = "Add data item", Width = 460, Height = 320, MinHeight = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Owner = this };
        var form = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var add = new Button { Content = "Add", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(add); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); form.Children.Add(buttons);
        var fields = new StackPanel();
        fields.Children.Add(new TextBlock { Text = "Dataset:" });
        var datasetPicker = new ComboBox { ItemsSource = _document.Datasets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(5) };
        if (DatasetsBox.SelectedItem is string selected && selected != "(all datasets)") datasetPicker.SelectedItem = selected;
        if (datasetPicker.SelectedIndex < 0) datasetPicker.SelectedIndex = 0;
        fields.Children.Add(datasetPicker);
        fields.Children.Add(new TextBlock { Text = "Data item name:" });
        var itemNameBox = new TextBox { Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(5) }; fields.Children.Add(itemNameBox);
        fields.Children.Add(new TextBlock { Text = "Value:" });
        var valueBox = new TextBox { Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(5) }; fields.Children.Add(valueBox);
        form.Children.Add(fields); dialog.Content = form;
        add.Click += (_, _) =>
        {
            if (datasetPicker.SelectedItem == null || string.IsNullOrWhiteSpace(itemNameBox.Text)) { MessageBox.Show(dialog, "Select a dataset and enter a data item name."); return; }
            if (_document.Datasets[datasetPicker.SelectedItem.ToString()!].ContainsKey(itemNameBox.Text.Trim())) { MessageBox.Show(dialog, "That data item name already exists in this dataset."); return; }
            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true) return;
        var datasetName = datasetPicker.SelectedItem!.ToString()!;
        var values = _document.Datasets[datasetName];
        var itemName = itemNameBox.Text.Trim();
        var valueText = valueBox.Text;
        JsonElement itemValue;
        try { itemValue = JsonDocument.Parse(valueText).RootElement.Clone(); }
        catch { itemValue = JsonDocument.Parse(JsonSerializer.Serialize(valueText)).RootElement.Clone(); }

        _engine.DataService.DeRegisterData(datasetName);
        values[itemName] = itemValue;
        try
        {
            RegisterDataset(datasetName, values);
            MarkDirty(); RefreshLists(); DatasetsBox.SelectedItem = datasetName;
            AddLog($"Added data item '{datasetName}.{itemName}'.");
        }
        catch (Exception ex) { ShowError("Data item registration failed", ex); }
    }
    private void EditDataset_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) { MessageBox.Show("Load a session first."); return; }
        if (DatasetsBox.SelectedItem is not string name || name == "(all datasets)" || !_document.Datasets.TryGetValue(name, out var old)) { MessageBox.Show("Select a specific dataset from the Dataset dropdown first."); return; }
        var lines = string.Join(Environment.NewLine, old.Select(kv => $"{kv.Key}={kv.Value}"));
        var input = Prompt("Edit dataset values", "One item per line, in name=value format:", lines); if (input == null) return;
        var values = new Dictionary<string, JsonElement>();
        try { foreach (var row in input.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)) { var ix = row.IndexOf('='); if (ix < 1) continue; var raw = row[(ix + 1)..].Trim(); try { values[row[..ix].Trim()] = JsonDocument.Parse(raw).RootElement.Clone(); } catch { values[row[..ix].Trim()] = JsonDocument.Parse(JsonSerializer.Serialize(raw)).RootElement.Clone(); } } }
        catch (Exception ex) { ShowError("Invalid dataset", ex); return; }
        _document.Datasets.Remove(name); _document.Datasets[name] = values;
        try { Activate(_document); MarkDirty(); AddLog($"Dataset '{name}' deregistered and registered with updated values."); }
        catch (Exception ex) { ShowError("Dataset registration failed", ex); }
    }
    private void NewRule_Click(object sender, RoutedEventArgs e) => CreateRule(false);
    private void NewDefault_Click(object sender, RoutedEventArgs e) => CreateRule(true);
    private void CreateRule(bool isDefault)
    {
        if (_document == null) { MessageBox.Show("Create or load a session first."); return; }
        var selectedContext = ContextsBox.SelectedItem as string;
        var dialog = new Window { Title = isDefault ? "Create default rule" : "Create rule", Width = 560, Height = isDefault ? 400 : 600, MinHeight = isDefault ? 400 : 560, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.CanResize, Owner = this };
        var panel = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var create = new Button { Content = "Create", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(create); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var fields = new StackPanel();
        TextBox AddField(string label, string initial = "", int height = 30)
        {
            fields.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 5, 0, 3) });
            var box = new TextBox { Text = initial, MinHeight = height, Padding = new Thickness(5), AcceptsReturn = height > 30, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = height > 30 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
            fields.Children.Add(box);
            return box;
        }
        var contextBox = AddField("Context", selectedContext == "(all contexts)" ? "" : selectedContext ?? "");
        var nameBox = AddField("Rule name", isDefault ? "Default" : "Rule" + (_document.Rules.Count + 1));
        TextBox? statementBox = null;
        if (!isDefault)
        {
            fields.Children.Add(new TextBlock { Text = "Statement (for example Customer.Age >= 18)", Margin = new Thickness(0, 6, 0, 3) });
            var statementRow = new DockPanel();
            var syntaxButton = new Button { Content = "Check syntax", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(syntaxButton, Dock.Right);
            statementBox = new TextBox { Height = 70, Padding = new Thickness(5), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            statementRow.Children.Add(syntaxButton); statementRow.Children.Add(statementBox); fields.Children.Add(statementRow);
            syntaxButton.Click += async (_, _) => await CheckStatementSyntaxAsync(statementBox.Text);
        }
        var decisionBox = AddField("Decision");
        var decisionDataBox = AddField("DecisionData (JSON, optional)", "", 55);
        var optionalDataBox = isDefault ? null : AddField("OptionalData (optional)", "", 55);
        panel.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); dialog.Content = panel;
        create.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(contextBox.Text) || string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(decisionBox.Text)
                || (!isDefault && string.IsNullOrWhiteSpace(statementBox?.Text)))
            {
                MessageBox.Show(dialog, "Context, rule name, Decision, and a Statement for non-default rules are required.", "Incomplete rule");
                return;
            }
            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true) return;
        var context = contextBox.Text.Trim();
        var name = nameBox.Text.Trim();
        var r = new RuleDocument
        {
            Number = _document.Rules.Where(rule => rule.Context == context).Select(rule => rule.Number).DefaultIfEmpty(0).Max() + 1,
            Name = name,
            Context = context,
            Statement = statementBox?.Text.Trim() ?? "",
            Decision = decisionBox.Text.Trim(),
            DecisionData = decisionDataBox.Text,
            OptionalData = optionalDataBox?.Text ?? ""
        };
        try { if (isDefault) { _document.Defaults.RemoveAll(x => x.Context == context); _document.Defaults.Add(r); RegisterDefault(r); } else { _document.Rules.Add(r); RegisterRule(r); } MarkDirty(); RefreshLists(); ContextsBox.SelectedItem = context; AddLog($"{(isDefault ? "Default r" : "R")}ule '{name}' registered for '{context}'."); }
        catch (Exception ex) { ShowError("Rule registration failed", ex); }
    }
    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null || _engine == null || RulesGrid.SelectedItem is not RuleDocument rule) { MessageBox.Show("Select a registered rule first."); return; }
        var action = ShowRuleEditor(rule);
        if (action == RuleEditorAction.Cancel) return;
        if (action == RuleEditorAction.Delete)
        {
            if (!_engine.RuleService.DeRegisterRule(rule.Context, rule.Number)) { MessageBox.Show("RightRule did not deregister the selected rule."); return; }
            _document.Rules.Remove(rule);
            MarkDirty(); RefreshLists();
            AddLog($"Rule '{rule.Name}' was deregistered and deleted.");
            return;
        }
        try { Activate(_document); MarkDirty(); AddLog($"Rule '{rule.Name}' deregistered and registered with updated values."); }
        catch (Exception ex) { ShowError("Rule registration failed", ex); }
    }
    private RuleEditorAction ShowRuleEditor(RuleDocument rule)
    {
        var dialog = new Window { Title = $"Edit rule: {rule.Name}", Width = 620, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.CanResize, Owner = this };
        var panel = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var delete = new Button { Content = "Delete rule", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(save); buttons.Children.Add(delete); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var form = new StackPanel();
        TextBox AddField(string label, string value, int height = 30)
        {
            form.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 3) });
            var box = new TextBox { Text = value, Height = height, Padding = new Thickness(5), AcceptsReturn = height > 30, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = height > 30 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
            form.Children.Add(box); return box;
        }
        form.Children.Add(new TextBlock { Text = "Statement", Margin = new Thickness(0, 6, 0, 3) });
        var statementRow = new DockPanel();
        var syntaxButton = new Button { Content = "Check syntax", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(syntaxButton, Dock.Right);
        var statement = new TextBox { Text = rule.Statement, Height = 90, Padding = new Thickness(5), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        statementRow.Children.Add(syntaxButton); statementRow.Children.Add(statement); form.Children.Add(statementRow);
        syntaxButton.Click += async (_, _) => await CheckStatementSyntaxAsync(statement.Text);
        var decision = AddField("Decision", rule.Decision);
        var decisionData = AddField("DecisionData (JSON, optional)", rule.DecisionData, 75);
        var optionalData = AddField("OptionalData", rule.OptionalData, 75);
        panel.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); dialog.Content = panel;
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(statement.Text) || string.IsNullOrWhiteSpace(decision.Text)) { MessageBox.Show(dialog, "Statement and Decision are required.", "Invalid rule"); return; }
            rule.Statement = statement.Text.Trim(); rule.Decision = decision.Text.Trim(); rule.DecisionData = decisionData.Text; rule.OptionalData = optionalData.Text;
            dialog.Tag = RuleEditorAction.Save;
            dialog.DialogResult = true;
        };
        delete.Click += (_, _) =>
        {
            var answer = MessageBox.Show(dialog, $"Delete rule '{rule.Name}' from context '{rule.Context}'?", "Delete rule", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            dialog.Tag = RuleEditorAction.Delete;
            dialog.DialogResult = true;
        };
        return dialog.ShowDialog() == true && dialog.Tag is RuleEditorAction action ? action : RuleEditorAction.Cancel;
    }
    private void RulesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => EditRule_Click(sender, e);
    private async Task CheckStatementSyntaxAsync(string statement)
    {
        if (_engine == null) { MessageBox.Show(this, "Create or load a session before checking statement syntax."); return; }
        try
        {
            var result = await _engine.StatementSyntaxCheckAsync(statement);
            var details = result.Diagnostics.Count == 0 ? "No syntax errors found." : string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString()));
            MessageBox.Show(this, details, result.IsValid ? "Statement syntax is valid" : "Statement syntax errors", MessageBoxButton.OK, result.IsValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { ShowError("Statement syntax check failed", ex); }
    }
    private void DataGridView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_document == null || _engine == null || DataGridView.SelectedItem is not DataRow row || !_document.Datasets.TryGetValue(row.DatasetName, out var values) || !values.TryGetValue(row.ItemName, out var current)) return;
        var dialog = new Window { Title = $"Edit data: {row.DatasetName}.{row.ItemName}", Width = 500, Height = 270, MinHeight = 270, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Owner = this };
        var panel = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = new Button { Content = "Save", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var delete = new Button { Content = "Delete item", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Padding = new Thickness(8, 4, 8, 4) };
        buttons.Children.Add(save); buttons.Children.Add(delete); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
        var form = new StackPanel();
        form.Children.Add(new TextBlock { Text = "Data item name:" });
        var itemName = new TextBox { Text = row.ItemName, Margin = new Thickness(0, 5, 0, 10), Padding = new Thickness(6) }; form.Children.Add(itemName);
        form.Children.Add(new TextBlock { Text = "Value:" });
        var value = new TextBox { Text = current.ToString(), Margin = new Thickness(0, 5, 0, 8), Padding = new Thickness(6) }; form.Children.Add(value); panel.Children.Add(form); dialog.Content = panel;
        save.Click += (_, _) =>
        {
            var proposedName = itemName.Text.Trim();
            if (string.IsNullOrWhiteSpace(proposedName)) { MessageBox.Show(dialog, "Data item name is required."); return; }
            if (proposedName != row.ItemName && values.ContainsKey(proposedName)) { MessageBox.Show(dialog, "That data item name already exists in this dataset."); return; }
            try { dialog.Tag = ParseEditedDataValue(value.Text, current, row.DatasetName, row.ItemName); }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Invalid data value"); return; }
            dialog.DialogResult = true;
        };
        delete.Click += (_, _) =>
        {
            var answer = MessageBox.Show(dialog, $"Delete data item '{row.DatasetName}.{row.ItemName}'?", "Delete data item", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
            dialog.Tag = "delete";
            dialog.DialogResult = true;
        };
        if (dialog.ShowDialog() != true) return;
        var newItemName = itemName.Text.Trim();
        var deleteItem = dialog.Tag is string action && action == "delete";
        if (!deleteItem && dialog.Tag is not JsonElement) { MessageBox.Show("The edited data value could not be read."); return; }
        _engine.DataService.DeRegisterData(row.DatasetName);
        if (deleteItem)
        {
            values.Remove(row.ItemName);
            if (values.Count == 0) { _document.Datasets.Remove(row.DatasetName); AddLog($"Deleted last item; dataset '{row.DatasetName}' was deregistered and removed."); }
            else { RegisterDataset(row.DatasetName, values); AddLog($"Deleted data item '{row.DatasetName}.{row.ItemName}'."); }
        }
        else
        {
            var updated = (JsonElement)dialog.Tag;
            values.Remove(row.ItemName); values[newItemName] = updated; RegisterDataset(row.DatasetName, values);
            AddLog($"Updated data item '{row.DatasetName}.{newItemName}'.");
        }
        MarkDirty(); RefreshLists();
    }
    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_engine == null || ContextsBox.SelectedItem is not string context) { MessageBox.Show("Load a session and select a context first."); return; }
        var ruleService = _engine.RuleService;
        var method = (MethodBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Single";
        var contextsToApply = context == "(all contexts)" ? _contexts.ToArray() : [context];
        if (contextsToApply.Length == 0) { MessageBox.Show("There are no registered contexts to apply."); return; }
        ApplyButton.IsEnabled = false;
        var contextLabel = context == "(all contexts)" ? "all contexts" : context;
        StatusText.Text = $"Applying {method} rules for {contextLabel}…";
        AddLog($"Applying {method} rules for {contextLabel}.");
        try
        {
            await Task.Yield();
            foreach (var selectedContext in contextsToApply)
            {
                if (method == "Single")
                {
                    var result = await ruleService.ApplyRuleAsync(selectedContext);
                    AddLog($"{selectedContext} · Single · {result.Status}: {result.RuleName} — {result.Decision}; Result={result.Result}; Data={result.DecisionData}; StatementApplied={result.StatementApplied}; {result.Message}");
                }
                else
                {
                    var kind = method switch { "Any" => EvaluationTypeEnum.ANY, "All" => EvaluationTypeEnum.ALL, _ => EvaluationTypeEnum.FIRST };
                    var results = await ruleService.ApplyRulesAsync(selectedContext, kind);
                    foreach (var result in results)
                        AddLog($"{selectedContext} · {method} · {result.Status}: {result.RuleName} — {result.Decision}; Result={result.Result}; Data={result.DecisionData}; StatementApplied={result.StatementApplied}; {result.Message}");
                }
            }
            StatusText.Text = $"Rules applied for {contextLabel}";
        }
        catch (Exception ex) { ShowError("Rule application failed", ex); }
        finally { ApplyButton.IsEnabled = true; }
    }
    private void ExportRules_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) { MessageBox.Show("Create or load a session first."); return; }
        try
        {
            var rules = _document.Rules.Concat(_document.Defaults).Select(rule => new
            {
                rule.Number, rule.Name, rule.Context, rule.Decision,
                rule.DecisionData, rule.Statement, rule.OptionalData
            }).ToList();
            var mode = MessageBox.Show(this, "Export all rules to one file? Choose No to export a separate file for each context.", "Export rules", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (mode == MessageBoxResult.Cancel) return;
            if (mode == MessageBoxResult.Yes)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export all rules", FileName = $"{_document.Name}-rules.json", Filter = "JSON files|*.json|All files|*.*", DefaultExt = ".json", AddExtension = true };
                if (dialog.ShowDialog(this) != true) return;
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(rules, SessionFiles.JsonOptions));
                AddLog($"Exported {rules.Count} rule(s) to '{dialog.FileName}'.");
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder for context rule files", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                if (dialog.ShowDialog(this) != true) return;
                foreach (var group in rules.GroupBy(rule => rule.Context))
                {
                    var path = Path.Combine(dialog.FolderName, $"{SafeFileName(group.Key)}-rules.json");
                    File.WriteAllText(path, JsonSerializer.Serialize(group.ToList(), SessionFiles.JsonOptions));
                    AddLog($"Exported {group.Count()} rule(s) for '{group.Key}' to '{path}'.");
                }
            }
            StatusText.Text = "Rules exported";
        }
        catch (Exception ex) { ShowError("Could not export rules", ex); }
    }
    private void ExportData_Click(object sender, RoutedEventArgs e)
    {
        if (_document == null) { MessageBox.Show("Create or load a session first."); return; }
        try
        {
            var datasets = _document.Datasets.Select(dataset => new
            {
                DatasetName = dataset.Key,
                DataItems = dataset.Value.Select(item => new { Key = item.Key, Value = item.Value })
            }).ToList();
            var mode = MessageBox.Show(this, "Export all datasets to one file? Choose No to export a separate file for each dataset.", "Export data", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (mode == MessageBoxResult.Cancel) return;
            if (mode == MessageBoxResult.Yes)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export all data", FileName = $"{_document.Name}-data.json", Filter = "JSON files|*.json|All files|*.*", DefaultExt = ".json", AddExtension = true };
                if (dialog.ShowDialog(this) != true) return;
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(datasets, SessionFiles.JsonOptions));
                AddLog($"Exported {datasets.Count} dataset(s) to '{dialog.FileName}'.");
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder for dataset files", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
                if (dialog.ShowDialog(this) != true) return;
                foreach (var dataset in datasets)
                {
                    var path = Path.Combine(dialog.FolderName, $"{SafeFileName(dataset.DatasetName)}-data.json");
                    File.WriteAllText(path, JsonSerializer.Serialize(new[] { dataset }, SessionFiles.JsonOptions));
                    AddLog($"Exported dataset '{dataset.DatasetName}' to '{path}'.");
                }
            }
            StatusText.Text = "Data exported";
        }
        catch (Exception ex) { ShowError("Could not export data", ex); }
    }
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var safe = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "export" : safe;
    }
    private void DatasetsBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) RefreshDatasetView(); }
    private void ContextsBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_refreshing) RefreshRules(); }
    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Clear();
        StatusText.Text = "Activity and results cleared";
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!SaveIfNeeded()) e.Cancel = true; else CloseEngine(); }
    private void ShowError(string title, Exception ex) { AddLog($"ERROR: {ex.Message}"); MessageBox.Show(ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); }
}
