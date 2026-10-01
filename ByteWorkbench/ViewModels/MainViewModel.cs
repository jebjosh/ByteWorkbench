using System.Collections.ObjectModel;
using System.Windows;
using ByteWorkbench.Models;
using ByteWorkbench.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ByteWorkbench.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConversionService conversionService = new();

    public IReadOnlyList<string> InputFormats { get; } =
        ["Auto", "Hex", "Binary", "Decimal", "Octal", "Bytes (Hex)", "Text (UTF-8)"];

    public IReadOnlyList<string> ByteOrders { get; } = ["Big Endian", "Little Endian"];
    public IReadOnlyList<string> BitWidths { get; } = ["Auto", "8-bit", "16-bit", "32-bit", "64-bit", "128-bit", "256-bit"];

    [ObservableProperty]
    private string inputText = "0x400DB02";

    [ObservableProperty]
    private string selectedInputFormat = "Auto";

    [ObservableProperty]
    private string selectedByteOrder = "Big Endian";

    [ObservableProperty]
    private string selectedBitWidth = "Auto";

    [ObservableProperty]
    private string detectedFormat = "—";

    [ObservableProperty]
    private string summary = "";

    [ObservableProperty]
    private string errorMessage = "";

    [ObservableProperty]
    private ObservableCollection<RepresentationRow> representations = [];

    [ObservableProperty]
    private ObservableCollection<EndianRow> endianRows = [];

    public MainViewModel()
    {
        Recalculate();
    }

    partial void OnInputTextChanged(string value) => Recalculate();
    partial void OnSelectedInputFormatChanged(string value) => Recalculate();
    partial void OnSelectedByteOrderChanged(string value) => Recalculate();
    partial void OnSelectedBitWidthChanged(string value) => Recalculate();

    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        SelectedInputFormat = "Auto";
        SelectedByteOrder = "Big Endian";
        SelectedBitWidth = "Auto";
    }

    [RelayCommand]
    private void LoadExample(string? example)
    {
        if (string.IsNullOrWhiteSpace(example)) return;

        switch (example)
        {
            case "Hex":
                SelectedInputFormat = "Hex";
                InputText = "0x400DB02";
                break;
            case "Binary":
                SelectedInputFormat = "Binary";
                InputText = "00100100 00001101 10110000 00000010";
                break;
            case "Bytes":
                SelectedInputFormat = "Bytes (Hex)";
                InputText = "04 00 DB 02";
                break;
            case "Signed":
                SelectedInputFormat = "Decimal";
                SelectedBitWidth = "16-bit";
                InputText = "-1234";
                break;
        }
    }

    [RelayCommand]
    private void Copy(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            Clipboard.SetText(value);
    }

    private void Recalculate()
    {
        try
        {
            ConversionResult result = conversionService.Convert(InputText, SelectedInputFormat, SelectedByteOrder, SelectedBitWidth);
            DetectedFormat = result.DetectedFormat;
            Summary = result.Summary;
            Representations = new ObservableCollection<RepresentationRow>(result.Representations);
            EndianRows = new ObservableCollection<EndianRow>(result.EndianRows);
            ErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            ErrorMessage = ex.Message;
            DetectedFormat = "Invalid";
            Summary = "Fix the input and the conversions will update automatically.";
            Representations = [];
            EndianRows = [];
        }
    }
}
