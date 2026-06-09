using System;
using cdeLib.Infrastructure;

namespace cdeAppCore.Validation;

/// <summary>
/// Which search filter a validation failure relates to (for structured, UI-agnostic handling).
/// </summary>
public enum SearchFilterField
{
    None = 0,
    Regex,
    Size,
    Date,
    Hour
}

/// <summary>
/// The outcome of validating a search query: either valid, or a failure carrying a human-readable
/// message and the offending field. Pure data — no UI dependency.
/// </summary>
public readonly record struct SearchValidationResult(bool IsValid, SearchFilterField Field, string Message)
{
    public static SearchValidationResult Valid { get; } = new(true, SearchFilterField.None, null);

    public static SearchValidationResult Invalid(SearchFilterField field, string message)
        => new(false, field, message);
}

/// <summary>
/// Pure, frontend-agnostic search-filter validation. Mirrors the rules previously inline in the
/// WinForms presenter (regex validity, and From/To size/date/hour consistency) with identical
/// messages, but takes already-resolved values rather than reading WinForms controls.
/// </summary>
public static class SearchFilterValidator
{
    public const string FromToSizeMessage =
        "The From Size Field is greater than the To Size field no search results possible.";

    public const string FromToDateMessage =
        "The From Date Field is greater than the To Date field no search results possible.";

    public const string FromToHourMessage =
        "The From Hour Field is greater than the To Hour field no search results possible.";

    /// <summary>Regex is invalid only when regex mode is on and the pattern fails to compile.</summary>
    public static SearchValidationResult ValidateRegex(bool regexMode, string pattern)
    {
        if (!regexMode) return SearchValidationResult.Valid;
        var regexError = RegexHelper.GetRegexErrorMessage(pattern);
        return string.IsNullOrEmpty(regexError)
            ? SearchValidationResult.Valid
            : SearchValidationResult.Invalid(SearchFilterField.Regex, regexError);
    }

    /// <summary>Invalid only when both ends are enabled and From-size exceeds To-size.</summary>
    public static SearchValidationResult ValidateSize(bool fromEnabled, long fromSize, bool toEnabled, long toSize)
    {
        if (fromEnabled && toEnabled && fromSize > toSize)
            return SearchValidationResult.Invalid(SearchFilterField.Size, FromToSizeMessage);
        return SearchValidationResult.Valid;
    }

    /// <summary>Invalid only when both ends are enabled and From-date is not before To-date.</summary>
    public static SearchValidationResult ValidateDate(bool fromEnabled, DateTime fromDate, bool toEnabled, DateTime toDate)
    {
        if (fromEnabled && toEnabled && fromDate >= toDate)
            return SearchValidationResult.Invalid(SearchFilterField.Date, FromToDateMessage);
        return SearchValidationResult.Valid;
    }

    /// <summary>Invalid only when both ends are enabled and From-hour is not before To-hour.</summary>
    public static SearchValidationResult ValidateHour(bool fromEnabled, TimeSpan fromHour, bool toEnabled, TimeSpan toHour)
    {
        if (fromEnabled && toEnabled && fromHour.TotalSeconds >= toHour.TotalSeconds)
            return SearchValidationResult.Invalid(SearchFilterField.Hour, FromToHourMessage);
        return SearchValidationResult.Valid;
    }

    /// <summary>
    /// Validate all filters, returning the first failure in the same precedence the presenter used
    /// (regex, then size, then date, then hour). Returns <see cref="SearchValidationResult.Valid"/>
    /// when every filter is consistent.
    /// </summary>
    public static SearchValidationResult Validate(
        bool regexMode, string pattern,
        bool fromSizeEnabled, long fromSize, bool toSizeEnabled, long toSize,
        bool fromDateEnabled, DateTime fromDate, bool toDateEnabled, DateTime toDate,
        bool fromHourEnabled, TimeSpan fromHour, bool toHourEnabled, TimeSpan toHour)
    {
        var regex = ValidateRegex(regexMode, pattern);
        if (!regex.IsValid) return regex;

        var size = ValidateSize(fromSizeEnabled, fromSize, toSizeEnabled, toSize);
        if (!size.IsValid) return size;

        var date = ValidateDate(fromDateEnabled, fromDate, toDateEnabled, toDate);
        if (!date.IsValid) return date;

        return ValidateHour(fromHourEnabled, fromHour, toHourEnabled, toHour);
    }
}
