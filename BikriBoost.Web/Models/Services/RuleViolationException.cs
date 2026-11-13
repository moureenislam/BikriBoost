namespace BikriBoost.Web.Models.Services;

/// <summary>
/// Thrown when a business rule is broken (e.g. "a TrxID can be used only once").
/// Controllers catch it and show the message to the user; nothing is saved.
/// </summary>
public class RuleViolationException(string message) : Exception(message);
