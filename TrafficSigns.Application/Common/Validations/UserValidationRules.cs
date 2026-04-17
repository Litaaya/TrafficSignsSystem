using System.Text.RegularExpressions;

namespace TrafficSigns.Application.Common.Validations
{
    public static class UserValidationRules
    {
        public const int UsernameMin = 3;
        public const int UsernameMax = 255;
        private static readonly Regex UsernameRegex = new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);
        public static bool IsValidUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (username.Length < UsernameMin || username.Length > UsernameMax) return false;
            if (username.Any(c => c > 127)) return false;
            return UsernameRegex.IsMatch(username);
        }

        public const int NameMax = 255;
        private static readonly Regex NameRegex = new(@"^[\p{L}\s'-]+$", RegexOptions.Compiled);
        public static bool IsValidName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            if (name.Length > NameMax) return false;
            return NameRegex.IsMatch(name);
        }

        public const string PasswordRegexPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$";
        private static readonly Regex PasswordRegex = new(PasswordRegexPattern, RegexOptions.Compiled);
        public static bool IsStrongPassword(string? password) =>
            !string.IsNullOrWhiteSpace(password) && PasswordRegex.IsMatch(password);

        public const int EmailMax = 255;
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMax) return false;
            return EmailRegex.IsMatch(email);
        }

        private static readonly Regex PhoneRegex = new(@"^0[35789][0-9]{8}$", RegexOptions.Compiled);
        public static bool IsValidPhone(string? phone) =>
            !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone);
    }
}
