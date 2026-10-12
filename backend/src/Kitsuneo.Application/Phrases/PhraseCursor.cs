using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Kitsuneo.Application.Phrases;

/// <summary>
/// Position of the last phrase of a page, in the list order (CreatedAt and Id, both descending).
/// Travels to the client as an opaque base64url string.
/// </summary>
public sealed record PhraseCursor(DateTime CreatedAt, Guid Id)
{
    private const char Separator = '_';

    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"{CreatedAt.Ticks.ToString(CultureInfo.InvariantCulture)}{Separator}{Id:N}"));

    public static bool TryDecode(string value, out PhraseCursor? cursor)
    {
        cursor = null;

        if (!Base64Url.IsValid(value))
            return false;

        var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split(Separator);

        if (parts.Length != 2
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > DateTime.MaxValue.Ticks
            || !Guid.TryParseExact(parts[1], "N", out var id))
            return false;

        cursor = new PhraseCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        return true;
    }
}
