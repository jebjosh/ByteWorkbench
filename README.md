# Byte Workbench

A small WPF utility for converting values between hex, binary, decimal, octal, byte arrays, and UTF-8 text. It also compares big-endian and little-endian interpretations and shows signed two's-complement values.

## Stack

- WPF
- C# / .NET 8
- CommunityToolkit.Mvvm 8.4.2
- MVVM with `ObservableObject`, `[ObservableProperty]`, and `[RelayCommand]`

## Run

1. Install Visual Studio 2022 (17.8+) with **.NET desktop development** or install the .NET 8 SDK on Windows.
2. Open `ByteWorkbench.sln`.
3. Restore NuGet packages.
4. Run the `ByteWorkbench` project.

Or from a Windows terminal:

```powershell
dotnet restore
dotnet run --project .\ByteWorkbench\ByteWorkbench.csproj
```

## Input examples

- Hex: `0x400DB02`
- Binary: `00100100 00001101 10110000 00000010`
- Decimal: `12345678`
- Signed decimal: `-1234` (try a fixed bit width)
- Octal: `0o755`
- Bytes: `40 0D B0 02` or `0x40, 0x0D, 0xB0, 0x02`
- Text: `Hello`

## Notes

- **Auto** treats plain digits as decimal so `1010` means decimal 1010. Use `0b1010` or choose Binary to make it binary.
- For raw byte arrays, **Byte order** controls the main decimal/signed interpretation.
- The endian comparison section always shows both interpretations of the same byte sequence.
- A fixed bit width is useful when you want to see exact `Int16`, `Int32`, or `Int64` two's-complement behavior.
