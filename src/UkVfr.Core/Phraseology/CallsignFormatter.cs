namespace UkVfr.Core.Phraseology;

/// <summary>
/// Formats aircraft callsigns using the ICAO phonetic alphabet.
/// Handles full and abbreviated forms per CAP 413.
/// </summary>
public static class CallsignFormatter
{
    private static readonly Dictionary<char, string> PhoneticAlphabet = new()
    {
        ['A'] = "Alpha", ['B'] = "Bravo", ['C'] = "Charlie", ['D'] = "Delta",
        ['E'] = "Echo", ['F'] = "Foxtrot", ['G'] = "Golf", ['H'] = "Hotel",
        ['I'] = "India", ['J'] = "Juliet", ['K'] = "Kilo", ['L'] = "Lima",
        ['M'] = "Mike", ['N'] = "November", ['O'] = "Oscar", ['P'] = "Papa",
        ['Q'] = "Quebec", ['R'] = "Romeo", ['S'] = "Sierra", ['T'] = "Tango",
        ['U'] = "Uniform", ['V'] = "Victor", ['W'] = "Whiskey", ['X'] = "X-ray",
        ['Y'] = "Yankee", ['Z'] = "Zulu",
        ['0'] = "Zero", ['1'] = "One", ['2'] = "Two", ['3'] = "Three",
        ['4'] = "Four", ['5'] = "Five", ['6'] = "Six", ['7'] = "Seven",
        ['8'] = "Eight", ['9'] = "Niner"
    };

    /// <summary>
    /// Formats a registration (e.g. "G-ABCD") into full phonetic form.
    /// Returns "Golf Alpha Bravo Charlie Delta".
    /// </summary>
    public static string ToFullPhonetic(string registration)
    {
        var clean = registration.Replace("-", "").ToUpperInvariant();
        var words = new List<string>(clean.Length);

        foreach (var ch in clean)
        {
            if (PhoneticAlphabet.TryGetValue(ch, out var word))
                words.Add(word);
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Formats a registration into abbreviated form (aircraft type prefix + last 2 chars).
    /// "G-ABCD" → "Golf Charlie Delta" (first letter + last two).
    /// Per CAP 413, after initial contact the callsign may be abbreviated.
    /// </summary>
    public static string ToAbbreviated(string registration)
    {
        var clean = registration.Replace("-", "").ToUpperInvariant();
        if (clean.Length < 3) return ToFullPhonetic(registration);

        var first = clean[0];
        var lastTwo = clean[^2..];
        var abbreviated = $"{first}{lastTwo}";

        var words = new List<string>(3);
        foreach (var ch in abbreviated)
        {
            if (PhoneticAlphabet.TryGetValue(ch, out var word))
                words.Add(word);
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Formats a number for RT (e.g. squawk "4512" → "four five one two",
    /// QNH "1013" → "one zero one three").
    /// </summary>
    public static string SpokenDigits(string number)
    {
        var words = new List<string>(number.Length);
        foreach (var ch in number)
        {
            if (PhoneticAlphabet.TryGetValue(ch, out var word))
                words.Add(word.ToLowerInvariant());
        }
        return string.Join(" ", words);
    }

    /// <summary>
    /// Formats a runway designator for RT (e.g. "24" → "two four").
    /// </summary>
    public static string SpokenRunway(string designator)
    {
        return SpokenDigits(designator.TrimEnd('L', 'R', 'C'));
    }
}
