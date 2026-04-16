namespace TrafficSigns.Application.Common.Validations;

public static class AccountValidationRules
{
    public const int NameMin = 1;
    public const int NameMax = 100;
    public const int DescMax = 500;

    public static bool IsValidDescription(string? desc) =>
        string.IsNullOrEmpty(desc) || desc.Length <= DescMax;

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= NameMax;

    public const string PhoneRegex = @"^0[35789][0-9]{8}$";

    public static bool IsValidPhone(string? phone) =>
        string.IsNullOrEmpty(phone) || System.Text.RegularExpressions.Regex.IsMatch(phone, PhoneRegex);

    public static bool IsValidEmail(string? email) =>
        string.IsNullOrEmpty(email) || new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email);
}