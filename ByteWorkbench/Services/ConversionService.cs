using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ByteWorkbench.Models;

namespace ByteWorkbench.Services;

public sealed class ConversionService
{
    private static readonly int[] StandardWidths = [8, 16, 32, 64, 128, 256];

    public ConversionResult Convert(string input, string requestedFormat, string byteOrder, string bitWidthText)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return EmptyResult();
        }

        string format = requestedFormat == "Auto" ? DetectFormat(input) : requestedFormat;
        int? requestedWidth = ParseRequestedWidth(bitWidthText);

        ParsedInput parsed = format switch
        {
            "Hex" => ParseUnsignedRadix(input, 16, requestedWidth, "Hex"),
            "Binary" => ParseUnsignedRadix(input, 2, requestedWidth, "Binary"),
            "Decimal" => ParseDecimal(input, requestedWidth),
            "Octal" => ParseUnsignedRadix(input, 8, requestedWidth, "Octal"),
            "Bytes (Hex)" => ParseBytes(input, byteOrder, requestedWidth),
            "Text (UTF-8)" => ParseText(input, byteOrder, requestedWidth),
            _ => throw new FormatException($"Unsupported input format: {format}")
        };

        byte[] rawBytes = parsed.Bytes;
        bool selectedBigEndian = byteOrder == "Big Endian";

        BigInteger selectedUnsigned = parsed.IsScalar
            ? parsed.UnsignedValue
            : new BigInteger(rawBytes, isUnsigned: true, isBigEndian: selectedBigEndian);

        BigInteger selectedSigned = parsed.IsScalar
            ? ToSigned(parsed.UnsignedValue, parsed.BitWidth)
            : new BigInteger(rawBytes, isUnsigned: false, isBigEndian: selectedBigEndian);

        BigInteger beUnsigned = new(rawBytes, isUnsigned: true, isBigEndian: true);
        BigInteger leUnsigned = new(rawBytes, isUnsigned: true, isBigEndian: false);
        BigInteger beSigned = new(rawBytes, isUnsigned: false, isBigEndian: true);
        BigInteger leSigned = new(rawBytes, isUnsigned: false, isBigEndian: false);

        string hexCompact = "0x" + System.Convert.ToHexString(rawBytes);
        string hexBytes = string.Join(" ", rawBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        string binary = string.Join(" ", rawBytes.Select(b => System.Convert.ToString(b, 2).PadLeft(8, '0')));
        string decimalBytes = string.Join(" ", rawBytes.Select(b => b.ToString(CultureInfo.InvariantCulture)));
        string ascii = BuildAsciiPreview(rawBytes);
        string utf8 = BuildUtf8Preview(rawBytes);

        var representations = new List<RepresentationRow>
        {
            new("Hex", hexCompact, "Raw byte sequence"),
            new("Hex bytes", hexBytes, $"{rawBytes.Length} byte(s)"),
            new("Binary", binary, $"{parsed.BitWidth} bits"),
            new("Octal", ToBaseString(selectedUnsigned, 8), $"Using {byteOrder.ToLowerInvariant()} interpretation"),
            new("Decimal (unsigned)", selectedUnsigned.ToString(CultureInfo.InvariantCulture), $"Using {byteOrder.ToLowerInvariant()} interpretation"),
            new("Decimal (signed)", selectedSigned.ToString(CultureInfo.InvariantCulture), "Two's complement"),
            new("Bytes (decimal)", decimalBytes, "Each byte shown as 0-255"),
            new("ASCII preview", ascii, "Non-printable bytes are shown as ·"),
            new("UTF-8 preview", utf8, "Best-effort text decoding")
        };

        var endianRows = new List<EndianRow>
        {
            new("Unsigned", beUnsigned.ToString(CultureInfo.InvariantCulture), leUnsigned.ToString(CultureInfo.InvariantCulture)),
            new("Signed", beSigned.ToString(CultureInfo.InvariantCulture), leSigned.ToString(CultureInfo.InvariantCulture)),
            new("Hex byte order", hexBytes, string.Join(" ", rawBytes.Reverse().Select(b => b.ToString("X2", CultureInfo.InvariantCulture))))
        };

        AddIntegerTypeRows(endianRows, rawBytes);

        return new ConversionResult
        {
            DetectedFormat = format,
            Summary = $"{rawBytes.Length} byte(s) • {parsed.BitWidth} bits • input interpreted as {format}",
            Representations = representations,
            EndianRows = endianRows
        };
    }

    private static ConversionResult EmptyResult() => new()
    {
        DetectedFormat = "—",
        Summary = "Enter a value to see every representation.",
        Representations = [],
        EndianRows = []
    };

    private static string DetectFormat(string input)
    {
        string s = input.Trim();

        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return "Hex";
        if (s.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) return "Binary";
        if (s.StartsWith("0o", StringComparison.OrdinalIgnoreCase)) return "Octal";

        if ((s.Contains(' ') || s.Contains(',') || s.Contains('-')) && LooksLikeHexBytes(s))
            return "Bytes (Hex)";

        if (Regex.IsMatch(s, @"^[+-]?\d+$"))
            return "Decimal";

        if (Regex.IsMatch(s, @"^[0-9A-Fa-f_]+$") && Regex.IsMatch(s, @"[A-Fa-f]"))
            return "Hex";

        return "Text (UTF-8)";
    }

    private static bool LooksLikeHexBytes(string input)
    {
        string[] tokens = Regex.Split(input.Trim(), @"[\s,;:-]+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        return tokens.Length > 0 && tokens.All(t =>
        {
            string clean = StripPrefix(t, "0x");
            return clean.Length is >= 1 and <= 2 && Regex.IsMatch(clean, @"^[0-9A-Fa-f]{1,2}$");
        });
    }

    private static ParsedInput ParseUnsignedRadix(string input, int radix, int? requestedWidth, string formatName)
    {
        string clean = input.Trim().Replace("_", "").Replace(" ", "");
        clean = radix switch
        {
            16 => StripPrefix(clean, "0x"),
            2 => StripPrefix(clean, "0b"),
            8 => StripPrefix(clean, "0o"),
            _ => clean
        };

        if (string.IsNullOrWhiteSpace(clean))
            throw new FormatException($"Enter a {formatName.ToLowerInvariant()} value.");

        BigInteger value = ParseBigIntegerRadix(clean, radix);
        int requiredBits = Math.Max(1, GetUnsignedBitLength(value));
        int width = ResolveWidth(requiredBits, requestedWidth, signed: false);
        byte[] bytes = UnsignedToFixedBytes(value, width);

        return new ParsedInput(bytes, value, width, true);
    }

    private static ParsedInput ParseDecimal(string input, int? requestedWidth)
    {
        string clean = input.Trim().Replace("_", "").Replace(",", "");
        if (!BigInteger.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger signedValue))
            throw new FormatException("Enter a valid decimal integer.");

        int requiredBits;
        int width;
        BigInteger unsignedBits;

        if (signedValue >= 0)
        {
            requiredBits = Math.Max(1, GetUnsignedBitLength(signedValue));
            width = ResolveWidth(requiredBits, requestedWidth, signed: false);
            unsignedBits = signedValue;
        }
        else
        {
            requiredBits = GetSignedBitLength(signedValue);
            width = ResolveWidth(requiredBits, requestedWidth, signed: true);
            BigInteger modulus = BigInteger.One << width;
            unsignedBits = modulus + signedValue;
        }

        byte[] bytes = UnsignedToFixedBytes(unsignedBits, width);
        return new ParsedInput(bytes, unsignedBits, width, true);
    }

    private static ParsedInput ParseBytes(string input, string byteOrder, int? requestedWidth)
    {
        string[] tokens = Regex.Split(input.Trim(), @"[\s,;:-]+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        if (tokens.Length == 0)
            throw new FormatException("Enter one or more hex bytes, for example: 40 0D B0 02");

        var bytes = new List<byte>(tokens.Length);
        foreach (string token in tokens)
        {
            string clean = StripPrefix(token, "0x");
            if (clean.Length is < 1 or > 2 || !byte.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                throw new FormatException($"'{token}' is not a valid hex byte.");
            bytes.Add(value);
        }

        byte[] padded = ApplyRequestedWidth(bytes.ToArray(), requestedWidth, byteOrder);
        return new ParsedInput(padded, BigInteger.Zero, padded.Length * 8, false);
    }

    private static ParsedInput ParseText(string input, string byteOrder, int? requestedWidth)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        if (bytes.Length == 0)
            throw new FormatException("Enter some text.");

        byte[] padded = ApplyRequestedWidth(bytes, requestedWidth, byteOrder);
        return new ParsedInput(padded, BigInteger.Zero, padded.Length * 8, false);
    }

    private static byte[] ApplyRequestedWidth(byte[] source, int? requestedWidth, string byteOrder)
    {
        if (requestedWidth is null) return source;
        if (requestedWidth.Value % 8 != 0) throw new FormatException("Bit width must be a multiple of 8.");

        int requestedBytes = requestedWidth.Value / 8;
        if (source.Length > requestedBytes)
            throw new OverflowException($"Input uses {source.Length * 8} bits, which does not fit in {requestedWidth.Value} bits.");

        if (source.Length == requestedBytes) return source;

        byte[] result = new byte[requestedBytes];
        if (byteOrder == "Big Endian")
            Buffer.BlockCopy(source, 0, result, requestedBytes - source.Length, source.Length);
        else
            Buffer.BlockCopy(source, 0, result, 0, source.Length);

        return result;
    }

    private static int? ParseRequestedWidth(string bitWidthText)
    {
        if (bitWidthText == "Auto") return null;
        return int.TryParse(bitWidthText.Replace("-bit", "", StringComparison.OrdinalIgnoreCase).Trim(), out int width)
            ? width
            : null;
    }

    private static int ResolveWidth(int requiredBits, int? requestedWidth, bool signed)
    {
        if (requestedWidth is int width)
        {
            int capacity = signed ? width : width;
            if (requiredBits > capacity)
                throw new OverflowException($"Value needs at least {requiredBits} bits and does not fit in {width} bits.");
            return width;
        }

        foreach (int candidate in StandardWidths)
        {
            if (requiredBits <= candidate) return candidate;
        }

        return ((requiredBits + 7) / 8) * 8;
    }

    private static BigInteger ParseBigIntegerRadix(string digits, int radix)
    {
        BigInteger result = BigInteger.Zero;
        foreach (char c in digits)
        {
            int value = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'F' => c - 'A' + 10,
                >= 'a' and <= 'f' => c - 'a' + 10,
                _ => -1
            };

            if (value < 0 || value >= radix)
                throw new FormatException($"'{c}' is not valid in base {radix}.");

            result = result * radix + value;
        }
        return result;
    }

    private static byte[] UnsignedToFixedBytes(BigInteger value, int bitWidth)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

        int byteCount = (bitWidth + 7) / 8;
        byte[] minimal = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (minimal.Length > byteCount)
            throw new OverflowException($"Value does not fit in {bitWidth} bits.");

        byte[] result = new byte[byteCount];
        Buffer.BlockCopy(minimal, 0, result, byteCount - minimal.Length, minimal.Length);
        return result;
    }

    private static BigInteger ToSigned(BigInteger unsignedValue, int bitWidth)
    {
        if (bitWidth <= 0) return unsignedValue;
        BigInteger signBit = BigInteger.One << (bitWidth - 1);
        BigInteger modulus = BigInteger.One << bitWidth;
        return (unsignedValue & signBit) != 0 ? unsignedValue - modulus : unsignedValue;
    }

    private static int GetUnsignedBitLength(BigInteger value)
    {
        if (value.IsZero) return 1;
        int bits = 0;
        BigInteger n = value;
        while (n > 0)
        {
            n >>= 1;
            bits++;
        }
        return bits;
    }

    private static int GetSignedBitLength(BigInteger value)
    {
        // Smallest two's-complement width that can represent the negative value.
        int bits = 1;
        while (value < -(BigInteger.One << (bits - 1)))
            bits++;
        return bits;
    }

    private static string ToBaseString(BigInteger value, int radix)
    {
        if (value.IsZero) return "0";
        if (value < 0) return "-" + ToBaseString(BigInteger.Abs(value), radix);

        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var sb = new StringBuilder();
        BigInteger current = value;
        while (current > 0)
        {
            current = BigInteger.DivRem(current, radix, out BigInteger remainder);
            sb.Insert(0, chars[(int)remainder]);
        }
        return sb.ToString();
    }

    private static string BuildAsciiPreview(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length);
        foreach (byte b in bytes)
            sb.Append(b is >= 32 and <= 126 ? (char)b : '·');
        return sb.ToString();
    }

    private static string BuildUtf8Preview(byte[] bytes)
    {
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "(not valid UTF-8)";
        }
    }

    private static string StripPrefix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static void AddIntegerTypeRows(List<EndianRow> rows, byte[] rawBytes)
    {
        int bits = rawBytes.Length * 8;
        switch (bits)
        {
            case 8:
                rows.Add(new EndianRow("UInt8", rawBytes[0].ToString(CultureInfo.InvariantCulture), rawBytes[0].ToString(CultureInfo.InvariantCulture)));
                rows.Add(new EndianRow("Int8", unchecked((sbyte)rawBytes[0]).ToString(CultureInfo.InvariantCulture), unchecked((sbyte)rawBytes[0]).ToString(CultureInfo.InvariantCulture)));
                break;
            case 16:
                AddFixedWidthRows(rows, rawBytes, "UInt16", "Int16", 16);
                break;
            case 32:
                AddFixedWidthRows(rows, rawBytes, "UInt32", "Int32", 32);
                break;
            case 64:
                AddFixedWidthRows(rows, rawBytes, "UInt64", "Int64", 64);
                break;
        }
    }

    private static void AddFixedWidthRows(List<EndianRow> rows, byte[] bytes, string unsignedName, string signedName, int bitWidth)
    {
        BigInteger beUnsigned = new(bytes, isUnsigned: true, isBigEndian: true);
        BigInteger leUnsigned = new(bytes, isUnsigned: true, isBigEndian: false);
        BigInteger modulus = BigInteger.One << bitWidth;
        BigInteger signBit = BigInteger.One << (bitWidth - 1);

        BigInteger beSigned = (beUnsigned & signBit) != 0 ? beUnsigned - modulus : beUnsigned;
        BigInteger leSigned = (leUnsigned & signBit) != 0 ? leUnsigned - modulus : leUnsigned;

        rows.Add(new EndianRow(unsignedName, beUnsigned.ToString(CultureInfo.InvariantCulture), leUnsigned.ToString(CultureInfo.InvariantCulture)));
        rows.Add(new EndianRow(signedName, beSigned.ToString(CultureInfo.InvariantCulture), leSigned.ToString(CultureInfo.InvariantCulture)));
    }

    private sealed record ParsedInput(byte[] Bytes, BigInteger UnsignedValue, int BitWidth, bool IsScalar);
}
